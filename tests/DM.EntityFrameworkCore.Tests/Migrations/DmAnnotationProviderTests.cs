using DM.EntityFrameworkCore.Metadata;
using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace DM.EntityFrameworkCore.Tests.Migrations;

/// <summary>
/// T2 tests for the annotations a generated migration carries. A migration is code, written from the
/// operations the differ produces, so an annotation the differ never sees is one the migration never
/// shows and a replay never reads.
/// </summary>
/// <remarks>
/// Each scenario has its own context type, because EF Core caches the model per context type.
/// </remarks>
public class DmAnnotationProviderTests
{
    [Fact]
    public void Differ_PutsTheStrategyOnTheColumn()
    {
        using var context = new IdentityContext(new FakeDmConnection());

        var createTable = Assert.IsType<CreateTableOperation>(Assert.Single(Differences(context)));
        var column = Assert.Single(createTable.Columns);

        Assert.Equal(
            DmValueGenerationStrategy.IdentityColumn,
            column[DmPropertyExtensions.ValueGenerationStrategyAnnotation]);
    }

    /// <summary>
    /// The seed and the increment are part of the attribute, so a migration that replays them has to
    /// carry them too. A default is left out, which keeps the generated migration readable.
    /// </summary>
    [Fact]
    public void Differ_LeavesTheDefaultsOut()
    {
        using var context = new IdentityContext(new FakeDmConnection());

        var createTable = Assert.IsType<CreateTableOperation>(Assert.Single(Differences(context)));

        Assert.Null(createTable.Columns[0][DmAnnotationNames.IdentitySeed]);
        Assert.Null(createTable.Columns[0][DmAnnotationNames.IdentityIncrement]);
    }

    [Fact]
    public void Differ_PutsANonDefaultSeedAndIncrementOnTheColumn()
    {
        using var context = new SeedContext(new FakeDmConnection());

        var createTable = Assert.IsType<CreateTableOperation>(Assert.Single(Differences(context)));
        var column = Assert.Single(createTable.Columns);

        Assert.Equal(100, column[DmAnnotationNames.IdentitySeed]);
        Assert.Equal(5, column[DmAnnotationNames.IdentityIncrement]);
    }

    /// <summary>
    /// The derived tables of a table per type hierarchy repeat a key their base table generates, so the
    /// column of a derived table carries no identity attribute of its own.
    /// </summary>
    [Fact]
    public void Differ_LeavesTheMirroredKeyOfADerivedTablePlain()
    {
        using var context = new TptContext(new FakeDmConnection());

        var columns = Differences(context)
            .OfType<CreateTableOperation>()
            .ToDictionary(table => table.Name, table => table.Columns[0]);

        Assert.Equal(
            DmValueGenerationStrategy.IdentityColumn,
            columns["Animals"][DmPropertyExtensions.ValueGenerationStrategyAnnotation]);

        Assert.Null(columns["Cats"][DmPropertyExtensions.ValueGenerationStrategyAnnotation]);
    }

    /// <summary>
    /// The statement generator reads the annotation the differ wrote, so a migration that carries only
    /// the model state it was written from still creates the attribute.
    /// </summary>
    [Fact]
    public void StatementGenerator_ReadsTheAnnotation()
    {
        using var context = new IdentityContext(new FakeDmConnection());
        var operations = Differences(context);

        var sql = context.GetService<IMigrationsSqlGenerator>().Generate(operations, null);

        Assert.Contains("IDENTITY(1,1)", Assert.Single(sql).CommandText, StringComparison.Ordinal);
    }

    private static IReadOnlyList<MigrationOperation> Differences(DbContext context)
    {
        var differ = context.GetService<IMigrationsModelDiffer>();
        var model = context.GetService<IDesignTimeModel>().Model;

        return differ.GetDifferences(null, model.GetRelationalModel());
    }

    private abstract class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        protected ProbeContext(FakeDmConnection connection) => _connection = connection;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);
    }

    private sealed class IdentityContext : ProbeContext
    {
        public IdentityContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(
                e =>
                {
                    e.ToTable("Probes");
                    e.Property(p => p.Id).UseIdentityColumn();
                });
    }

    private sealed class SeedContext : ProbeContext
    {
        public SeedContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(
                e =>
                {
                    e.ToTable("Probes");
                    e.Property(p => p.Id).UseIdentityColumn(100, 5);
                });
    }

    private sealed class TptContext : ProbeContext
    {
        public TptContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        public DbSet<Animal> Animals => Set<Animal>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Animal>(
                e =>
                {
                    e.ToTable("Animals");
                    e.UseTptMappingStrategy();
                });
            modelBuilder.Entity<Cat>().ToTable("Cats");
        }
    }

    private sealed class Probe
    {
        public int Id { get; set; }
    }

    private abstract class Animal
    {
        public int Id { get; set; }
    }

    private sealed class Cat : Animal
    {
    }
}
