using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// T2 tests for behavior the provider inherits from the relational layer rather than implements. They
/// pin the behavior down so that a change in the base class, or a missing registration, shows up here
/// instead of in an application.
/// </summary>
/// <remarks>
/// Each scenario has its own context type, because EF Core caches the model per context type.
/// </remarks>
public class DmInheritedBehaviorTests
{
    [Fact]
    public void CommandTimeout_ReachesTheCommand()
    {
        var connection = new FakeDmConnection { RowsAffected = 1 };

        using var context = new ProbeContext(connection);
        context.Database.SetCommandTimeout(7);
        context.Add(new Probe { Id = 1, Name = "a" });

        context.SaveChanges();

        Assert.Equal(7, connection.LastCommandTimeout);
    }

    /// <summary>
    /// A strategy that retried would replay a statement whose failure may already have changed the
    /// database, so the default has to be the one that runs a unit of work once.
    /// </summary>
    [Fact]
    public void ExecutionStrategy_DoesNotRetry()
    {
        using var context = new ProbeContext(new FakeDmConnection());

        var strategy = context.Database.CreateExecutionStrategy();

        Assert.IsType<NonRetryingExecutionStrategy>(strategy);
        Assert.False(strategy.RetriesOnFailure);
    }

    /// <summary>
    /// A key the store does not generate has to be filled before the statement runs, which is where a
    /// client side generator applies.
    /// </summary>
    [Fact]
    public void ClientSideValueGeneration_FillsAGuidKey()
    {
        using var context = new GuidKeyContext(new FakeDmConnection { RowsAffected = 1 });
        var probe = new GuidKeyProbe();

        context.Add(probe);
        context.SaveChanges();

        Assert.NotEqual(Guid.Empty, probe.Id);
    }

    /// <summary>
    /// A key the store generates is marked temporary while it is pending, so two new entities can hold
    /// the same default value without the identity map confusing them, and the real value replaces it
    /// once the insert returns it.
    /// </summary>
    [Fact]
    public void ClientSideValueGeneration_MarksAStoreGeneratedKeyAsTemporary()
    {
        var connection = new FakeDmConnection { RowsAffected = 1, ReadBackColumns = ["Id"] };
        connection.ReadBackRows.AddRange([[5], [6]]);
        using var context = new ProbeContext(connection);
        var first = new Probe { Name = "a" };
        var second = new Probe { Name = "b" };

        context.AddRange(first, second);

        Assert.True(context.Entry(first).Property(p => p.Id).IsTemporary);
        Assert.True(context.Entry(second).Property(p => p.Id).IsTemporary);
        Assert.Equal(2, context.ChangeTracker.Entries().Count());

        context.SaveChanges();

        Assert.Equal(5, first.Id);
        Assert.Equal(6, second.Id);
        Assert.False(context.Entry(first).Property(p => p.Id).IsTemporary);
    }

    [Fact]
    public void QueryFilter_IsAppliedToTheQuery()
    {
        using var context = new FilteredContext(new FakeDmConnection());

        var sql = context.Probes.ToQueryString();

        Assert.Contains("WHERE", sql, StringComparison.Ordinal);
        Assert.Contains("<> 'hidden'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_StopsAnAsyncQuery()
    {
        using var context = new ProbeContext(new FakeDmConnection());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Probes.ToListAsync(cancelled.Token));
    }

    /// <summary>
    /// A query is cached against the model it was compiled for, so two models that map the same type
    /// to different tables must not share SQL. A provider only needs its own cache key when its
    /// translation depends on something the model does not carry.
    /// </summary>
    [Fact]
    public void QueryCache_DistinguishesTheModel()
    {
        using var first = new ProbeContext(new FakeDmConnection());
        using var second = new RenamedProbeContext(new FakeDmConnection());

        Assert.NotEqual(first.Probes.ToQueryString(), second.Probes.ToQueryString());
        Assert.Contains("\"RenamedProbes\"", second.Probes.ToQueryString(), StringComparison.Ordinal);
    }

    private class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public ProbeContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(e => e.Property(p => p.Name).HasMaxLength(50));
    }

    private sealed class RenamedProbeContext : ProbeContext
    {
        public RenamedProbeContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(
                e =>
                {
                    e.ToTable("RenamedProbes");
                    e.Property(p => p.Name).HasMaxLength(50);
                });
    }

    private sealed class FilteredContext : ProbeContext
    {
        public FilteredContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Probe>().HasQueryFilter(p => p.Name != "hidden");
        }
    }

    private sealed class GuidKeyContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public GuidKeyContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<GuidKeyProbe> Probes => Set<GuidKeyProbe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class GuidKeyProbe
    {
        public Guid Id { get; set; }
    }
}
