using System.Data;
using System.Data.Common;
using System.Reflection;
using Dm;
using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Tests.Storage;

/// <summary>
/// Tests for the connection and transaction layer. Everything the provider inherits from the
/// relational layer is exercised here, because it is the part no other test touches.
/// </summary>
public class DmConnectionTests
{
    [Fact]
    public void CreateDbConnection_MarksTheConnectionForEfCore()
    {
        using var context = new ProbeContext("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        var connection = Assert.IsType<DmConnection>(context.Database.GetDbConnection());

        Assert.True(connection.ForEFCore);
    }

    /// <summary>
    /// The framework enlists the transaction of a <see cref="System.Transactions.TransactionScope" /> only
    /// for a provider that says ambient transactions are supported, and the base default is no: with the
    /// switch off, a write inside a scope that was never completed survived, measured on 8.1.4.80, and with
    /// it on the write did not.
    /// </summary>
    [Fact]
    public void AmbientTransactions_AreSupported()
    {
        using var context = new ProbeContext("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        var connection = context.GetService<IRelationalConnection>();

        var supported = typeof(RelationalConnection)
            .GetProperty("SupportsAmbientTransactions", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(connection);

        Assert.True((bool)supported!);
    }

    [Fact]
    public void OpenAndClose_ChangeTheState()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        context.Database.OpenConnection();
        Assert.Equal(ConnectionState.Open, connection.State);

        context.Database.CloseConnection();
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public async Task OpenAndCloseAsync_ChangeTheState()
    {
        var connection = new FakeDmConnection();
        await using var context = new ProbeContext(connection);

        await context.Database.OpenConnectionAsync();
        Assert.Equal(ConnectionState.Open, connection.State);

        await context.Database.CloseConnectionAsync();
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    /// <summary>
    /// Opening is counted, so a caller that opens twice still has the connection open after the
    /// first close. The relational layer tracks this so that nested opens cannot close too early.
    /// </summary>
    [Fact]
    public void OpenTwice_NeedsTwoCloses()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        context.Database.OpenConnection();
        context.Database.OpenConnection();
        context.Database.CloseConnection();

        Assert.Equal(ConnectionState.Open, connection.State);

        context.Database.CloseConnection();
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public void Commit_ReachesTheDriverTransaction()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        using var transaction = context.Database.BeginTransaction();
        transaction.Commit();

        var driverTransaction = Assert.Single(connection.Transactions);
        Assert.True(driverTransaction.Committed);
        Assert.False(driverTransaction.RolledBack);
    }

    [Fact]
    public void Rollback_ReachesTheDriverTransaction()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        using var transaction = context.Database.BeginTransaction();
        transaction.Rollback();

        var driverTransaction = Assert.Single(connection.Transactions);
        Assert.True(driverTransaction.RolledBack);
        Assert.False(driverTransaction.Committed);
    }

    /// <summary>
    /// Disposing without committing leaves the rollback to the driver, which rolls back a
    /// transaction that is disposed while still uncommitted. The provider's job is to dispose it.
    /// </summary>
    [Fact]
    public void DisposingWithoutCommit_DisposesTheDriverTransaction()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        using (context.Database.BeginTransaction())
        {
        }

        var driverTransaction = Assert.Single(connection.Transactions);
        Assert.True(driverTransaction.Disposed);
        Assert.False(driverTransaction.Committed);
    }

    /// <summary>
    /// A unit of work that builds more than one command has to be atomic, so the relational layer
    /// opens a transaction around <c>SaveChanges</c> and commits it.
    /// </summary>
    [Fact]
    public void SaveChanges_WrapsMultipleCommandsInATransaction()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        context.Add(new Probe { Id = 1, Name = "a" });
        context.Add(new Probe { Id = 2, Name = "b" });
        context.SaveChanges();

        var driverTransaction = Assert.Single(connection.Transactions);
        Assert.True(driverTransaction.Committed);
    }

    [Fact]
    public void SingleCommandSaveChanges_NeedsNoTransaction()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        context.Add(new Probe { Id = 1, Name = "a" });
        context.SaveChanges();

        Assert.Empty(connection.Transactions);
    }

    /// <summary>
    /// Savepoints do not go through the driver transaction. The relational layer runs them as
    /// statements on the connection, so the provider's part is the SQL generation helper.
    /// </summary>
    [Fact]
    public void Savepoints_AreExecutedAsStatements()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        using var transaction = context.Database.BeginTransaction();
        transaction.CreateSavepoint("sp1");
        transaction.RollbackToSavepoint("sp1");
        transaction.ReleaseSavepoint("sp1");

        Assert.Equal(
            ["SAVEPOINT \"sp1\";", "ROLLBACK TO \"sp1\";", "RELEASE SAVEPOINT \"sp1\";"],
            connection.Commands);
    }

    private sealed class ProbeContext : DbContext
    {
        private readonly FakeDmConnection? _connection;
        private readonly string? _connectionString;

        public ProbeContext(FakeDmConnection connection) => _connection = connection;

        public ProbeContext(string connectionString) => _connectionString = connectionString;

        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (_connection is not null)
            {
                optionsBuilder.UseDm(_connection);
            }
            else
            {
                optionsBuilder.UseDm(_connectionString!);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(entity =>
            {
                entity.Property(p => p.Id).ValueGeneratedNever();
                entity.Property(p => p.Name).HasMaxLength(50);
            });
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
