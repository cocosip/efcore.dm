using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DM.EntityFrameworkCore.Tests.ValueGeneration;

/// <summary>
/// T2 tests for hi-lo value generation. They run against a recording connection that answers the
/// sequence statement, so they pin both the reservation behaviour and the statement itself without
/// a server.
/// </summary>
/// <remarks>
/// The generator state lives in the internal service provider, which every context in the process
/// shares, so a block reserved by one test is still held when the next one runs. Two consequences
/// shape these tests: the fake sequence hands out ever higher blocks, so values are asserted to be
/// consecutive rather than to start at a given number, and each test uses its own sequence, so the
/// number of round trips it needs is predictable.
/// </remarks>
public class DmHiLoValueGenerationTests
{
    private static long _nextLow = 1_000_000;

    [Fact]
    public void Convention_AddsTheDefaultSequence_WithABlockSizedIncrement()
    {
        using var context = new DefaultSequenceContext(new FakeDmConnection { RowsAffected = 1 });

        var sequence = context.Model.FindSequence(DmModelExtensions.DefaultHiLoSequenceName);

        Assert.NotNull(sequence);
        Assert.Equal(10, sequence.IncrementBy);
    }

    [Fact]
    public void Insert_TakesTheFirstBlockFromTheSequence()
    {
        var connection = Connection(10);

        using var context = new FirstBlockContext(connection);
        var first = new HiLoProbe { Name = "a" };
        var second = new HiLoProbe { Name = "b" };
        context.AddRange(first, second);

        context.SaveChanges();

        Assert.Equal(first.Id + 1, second.Id);
        Assert.Equal(
            $"""SELECT "{FirstBlockContext.SequenceName}".NEXTVAL FROM DUAL""",
            Assert.Single(Fetches(connection)));
    }

    /// <summary>
    /// A hi-lo value is a real key, so the insert has nothing to read back. Reading it would cost a
    /// round trip per row for a value the client already knows.
    /// </summary>
    [Fact]
    public void Insert_DoesNotReadTheKeyBack()
    {
        var connection = Connection(10);

        using var context = new NoReadBackContext(connection);
        context.Add(new HiLoProbe { Name = "a" });

        context.SaveChanges();

        var insert = connection.Commands.Single(c => c.StartsWith("INSERT", StringComparison.Ordinal));

        Assert.DoesNotContain("SELECT", insert, StringComparison.Ordinal);
    }

    /// <summary>
    /// Only the first value of a block costs a round trip: the eleventh entity needs a second block
    /// because the sequence hands out ten values at a time.
    /// </summary>
    [Fact]
    public void Insert_AcrossABlockBoundary_TakesASecondBlock()
    {
        var connection = Connection(10);

        using var context = new ElevenEntitiesContext(connection);
        var probes = Enumerable.Range(1, 11).Select(i => new HiLoProbe { Name = "p" + i }).ToList();
        context.AddRange(probes);

        context.SaveChanges();

        AssertConsecutive(probes);
        Assert.Equal(2, Fetches(connection).Count);
    }

    /// <summary>
    /// The block size is the sequence's own increment, so a model that declares a different
    /// increment reserves a block of that size.
    /// </summary>
    [Fact]
    public void SequenceIncrement_BecomesTheBlockSize()
    {
        var connection = Connection(5);

        using var context = new FiveBlockContext(connection);
        var probes = Enumerable.Range(1, 6).Select(i => new HiLoProbe { Name = "p" + i }).ToList();
        context.AddRange(probes);

        context.SaveChanges();

        AssertConsecutive(probes);
        Assert.Equal(2, Fetches(connection).Count);
    }

    private static FakeDmConnection Connection(int blockSize)
        => new() { RowsAffected = 1, ScalarSource = () => NextBlock(blockSize) };

    private static long NextBlock(int blockSize)
        => Interlocked.Add(ref _nextLow, blockSize) - blockSize;

    private static List<string> Fetches(FakeDmConnection connection)
        => connection.Commands.Where(c => c.Contains("NEXTVAL", StringComparison.Ordinal)).ToList();

    private static void AssertConsecutive(IReadOnlyList<HiLoProbe> probes)
    {
        for (var i = 1; i < probes.Count; i++)
        {
            Assert.Equal(probes[i - 1].Id + 1, probes[i].Id);
        }
    }

    private class DefaultSequenceContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public DefaultSequenceContext(FakeDmConnection connection) => _connection = connection;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HiLoProbe>(
                e =>
                {
                    e.Property(p => p.Name).HasMaxLength(50);
                    e.Property(p => p.Id).Metadata.SetDmValueGenerationStrategy(DmValueGenerationStrategy.SequenceHiLo);
                });
    }

    private abstract class HiLoContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        protected HiLoContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<HiLoProbe> Probes => Set<HiLoProbe>();

        protected abstract string HiLoSequence { get; }

        protected abstract int BlockSize { get; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasSequence(HiLoSequence).IncrementsBy(BlockSize);

            modelBuilder.Entity<HiLoProbe>(
                e =>
                {
                    e.Property(p => p.Name).HasMaxLength(50);
                    e.Property(p => p.Id).Metadata.SetDmValueGenerationStrategy(DmValueGenerationStrategy.SequenceHiLo);
                    e.Property(p => p.Id).Metadata.SetDmHiLoSequenceName(HiLoSequence);
                });
        }
    }

    private sealed class FirstBlockContext : HiLoContext
    {
        public const string SequenceName = "HiLoFirstBlock";

        public FirstBlockContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override string HiLoSequence => SequenceName;

        protected override int BlockSize => 10;
    }

    private sealed class NoReadBackContext : HiLoContext
    {
        public NoReadBackContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override string HiLoSequence => "HiLoNoReadBack";

        protected override int BlockSize => 10;
    }

    private sealed class ElevenEntitiesContext : HiLoContext
    {
        public ElevenEntitiesContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override string HiLoSequence => "HiLoElevenEntities";

        protected override int BlockSize => 10;
    }

    private sealed class FiveBlockContext : HiLoContext
    {
        public FiveBlockContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override string HiLoSequence => "HiLoFiveBlock";

        protected override int BlockSize => 5;
    }

    private sealed class HiLoProbe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
