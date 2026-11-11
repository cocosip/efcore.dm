using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DM.EntityFrameworkCore.Tests.Migrations;

/// <summary>
/// Composes a whole migration script the way <c>dotnet ef migrations script</c> does, rather than one
/// operation at a time. The script is what a deployment runs, so the shapes each part produces have to
/// fit together: a batch separator on a line of its own, a transaction statement Dameng defines, and
/// the history guard wrapping the statements of the migration.
/// </summary>
public class DmMigrationScriptTests
{
    /// <summary>
    /// The terminator is a slash on a line of its own. The writer appends the next statement directly
    /// after it, so a terminator without the trailing line feed leaves a line that reads
    /// <c>/START</c>, which no script runner accepts.
    /// </summary>
    [Fact]
    public void EveryBatchSeparatorIsOnItsOwnLine()
    {
        var script = Script(MigrationsSqlGenerationOptions.Default);

        Assert.Contains("\n/\n", script, StringComparison.Ordinal);
        Assert.DoesNotContain("/START", script, StringComparison.Ordinal);
        Assert.DoesNotContain("/CREATE", script, StringComparison.Ordinal);

        foreach (var line in script.Split('\n').Where(line => line.Contains('/')))
        {
            Assert.Equal("/", line);
        }
    }

    /// <summary>
    /// Dameng starts a transaction with the first statement it runs, and its manual defines no
    /// <c>START TRANSACTION</c> statement, so the script carries none. The commit stays, because
    /// <c>COMMIT</c> is defined and a script run has to end the transaction it opened.
    /// </summary>
    [Fact]
    public void TheScriptOpensNoTransaction()
    {
        var script = Script(MigrationsSqlGenerationOptions.Default);

        Assert.DoesNotContain("START TRANSACTION", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COMMIT;", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// An idempotent script wraps the migration in the history guard and the create in a catalog
    /// guard, which is two blocks nested rather than two statements. The nesting has to close in the
    /// order it opened.
    /// </summary>
    [Fact]
    public void TheIdempotentScriptNestsTheGuardsAndClosesThem()
    {
        var script = Script(MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains(
            "SELECT COUNT(*) INTO CNT FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260101_Script'",
            script,
            StringComparison.Ordinal);

        Assert.Contains(
            "SELECT COUNT(*) INTO declared FROM ALL_TABLES WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = 'ScriptItems';",
            script,
            StringComparison.Ordinal);

        var opens = script.Split("BEGIN", StringSplitOptions.None).Length - 1;
        var closes = script.Split("END;", StringSplitOptions.None).Length - 1;

        Assert.True(closes >= opens, $"The script opens {opens} blocks and closes {closes}.");

        Assert.DoesNotContain("/DECLARE", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// The history table is created by an anonymous block too, so an idempotent script is a sequence of
    /// blocks and the migration statements between them, never one block that swallowed the next.
    /// </summary>
    [Fact]
    public void TheHistoryTableIsCreatedByItsOwnBlock()
    {
        var script = Script(MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("EXECUTE IMMEDIATE 'CREATE TABLE \"__EFMigrationsHistory\"", script, StringComparison.Ordinal);
        Assert.Contains(
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\")",
            script,
            StringComparison.Ordinal);
    }

    internal sealed class ScriptContext : DbContext
    {
        public DbSet<ScriptItem> Items => Set<ScriptItem>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ScriptItem>().ToTable("ScriptItems");
    }
    private static string Script(MigrationsSqlGenerationOptions options)
    {
        using var context = new ScriptContext();

        return context.GetService<IMigrator>().GenerateScript(null, null, options).Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}

[DbContext(typeof(DmMigrationScriptTests.ScriptContext))]
[Migration("20260101_Script")]
public class ScriptMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.CreateTable(
            name: "ScriptItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "INT", nullable: false),
                Name = table.Column<string>(type: "VARCHAR(50)", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ScriptItems", x => x.Id));
}

[DbContext(typeof(DmMigrationScriptTests.ScriptContext))]
public class ScriptModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
        => modelBuilder.Entity<ScriptItem>().ToTable("ScriptItems");
}

public class ScriptItem
{
    public int Id { get; set; }

    public string? Name { get; set; }
}
