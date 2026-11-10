using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Update;

/// <summary>
/// T2 tests for the statements <see cref="DbContext.SaveChanges" /> issues. They drive the real
/// update pipeline against a recording connection, so parameter names, batching and the
/// concurrency check are covered rather than only the SQL generator in isolation.
/// </summary>
public class DmUpdateSqlTests
{
    private const string InsertSql = """
        INSERT INTO "Probes" ("Id", "Name")
        VALUES (:p0, :p1);
        """;

    private const string UpdateSql = """
        UPDATE "Probes" SET "Name" = :p0
        WHERE "Id" = :p1 AND "Name" = :p2;
        """;

    private const string DeleteSql = """
        DELETE FROM "Probes"
        WHERE "Id" = :p0 AND "Name" = :p1;
        """;

    [Fact]
    public void Insert_GeneratesInsertWithoutReturning()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        context.Probes.Add(new Probe { Id = 1, Name = "a" });
        context.SaveChanges();

        Assert.Equal(InsertSql, Statement(Assert.Single(connection.Commands)));
    }

    [Fact]
    public void Update_GeneratesUpdateWithConcurrencyCondition()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        var probe = new Probe { Id = 1, Name = "a" };
        context.Attach(probe);
        probe.Name = "b";
        context.SaveChanges();

        Assert.Equal(UpdateSql, Statement(Assert.Single(connection.Commands)));
    }

    [Fact]
    public void Delete_GeneratesDeleteWithConcurrencyCondition()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        context.Remove(new Probe { Id = 1, Name = "a" });
        context.SaveChanges();

        Assert.Equal(DeleteSql, Statement(Assert.Single(connection.Commands)));
    }

    [Fact]
    public void Insert_WithoutBatch_IssuesOneStatementPerEntity()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        context.Probes.AddRange(new Probe { Id = 1, Name = "a" }, new Probe { Id = 2, Name = "b" });
        context.SaveChanges();

        Assert.Equal(2, connection.Commands.Count);
    }

    [Fact]
    public void SaveChanges_WhenNoRowMatches_ThrowsConcurrencyException()
    {
        var connection = new FakeDmConnection { RowsAffected = 0 };
        using var context = new ProbeContext(connection);

        context.Remove(new Probe { Id = 1, Name = "a" });

        var exception = Assert.Throws<DbUpdateConcurrencyException>(() => context.SaveChanges());

        Assert.Single(exception.Entries);
    }

    private static string Statement(string sql)
        => sql.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

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
                entity.Property(e => e.Name).IsConcurrencyToken();
            });
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
