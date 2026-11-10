using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

// The package smoke test: a project outside this repository's build restores the packed provider and
// uses it the way an application does. It needs no database, so it runs on every build.

var assembly = typeof(DmValueGenerationStrategy).Assembly;

Report("assembly", assembly.GetName().Name!);
Report("version", assembly.GetName().Version!.ToString());

using var context = new SmokeContext();

var script = context.Database.GenerateCreateScript();

Report("create script", Environment.NewLine + script);

return Verify(assembly, script) ? 0 : 1;

static void Report(string label, string value) => Console.WriteLine($"{label}: {value}");

static bool Verify(System.Reflection.Assembly assembly, string script)
{
    var problems = new List<string>();

    if (assembly.GetName().Name != "DM.EntityFrameworkCore")
    {
        problems.Add($"the provider assembly is {assembly.GetName().Name}");
    }

    if (!script.Contains("CREATE TABLE \"SMOKE\"", StringComparison.Ordinal))
    {
        problems.Add("the create script has no SMOKE table");
    }

    if (!script.Contains("IDENTITY", StringComparison.Ordinal))
    {
        problems.Add("the create script has no identity column");
    }

    foreach (var problem in problems)
    {
        Console.Error.WriteLine("error: " + problem);
    }

    return problems.Count == 0;
}

internal sealed class SmokeContext : DbContext
{
    public DbSet<Smoke> Smokes => Set<Smoke>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDm("Server=localhost;Port=5236;User Id=SYSDBA;Password=SYSDBA");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Smoke>(
            entity =>
            {
                entity.ToTable("SMOKE");
                entity.HasKey(smoke => smoke.Id);
                entity.Property(smoke => smoke.Id).ValueGeneratedOnAdd();
                entity.Property(smoke => smoke.Name).HasMaxLength(50);
            });
}

internal sealed class Smoke
{
    public int Id { get; set; }

    public string? Name { get; set; }
}
