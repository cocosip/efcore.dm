using System.Data;
using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Storage;

/// <summary>
/// T2 tests for the transaction the provider opens. They drive the real transaction pipeline against a
/// recording connection, so what the context reports and what the driver is asked to do are both
/// covered.
/// </summary>

public class DmTransactionTests
{
    [Fact]
    public void BeginTransaction_OpensOneTransactionWithTheRequestedIsolation()
    {
        var connection = new FakeDmConnection { RowsAffected = 1 };
        using var context = new ProbeContext(connection);

        using var transaction = context.Database.BeginTransaction(IsolationLevel.Serializable);

        var opened = Assert.Single(connection.Transactions);
        Assert.Equal(IsolationLevel.Serializable, opened.IsolationLevel);
        Assert.NotNull(context.Database.CurrentTransaction);
    }

    /// <summary>
    /// A unit of work inside an explicit transaction has to run on that transaction rather than open a
    /// second one, which the connection would reject.
    /// </summary>
    [Fact]
    public void SaveChanges_InATransaction_ReusesIt()
    {
        var connection = new FakeDmConnection { RowsAffected = 1 };
        using var context = new ProbeContext(connection);

        using var transaction = context.Database.BeginTransaction();
        context.Add(new Probe { Id = 1, Name = "a" });
        context.SaveChanges();

        Assert.Single(connection.Transactions);

        // The unit of work is bracketed by a savepoint, so a statement that failed part way could be
        // undone without losing the whole transaction.
        Assert.Equal(
            Normalize(
                """
                SAVEPOINT "__EFSavePoint";
                INSERT INTO "Probes" ("Id", "Name")
                VALUES (:p0, :p1);
                RELEASE SAVEPOINT "__EFSavePoint";
                """),
            Joined(connection));
    }

    /// <summary>
    /// A savepoint is managed by statements rather than by the driver transaction, so the three
    /// statements are what a database has to accept; the shape is asserted here because it is the
    /// dialect surface that is unverified against a server.
    /// </summary>
    [Fact]
    public void Savepoints_AreManagedByTheirOwnStatements()
    {
        var connection = new FakeDmConnection { RowsAffected = 1 };
        using var context = new ProbeContext(connection);

        using var transaction = context.Database.BeginTransaction();

        transaction.CreateSavepoint("before");
        transaction.RollbackToSavepoint("before");
        transaction.ReleaseSavepoint("before");

        Assert.Equal(
            Normalize(
                """
                SAVEPOINT "before";
                ROLLBACK TO "before";
                RELEASE SAVEPOINT "before";
                """),
            Joined(connection));
    }

    [Fact]
    public void Commit_ClearsTheCurrentTransaction()
    {
        var connection = new FakeDmConnection { RowsAffected = 1 };
        using var context = new ProbeContext(connection);

        var transaction = context.Database.BeginTransaction();
        transaction.Commit();

        Assert.True(Assert.Single(connection.Transactions).Committed);
        Assert.Null(context.Database.CurrentTransaction);
    }

    [Fact]
    public void SecondTransaction_IsRejected()
    {
        var connection = new FakeDmConnection { RowsAffected = 1 };
        using var context = new ProbeContext(connection);

        using var transaction = context.Database.BeginTransaction();

        Assert.Throws<InvalidOperationException>(() => context.Database.BeginTransaction());
    }

    private static string Joined(FakeDmConnection connection)
        => string.Join("\n", connection.Commands.Select(Normalize));

    private static string Normalize(string sql) => sql.ReplaceLineEndings("\n").TrimEnd();

    private sealed class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public ProbeContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(e => e.Property(p => p.Name).HasMaxLength(50));
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
