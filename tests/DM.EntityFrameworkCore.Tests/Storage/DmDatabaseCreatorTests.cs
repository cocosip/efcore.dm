using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Tests.Storage;

/// <summary>
/// T2 tests for the database lifecycle. They cover the catalog query and the ensure-created
/// path; creating the database itself happens outside SQL and is rejected on purpose.
/// </summary>
public class DmDatabaseCreatorTests
{
    private const string HasTablesSql =
        "SELECT CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END FROM SYS.SYSOBJECTS"
        + " WHERE SCHID = CURRENT_SCHID()"
        + " AND TYPE$ = 'SCHOBJ' AND SUBTYPE$ = 'UTAB'"
        + " AND NAME NOT LIKE 'BIN$%'";

    [Fact]
    public void HasTables_QueriesTheCatalogAndReportsAnEmptyDatabase()
    {
        var connection = new FakeDmConnection { ScalarResult = 0 };
        using var context = new ProbeContext(connection);

        Assert.False(context.GetService<IRelationalDatabaseCreator>().HasTables());

        Assert.Equal(HasTablesSql, Compact(Assert.Single(connection.Commands)));
    }

    [Fact]
    public void HasTables_ReportsPopulatedWhenTheCountIsPositive()
    {
        var connection = new FakeDmConnection { ScalarResult = 1 };
        using var context = new ProbeContext(connection);

        Assert.True(context.GetService<IRelationalDatabaseCreator>().HasTables());
    }

    [Fact]
    public async Task HasTablesAsync_ReportsTheSameResult()
    {
        var connection = new FakeDmConnection { ScalarResult = 1 };
        await using var context = new ProbeContext(connection);

        Assert.True(await context.GetService<IRelationalDatabaseCreator>().HasTablesAsync());
    }

    [Fact]
    public void EnsureCreated_CreatesTablesInAnExistingDatabase()
    {
        var connection = new FakeDmConnection { ScalarResult = 0 };
        using var context = new ProbeContext(connection);

        Assert.True(context.Database.EnsureCreated());

        Assert.Contains(
            connection.Commands,
            command => command.Contains("CREATE TABLE \"Probes\"", StringComparison.Ordinal));
    }

    [Fact]
    public void EnsureCreated_DoesNothingWhenTheDatabaseHasTables()
    {
        var connection = new FakeDmConnection { ScalarResult = 1 };
        using var context = new ProbeContext(connection);

        Assert.False(context.Database.EnsureCreated());

        Assert.Equal(HasTablesSql, Compact(Assert.Single(connection.Commands)));
    }

    [Fact]
    public void Create_IsRejectedBecauseDamengCreatesDatabasesOutsideSql()
    {
        using var context = new ProbeContext(new FakeDmConnection());

        var exception = Assert.Throws<NotSupportedException>(
            () => context.GetService<IRelationalDatabaseCreator>().Create());

        Assert.Contains("dminit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Delete_IsRejectedBecauseAConnectedSessionCannotDropItsOwnDatabase()
    {
        using var context = new ProbeContext(new FakeDmConnection());

        Assert.Throws<NotSupportedException>(() => context.GetService<IRelationalDatabaseCreator>().Delete());
    }

    private static string Compact(string sql)
        => string.Join(
            "\n",
            sql.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0));

    private sealed class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public ProbeContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            });
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
