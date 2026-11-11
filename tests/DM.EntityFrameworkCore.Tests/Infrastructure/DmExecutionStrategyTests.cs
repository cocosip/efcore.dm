using DM.EntityFrameworkCore.Storage.Internal;
using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// T1 tests for the retrying execution strategy: which failures it classifies as transient, and that the
/// option that installs it is the only thing that does.
/// </summary>
/// <remarks>
/// The classification is the provider's own logic; the retry loop itself belongs to
/// <see cref="ExecutionStrategy" />. A failure that carries a Dameng error number cannot be built in a
/// unit test, because the exception type has no public constructor, so the behavior of both halves is
/// measured on a server by the acceptance run and the numbers are pinned here.
/// </remarks>
public class DmExecutionStrategyTests
{
    /// <summary>
    /// The numbers are measured on 8.1.4.80 and documented on the detector, so a change to the set is a
    /// change to what a retry claims: -608 is the statement timeout while a lock is held, 6027 is a
    /// connection that received nothing.
    /// </summary>
    [Fact]
    public void TheMeasuredFailuresAreTheOnesRetried()
        => Assert.Equal([-608, 6027], DmTransientExceptionDetector.ErrorNumbers);

    /// <summary>
    /// Only a Dameng error number classifies. An exception the provider did not raise describes a failure
    /// the provider cannot classify, and a replay would be the wrong answer to it.
    /// </summary>
    [Fact]
    public void AFailureTheProviderDidNotRaiseIsNotTransient()
    {
        Assert.False(DmTransientExceptionDetector.ShouldRetryOn(new InvalidOperationException("no")));
        Assert.False(DmTransientExceptionDetector.ShouldRetryOn(new TimeoutException("no")));
        Assert.False(
            DmTransientExceptionDetector.ShouldRetryOn(new InvalidOperationException("wrapped", new TimeoutException())));
    }

    [Fact]
    public void EnableRetryOnFailure_InstallsTheRetryingStrategy()
    {
        using var context = new RetryContext(builder => builder.EnableRetryOnFailure(3, TimeSpan.FromSeconds(1)));

        var strategy = context.Database.CreateExecutionStrategy();

        Assert.IsType<DmRetryingExecutionStrategy>(strategy);
        Assert.True(strategy.RetriesOnFailure);
    }

    /// <summary>
    /// A failure the provider classifies as permanent is thrown on the first attempt, which is what makes
    /// the strategy safe to install: a constraint violation is not replayed.
    /// </summary>
    [Fact]
    public void Execute_ThrowsAPermanentFailureOnTheFirstAttempt()
    {
        using var context = new RetryContext(builder => builder.EnableRetryOnFailure(3));
        var attempts = 0;

        Assert.Throws<InvalidOperationException>(
            () => context.Database.CreateExecutionStrategy().Execute(() =>
            {
                attempts++;
                throw new InvalidOperationException("permanent");
            }));

        Assert.Equal(1, attempts);
    }

    [Theory]
    [InlineData(-1)]
    public void EnableRetryOnFailure_RejectsANegativeRetryCount(int maxRetryCount)
    {
        var builder = new DbContextOptionsBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DmDbContextOptionsBuilder(builder).EnableRetryOnFailure(maxRetryCount));
    }

    [Fact]
    public void EnableRetryOnFailure_RejectsANonPositiveDelay()
    {
        var builder = new DbContextOptionsBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DmDbContextOptionsBuilder(builder).EnableRetryOnFailure(maxRetryDelay: TimeSpan.Zero));
    }

    private sealed class RetryContext : DbContext
    {
        private readonly Action<DmDbContextOptionsBuilder> _configure;

        public RetryContext(Action<DmDbContextOptionsBuilder> configure) => _configure = configure;

        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(new FakeDmConnection(), _configure);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(entity => entity.Property(probe => probe.Name).HasMaxLength(50));
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
