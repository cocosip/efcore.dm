#Requires -Version 7
<#
.SYNOPSIS
    Runs the design-time tooling against a live Dameng instance.

.DESCRIPTION
    The acceptance console exercises the runtime path: a migration is applied by the application itself.
    This script exercises the path a developer takes, which is the `dotnet ef` tooling: a migration is
    added, applied, listed and removed, and the schema is scaffolded back into a compiling model.

    It builds a throwaway project that references the provider, so nothing is added to the repository.
    The connection string is read from the argument, or from the user secrets of the acceptance sample.

.PARAMETER ConnectionString
    The connection string. It is never printed.

.PARAMETER Secrets
    The user secrets key to read when no connection string is given. Defaults to `Dm`.

.PARAMETER Keep
    Leaves the throwaway project behind for inspection.

.EXAMPLE
    pwsh -File scripts/design-time-smoke.ps1 -Secrets Dm
#>
[CmdletBinding()]
param(
    [string] $ConnectionString,
    [string] $Secrets = 'Dm',
    [switch] $Keep
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-ConnectionString {
    if ($ConnectionString) {
        return $ConnectionString
    }

    $root = Join-Path $env:APPDATA 'Microsoft\UserSecrets\dm-entityframeworkcore-validation'
    $file = Join-Path $root 'secrets.json'

    if (-not (Test-Path $file)) {
        throw "No connection string was given and $file does not exist. Run: dotnet user-secrets set `"ConnectionStrings:$Secrets`" `"<connection string>`" --project samples/DmProviderValidation"
    }

    $secretData = Get-Content $file -Raw | ConvertFrom-Json

    # `dotnet user-secrets set "ConnectionStrings:Dm" ...` writes the key with the colon in it, which is
    # not the same shape as a nested object, so both are read.
    $key = "ConnectionStrings:$Secrets"
    $property = $secretData.PSObject.Properties[$key]
    $value = if ($property) { $property.Value } else { $null }

    if (-not $value) {
        $nested = $secretData.PSObject.Properties['ConnectionStrings']

        if ($nested) {
            $value = $nested.Value.PSObject.Properties[$Secrets].Value
        }
    }

    if (-not $value) {
        throw "The user secrets do not hold ConnectionStrings:$Secrets"
    }

    return $value
}

function Invoke-Tool {
    param([string] $Label, [string[]] $Arguments)

    $output = & dotnet @Arguments 2>&1 | Out-String

    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed:`n$output"
    }

    Write-Host "  ok    $Label"
    return $output
}

$connection = Resolve-ConnectionString
$repo = Split-Path -Parent $PSScriptRoot
$provider = Join-Path $repo 'src/DM.EntityFrameworkCore/DM.EntityFrameworkCore.csproj'
$work = Join-Path ([IO.Path]::GetTempPath()) 'dm-design-time'

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $work -Force | Out-Null

$env:DM_DESIGN_CONNECTION = $connection

Write-Host 'building the throwaway project'

Set-Content (Join-Path $work 'Probe.csproj') @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <NoWarn>`$(NoWarn);EF1001</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$provider" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.14" />
  </ItemGroup>
</Project>
"@

# Note carries an identity key, which the scaffolded model has to keep. Probe carries an identity
# column that is not the key, which is the only shape where the flag cannot come from a convention, and
# an ANSI column, whose length the catalog reports in two units and whose declaration names which one.
Set-Content (Join-Path $work 'Model.cs') @'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Probe;

public class Note
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public List<Probe> Probes { get; set; } = [];
}

public class Probe
{
    public int Id { get; set; }
    public int Sequence { get; set; }
    public string Label { get; set; } = "";
    public string Code { get; set; } = "";
    public int NoteId { get; set; }
    public Note Note { get; set; } = null!;
}

public class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Probe> Probes => Set<Probe>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>().Property(note => note.Text).HasMaxLength(100);
        modelBuilder.Entity<Probe>().Property(probe => probe.Id).ValueGeneratedNever();
        modelBuilder.Entity<Probe>().Property(probe => probe.Label).HasMaxLength(50);
        modelBuilder.Entity<Probe>().Property(probe => probe.Code).HasMaxLength(3).IsUnicode(false);
        modelBuilder.Entity<Probe>().Property(probe => probe.Sequence).UseIdentityColumn();
        modelBuilder.Entity<Probe>().HasIndex(probe => probe.Label).IsUnique();
        modelBuilder.Entity<Probe>().HasOne(probe => probe.Note).WithMany(note => note.Probes).HasForeignKey(probe => probe.NoteId);
    }
}

public class Factory : IDesignTimeDbContextFactory<ProbeContext>
{
    public ProbeContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<ProbeContext>()
            .UseDm(Environment.GetEnvironmentVariable("DM_DESIGN_CONNECTION")!)
            .Options);
}
'@

# The tooling needs an executable, and the only thing the executable does is drop what the run created:
# a reverted migration leaves the history table behind, so the tables are dropped by name instead.
Set-Content (Join-Path $work 'Program.cs') @'
using Dm;

if (args is not ["drop"])
{
    throw new InvalidOperationException("run with the argument 'drop'");
}

await using var connection = new DmConnection(Environment.GetEnvironmentVariable("DM_DESIGN_CONNECTION")!);
await connection.OpenAsync();

foreach (var statement in new[] { "DROP TABLE \"Probes\"", "DROP TABLE \"Notes\"", "DROP TABLE \"__EFMigrationsHistory\"" })
{
    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        await command.ExecuteNonQueryAsync();
    }
    catch (DmException)
    {
        // A table this run did not create is not an error.
    }
}
'@

Push-Location $work
try {
    Invoke-Tool 'build' @('build', '--nologo', '-v', 'quiet') | Out-Null

    $migration = Invoke-Tool 'migrations add' @('ef', 'migrations', 'add', 'Initial', '--context', 'ProbeContext')
    $files = Get-ChildItem (Join-Path $work 'Migrations') -Filter '*_Initial.cs' -ErrorAction SilentlyContinue

    if (-not $files) {
        throw 'the migration file was not written'
    }

    # The C# migration carries the operations; the SQL is what the migrations SQL generator makes of them,
    # so the shapes are asserted on the script rather than on the C# file.
    $body = Get-Content $files[0].FullName -Raw

    if ($body -notmatch 'CreateTable' -or $body -notmatch 'Dm:ValueGenerationStrategy') {
        throw 'the migration does not carry the table and the identity annotation'
    }

    $sql = Invoke-Tool 'migrations script' @('ef', 'migrations', 'script', '--context', 'ProbeContext')

    foreach ($shape in 'CREATE TABLE', 'IDENTITY(1,1)', 'CREATE UNIQUE INDEX', 'FOREIGN KEY') {
        if ($sql -notmatch [regex]::Escape($shape)) {
            throw "the migration script does not contain $shape"
        }
    }

    Write-Host '  ok    the migration script carries the table, the identity column, the index and the foreign key'

    Invoke-Tool 'database update' @('ef', 'database', 'update', '--context', 'ProbeContext') | Out-Null

    $second = Invoke-Tool 'database update again' @('ef', 'database', 'update', '--context', 'ProbeContext')
    if ($second -notmatch 'already up to date') {
        throw 'the second update did not report that the database is up to date'
    }

    $listed = Invoke-Tool 'migrations list' @('ef', 'migrations', 'list', '--context', 'ProbeContext')
    if ($listed -notmatch 'Initial') {
        throw 'the applied migration is not listed'
    }

    $scaffolded = Join-Path $work 'Scaffolded'
    Invoke-Tool 'dbcontext scaffold' @(
        'ef', 'dbcontext', 'scaffold', $connection, 'DM.EntityFrameworkCore',
        '--output-dir', 'Scaffolded', '--namespace', 'Probe.Scaffolded', '--no-onconfiguring',
        '--table', 'Notes', '--table', 'Probes'
    ) | Out-Null

    $context = Get-ChildItem $scaffolded -Filter '*Context.cs' | Select-Object -First 1

    if (-not $context) {
        throw 'the scaffolded context was not written'
    }

    $scaffoldedContext = Get-Content $context.FullName -Raw

    # The non-key identity column is the assertion that matters: a key is identity by convention, so only
    # this one proves the catalog flag reached the model.
    if ($scaffoldedContext -notmatch 'Sequence\b.*UseIdentityColumn|UseIdentityColumn\(\)') {
        throw 'the scaffolded model lost the identity column'
    }

    if ($scaffoldedContext -notmatch 'HasIndex' -or $scaffoldedContext -notmatch 'HasOne') {
        throw 'the scaffolded model lost an index or a foreign key'
    }

    # The ANSI column is declared in characters and the catalog reports it in two units, so the unit has
    # to survive: the scaffolded model has to read the column back as the same declaration.
    if ($scaffoldedContext -notmatch 'Code[^\n]*VARCHAR2\(3 CHAR\)') {
        $codeLine = ($scaffoldedContext -split "`n" | Where-Object { $_ -match 'Code' } | Select-Object -First 1)
        throw "the scaffolded model lost the ANSI character unit: $codeLine"
    }

    Write-Host '  ok    the scaffolded model keeps the identity column, the index and the foreign key'

    Invoke-Tool 'build the scaffolded model' @('build', '--nologo', '-v', 'quiet') | Out-Null

    Invoke-Tool 'migrations remove' @('ef', 'migrations', 'remove', '--force', '--context', 'ProbeContext') | Out-Null

    if (Get-ChildItem (Join-Path $work 'Migrations') -Filter '*_Initial.cs' -ErrorAction SilentlyContinue) {
        throw 'the migration file survived the remove'
    }

    Write-Host '  ok    the migration was removed'
}
finally {
    # The script owns the tables it created: it drops them by name rather than by reverting, because a
    # reverted migration leaves the history table behind and the next run would not be from scratch.
    try {
        Invoke-Tool 'drop the tables' @('run', '--no-build', '--', 'drop') | Out-Null
    }
    catch {
        Write-Host "  warn  the tables could not be dropped: $($_.Exception.Message)"
    }

    Pop-Location
    $env:DM_DESIGN_CONNECTION = $null

    if (-not $Keep) {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
    else {
        Write-Host "left behind: $work"
    }
}

Write-Host 'design-time smoke test passed'
