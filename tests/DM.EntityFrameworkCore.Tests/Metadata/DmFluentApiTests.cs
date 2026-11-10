using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DM.EntityFrameworkCore.Tests.Metadata;

/// <summary>
/// T2 tests for the fluent configuration surface. They assert what a model written for the Dameng
/// provider compiles against, and that every setting reaches the generated DDL rather than only the
/// model.
/// </summary>
/// <remarks>
/// Each scenario has its own context type, because EF Core caches the model per context type.
/// </remarks>
public class DmFluentApiTests
{
    [Fact]
    public void UseIdentityColumn_ReachesTheDdl()
    {
        using var context = new SeededIdentityContext(new FakeDmConnection { RowsAffected = 1 });

        Assert.Contains("IDENTITY(5,2) NOT NULL", context.Database.GenerateCreateScript(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The generic overload is the one a strongly typed property builder resolves to, so it has to
    /// produce the same model as the untyped one.
    /// </summary>
    [Fact]
    public void UseIdentityColumnOnATypedBuilder_ReachesTheDdl()
    {
        using var context = new TypedIdentityContext(new FakeDmConnection { RowsAffected = 1 });
        var property = context.Model.FindEntityType(typeof(Probe))!.FindProperty(nameof(Probe.Id))!;

        Assert.Equal(DmValueGenerationStrategy.IdentityColumn, property.GetDmValueGenerationStrategy());
        Assert.Equal(7, property.GetDmIdentitySeed());
        Assert.Equal(3, property.GetDmIdentityIncrement());
        Assert.Contains("IDENTITY(7,3) NOT NULL", context.Database.GenerateCreateScript(), StringComparison.Ordinal);
    }

    [Fact]
    public void UseIdentityColumn_WithAZeroIncrement_IsRejected()
    {
        using var context = new ZeroIncrementContext(new FakeDmConnection { RowsAffected = 1 });

        var exception = Assert.ThrowsAny<Exception>(() => context.Model);

        Assert.Contains("increment", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UseHiLo_NamesTheSequenceAndAddsItToTheModel()
    {
        using var context = new CustomSequenceContext(new FakeDmConnection { RowsAffected = 1 });
        var property = context.Model.FindEntityType(typeof(Probe))!.FindProperty(nameof(Probe.Id))!;

        Assert.Equal(DmValueGenerationStrategy.SequenceHiLo, property.GetDmValueGenerationStrategy());

        var sequence = context.Model.FindSequence("ProbeSequence");

        Assert.NotNull(sequence);
        Assert.Equal(10, sequence.IncrementBy);
    }

    /// <summary>
    /// The model default applies to a property that asks for a generated value, so a model only has
    /// to switch the strategy once instead of on every key.
    /// </summary>
    [Fact]
    public void UseIdentityColumns_AppliesToEveryGeneratedKey()
    {
        using var context = new ModelIdentityContext(new FakeDmConnection { RowsAffected = 1 });
        var property = context.Model.FindEntityType(typeof(Probe))!.FindProperty(nameof(Probe.Id))!;

        Assert.Equal(DmValueGenerationStrategy.IdentityColumn, property.GetDmValueGenerationStrategy());
        Assert.Contains("IDENTITY(3,4) NOT NULL", context.Database.GenerateCreateScript(), StringComparison.Ordinal);
    }

    [Fact]
    public void UseHiLoOnTheModel_AppliesToEveryGeneratedKey()
    {
        using var context = new ModelHiLoContext(new FakeDmConnection { RowsAffected = 1 });
        var property = context.Model.FindEntityType(typeof(Probe))!.FindProperty(nameof(Probe.Id))!;

        Assert.Equal(DmValueGenerationStrategy.SequenceHiLo, property.GetDmValueGenerationStrategy());
        Assert.NotNull(context.Model.FindSequence(DmModelExtensions.DefaultHiLoSequenceName));
    }

    /// <summary>
    /// A property that does not ask for a generated value keeps none, whatever the model default is:
    /// switching the model to hi-lo must not turn an assigned key into a generated one.
    /// </summary>
    [Fact]
    public void ModelDefault_LeavesAnAssignedKeyAlone()
    {
        using var context = new ModelHiLoContext(new FakeDmConnection { RowsAffected = 1 });
        var property = context.Model.FindEntityType(typeof(AssignedProbe))!.FindProperty(nameof(AssignedProbe.Id))!;

        Assert.Null(property.GetDmValueGenerationStrategy());
    }

    private abstract class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        protected ProbeContext(FakeDmConnection connection) => _connection = connection;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(e => e.Property(p => p.Name).HasMaxLength(50));
    }

    private sealed class SeededIdentityContext : ProbeContext
    {
        public SeededIdentityContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Probe>(e => e.Property(p => p.Id).UseIdentityColumn(5, 2));
        }
    }

    private sealed class TypedIdentityContext : ProbeContext
    {
        public TypedIdentityContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Probe>(e => e.Property(p => p.Id).UseIdentityColumn(7, 3));
        }
    }

    private sealed class ZeroIncrementContext : ProbeContext
    {
        public ZeroIncrementContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Probe>(e => e.Property(p => p.Id).UseIdentityColumn(increment: 0));
        }
    }

    private sealed class CustomSequenceContext : ProbeContext
    {
        public CustomSequenceContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Probe>(e => e.Property(p => p.Id).UseHiLo("ProbeSequence"));
        }
    }

    private sealed class ModelIdentityContext : ProbeContext
    {
        public ModelIdentityContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseIdentityColumns(3, 4);
        }
    }

    private sealed class ModelHiLoContext : ProbeContext
    {
        public ModelHiLoContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseHiLo();

            modelBuilder.Entity<AssignedProbe>(
                e =>
                {
                    e.Property(p => p.Id).ValueGeneratedNever();
                    e.Property(p => p.Name).HasMaxLength(50);
                });
        }
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class AssignedProbe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
