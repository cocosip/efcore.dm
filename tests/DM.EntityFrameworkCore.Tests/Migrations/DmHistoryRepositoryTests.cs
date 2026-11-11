using DM.EntityFrameworkCore.Dialect;
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
        "MigrationId" NVARCHAR2(150) NOT NULL,
        "ProductVersion" NVARCHAR2(32) NOT NULL,
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
    public void AcquireDatabaseLock_RequestsANamedLockExclusiveForItsWait()
    {
        var connection = new FakeDmConnection();
        using var context = new HistoryContext(connection);

        using (context.GetService<IHistoryRepository>().AcquireDatabaseLock())
        {
            Assert.Equal(
                $"SELECT DBMS_LOCK.REQUEST({DmNamedLock.Id("fake")}, 6, 30, FALSE) FROM DUAL;",
                Compact(Assert.Single(connection.Commands)));
        }
    }

    /// <summary>
    /// The lock belongs to the session, not to a transaction: the server commits around DDL, so a
    /// transaction scoped lock is gone before the migration it guards has finished.
    /// </summary>
    [Fact]
    public void DatabaseLock_IsReleasedWithTheConnection()
    {
        using var context = new HistoryContext(new FakeDmConnection());

        Assert.Equal(
            LockReleaseBehavior.Connection,
            context.GetService<IHistoryRepository>().LockReleaseBehavior);
    }

    /// <summary>
    /// The name is a function of the database, so that two deployments of the same database contend for one
    /// lock and deployments of different databases do not. The value is fixed, because a hash the runtime
    /// randomises per process would give every process its own lock.
    /// </summary>
    [Fact]
    public void NamedLockId_DependsOnTheNameAlone()
    {
        // The runtime randomises string hash codes per process, so the id cannot come from one: two
        // deployments in two processes must ask for the same lock.
        Assert.Equal(DmNamedLock.Id("hidos"), DmNamedLock.Id("HIDOS"));
        Assert.Equal(DmNamedLock.Id("hidos"), DmNamedLock.Id("hidos"));
        Assert.NotEqual(DmNamedLock.Id("hidos"), DmNamedLock.Id("another"));

        // Measured on 8.1.4.80: 2^30 - 1 is granted and 2^30 answers a parameter error, so the id stays
        // below the ceiling rather than merely inside the range of an int.
        Assert.InRange(DmNamedLock.Id("hidos"), 0, (1 << 30) - 1);
    }

    /// <summary>
    /// Measured on 8.1.4.80: a second session answered `1` while the first held the lock, and `0` once it was
    /// released, which is what the interpretation reads.
    /// </summary>
    [Fact]
    public void DatabaseLock_ReportsAWaitThatRanOut()
    {
        var connection = new FakeDmConnection { ScalarResult = 1 };
        using var context = new HistoryContext(connection);

        var exception = Assert.Throws<TimeoutException>(
            () => context.GetService<IHistoryRepository>().AcquireDatabaseLock());

        Assert.Contains("migration lock", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DatabaseLock_TreatsASessionThatAlreadyHoldsItAsGranted()
    {
        var connection = new FakeDmConnection { ScalarResult = 4 };
        using var context = new HistoryContext(connection);

        using var databaseLock = context.GetService<IHistoryRepository>().AcquireDatabaseLock();

        // Only the request was issued: the session already held it, so releasing it is left to the holder.
        Assert.Single(connection.Commands);
    }

    [Fact]
    public void GetCreateIfNotExistsScript_WrapsTheCreateInAnExecutableBlock()
    {
        using var context = new HistoryContext(new FakeDmConnection());

        Assert.Equal(
            """
            BEGIN
            IF (SELECT COUNT(*) FROM SYS.SYSOBJECTS WHERE NAME = '__EFMigrationsHistory' AND SCHID = (SELECT ID FROM SYS.SYSOBJECTS WHERE TYPE$ = 'SCH' AND NAME = (SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')))) = 0 THEN
            BEGIN
            EXECUTE IMMEDIATE 'CREATE TABLE "__EFMigrationsHistory" ( "MigrationId" NVARCHAR2(150) NOT NULL, "ProductVersion" NVARCHAR2(32) NOT NULL, CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId") )';
            EXCEPTION
            WHEN OTHERS THEN
            IF (SELECT COUNT(*) FROM SYS.SYSOBJECTS WHERE NAME = '__EFMigrationsHistory' AND SCHID = (SELECT ID FROM SYS.SYSOBJECTS WHERE TYPE$ = 'SCH' AND NAME = (SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')))) = 0 THEN
            RAISE;
            END IF;
            END;
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
