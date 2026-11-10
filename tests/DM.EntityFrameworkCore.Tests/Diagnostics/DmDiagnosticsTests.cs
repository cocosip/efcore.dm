using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Diagnostics;

/// <summary>
/// T1 tests for the provider's own diagnostics. They capture what the provider logs while the model
/// is built, so they pin both the message and the case that raises it.
/// </summary>
/// <remarks>
/// Each scenario has its own context type, because EF Core caches the model per context type.
/// </remarks>
public class DmDiagnosticsTests
{
    [Fact]
    public void DecimalWithoutPrecision_Warns()
    {
        CapturingLoggerFactory.Reset();

        using var context = new DecimalContext(new FakeDmConnection { RowsAffected = 1 });

        _ = context.Model;

        Assert.Contains(
            CapturingLoggerFactory.Messages,
            message => message.Contains("DECIMAL(29,4)", StringComparison.Ordinal)
                && message.Contains(nameof(DecimalProbe.Amount), StringComparison.Ordinal));
    }

    [Fact]
    public void DecimalWithPrecision_DoesNotWarn()
    {
        CapturingLoggerFactory.Reset();

        using var context = new PreciseDecimalContext(new FakeDmConnection { RowsAffected = 1 });

        _ = context.Model;

        Assert.DoesNotContain(
            CapturingLoggerFactory.Messages,
            message => message.Contains("DECIMAL(29,4)", StringComparison.Ordinal));
    }

    /// <summary>
    /// A column type set on the property replaces the provider default, so there is nothing to warn
    /// about.
    /// </summary>
    [Fact]
    public void DecimalWithAColumnType_DoesNotWarn()
    {
        CapturingLoggerFactory.Reset();

        using var context = new ColumnTypeDecimalContext(new FakeDmConnection { RowsAffected = 1 });

        _ = context.Model;

        Assert.DoesNotContain(
            CapturingLoggerFactory.Messages,
            message => message.Contains("DECIMAL(29,4)", StringComparison.Ordinal));
    }

    private abstract class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        protected ProbeContext(FakeDmConnection connection) => _connection = connection;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseDm(_connection)
                .UseLoggerFactory(CapturingLoggerFactory.Instance);
    }

    private sealed class DecimalContext : ProbeContext
    {
        public DecimalContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DecimalProbe>(e => e.Property(p => p.Name).HasMaxLength(50));
    }

    private sealed class PreciseDecimalContext : ProbeContext
    {
        public PreciseDecimalContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DecimalProbe>(
                e =>
                {
                    e.Property(p => p.Name).HasMaxLength(50);
                    e.Property(p => p.Amount).HasPrecision(12, 2);
                });
    }

    private sealed class ColumnTypeDecimalContext : ProbeContext
    {
        public ColumnTypeDecimalContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DecimalProbe>(
                e =>
                {
                    e.Property(p => p.Name).HasMaxLength(50);
                    e.Property(p => p.Amount).HasColumnType("DECIMAL(10,2)");
                });
    }

    private sealed class DecimalProbe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Amount { get; set; }
    }
}
