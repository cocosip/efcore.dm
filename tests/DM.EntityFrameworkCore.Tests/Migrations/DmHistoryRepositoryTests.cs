using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DM.EntityFrameworkCore.Tests.Migrations;

/// <summary>
/// T2 tests for the history repository and the migration lock. The lock and the existence check
/// are asserted from the statements the repository issues, not from the driver's return value.
/// </summary>
public class DmHistoryRepositoryTests
{
    private const string HistoryTable = "__EFMigrationsHistory";

    private const string CreateSql = """
        CREATE TABLE "__EFMigrationsHistory" (
        "MigrationId" VARCHAR(150) NOT NULL,
        "ProductVersion" VARCHAR(32) NOT NULL,
        CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
        );
        """;

    [Fact]
    public void GetCreateScript_CreatesTheHistoryTable()
    {
        using var context = new HistoryContext(new FakeDmConnection());

        Assert.Equal(CreateSql, Compact(context.GetService<IHistoryRepository>().GetCreateScript()));
    }

    [Fact]
    public void GetInsertScript_InsertsTheAppliedMigration()
    {
        using var context = new HistoryContext(new FakeDmConnection());

        Assert.Equal(
            """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('20260101_Init', '10.0.1');
            """,
            Compact(context.GetService<IHistoryRepository>().GetInsertScript(new HistoryRow("20260101_Init", "10.0.1"))));
    }

    [Fact]
    public void GetDeleteScript_DeletesTheAppliedMigration()
    {
        using var context = new HistoryContext(new FakeDmConnection());

        Assert.Equal(
            """
            DELETE FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = '20260101_Init';
            """,
            Compact(context.GetService<IHistoryRepository>().GetDeleteScript("20260101_Init")));
    }

    [Fact]
    public void Exists_QueriesTheSystemCatalog()
    {
        var connection = new FakeDmConnection();
        using var context = new HistoryContext(connection);

        Assert.False(context.GetService<IHistoryRepository>().Exists());

        Assert.Equal(
            """
            SELECT ID FROM SYS.SYSOBJECTS WHERE NAME = '__EFMigrationsHistory' AND SCHID = (SELECT ID FROM SYS.SYSOBJECTS WHERE TYPE$ = 'SCH' AND NAME = (SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')));
            """,
            Compact(Assert.Single(connection.Commands)));
    }

    [Fact]
    public void AcquireDatabaseLock_TakesAnExclusiveTableLock()
    {
        var connection = new FakeDmConnection();
        using var context = new HistoryContext(connection);

        using (context.GetService<IHistoryRepository>().AcquireDatabaseLock())
        {
            Assert.Equal("LOCK TABLE \"__EFMigrationsHistory\" IN EXCLUSIVE MODE;", Compact(Assert.Single(connection.Commands)));
        }
    }

    [Fact]
    public void GetCreateIfNotExistsScript_WrapsTheCreateInAnExecutableBlock()
    {
        using var context = new HistoryContext(new FakeDmConnection());

        Assert.Equal(
            """
            BEGIN
            IF (SELECT COUNT(*) FROM SYS.SYSOBJECTS WHERE NAME = '__EFMigrationsHistory' AND SCHID = (SELECT ID FROM SYS.SYSOBJECTS WHERE TYPE$ = 'SCH' AND NAME = (SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')))) = 0 THEN
            EXECUTE IMMEDIATE 'CREATE TABLE "__EFMigrationsHistory" (
            "MigrationId" VARCHAR(150) NOT NULL,
            "ProductVersion" VARCHAR(32) NOT NULL,
            CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            )';
            END IF;
            END;
            """,
            Compact(context.GetService<IHistoryRepository>().GetCreateIfNotExistsScript()));
    }

    [Theory]
    [InlineData("NotExists", "IF CNT = 0 THEN")]
    [InlineData("Exists", "IF CNT = 1 THEN")]
    public void ConditionalScriptBlock_GuardsOnTheMigrationRow(string kind, string condition)
    {
        using var context = new HistoryContext(new FakeDmConnection());
        var repository = context.GetService<IHistoryRepository>();

        var begin = kind == "Exists"
            ? repository.GetBeginIfExistsScript("20260101_Init")
            : repository.GetBeginIfNotExistsScript("20260101_Init");

        Assert.Equal(
            $"""
             DECLARE CNT INT;
             BEGIN
             SELECT COUNT(*) INTO CNT FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260101_Init';
             {condition}
             """,
            Compact(begin));

        Assert.Equal(
            """
            END IF;
            END;
            /
            """,
            Compact(repository.GetEndIfScript()));
    }

    /// <summary>
    /// Collapses indentation and blank lines, so that an assertion covers the statements rather
    /// than the layout the command list happens to produce.
    /// </summary>
    private static string Compact(string sql)
        => string.Join(
            "\n",
            sql.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0));

    private sealed class HistoryContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public HistoryContext(FakeDmConnection connection) => _connection = connection;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);
    }
}
