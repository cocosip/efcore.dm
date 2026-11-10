using DM.EntityFrameworkCore.Design.Internal;
using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.Tests.Migrations;

/// <summary>
/// T2 tests for the migration snapshot. A migration keeps what the snapshot writes, so an annotation
/// missing here is an annotation the next migration cannot see.
/// </summary>
/// <remarks>
/// The snapshot is generated from the design time model, the same model the tooling writes, without
/// a migration project or a server.
/// </remarks>
public class DmSnapshotTests
{
    /// <summary>
    /// The identity attribute is a provider annotation, so the snapshot has to carry it. Without it a
    /// migration added later sees a plain integer column and drops the attribute from the table.
    /// </summary>
    [Fact]
    public void Snapshot_CarriesTheValueGenerationStrategy()
    {
        using var context = new SnapshotContext(new FakeDmConnection { RowsAffected = 1 });

        var snapshot = SnapshotBody(context);

        Assert.Contains("Dm:ValueGenerationStrategy", snapshot, StringComparison.Ordinal);
        Assert.Contains(nameof(DmValueGenerationStrategy.IdentityColumn), snapshot, StringComparison.Ordinal);
    }

    /// <summary>
    /// The strategy is written as a member of the provider enum, so the file that holds it needs the
    /// namespace that enum lives in. A snapshot that is missing it does not compile in the
    /// application, which is where the user would find out.
    /// </summary>
    [Fact]
    public void SnapshotFile_ImportsTheNamespaceOfTheStrategy()
    {
        using var context = new SnapshotContext(new FakeDmConnection { RowsAffected = 1 });

        var file = SnapshotFile(context);

        Assert.Contains("using Microsoft.EntityFrameworkCore.Metadata;", file, StringComparison.Ordinal);
        Assert.Contains("DmValueGenerationStrategy.IdentityColumn", file, StringComparison.Ordinal);
    }

    /// <summary>
    /// The seed and increment are only written when the model names them, so a key that takes the
    /// default does not carry a redundant annotation into every snapshot.
    /// </summary>
    [Fact]
    public void Snapshot_CarriesAnExplicitIdentitySeed()
    {
        using var context = new SeededSnapshotContext(new FakeDmConnection { RowsAffected = 1 });

        var snapshot = SnapshotBody(context);

        Assert.Contains("Dm:IdentitySeed", snapshot, StringComparison.Ordinal);
        Assert.Contains("Dm:IdentityIncrement", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshot_OmitsTheIdentitySeedWhenTheModelNamesNone()
    {
        using var context = new SnapshotContext(new FakeDmConnection { RowsAffected = 1 });

        var snapshot = SnapshotBody(context);

        Assert.DoesNotContain("Dm:IdentitySeed", snapshot, StringComparison.Ordinal);
    }

    /// <summary>
    /// The hi-lo setting decides where a block comes from, so it has to survive the snapshot too.
    /// </summary>
    [Fact]
    public void Snapshot_CarriesTheHiLoSequence()
    {
        using var context = new HiLoSnapshotContext(new FakeDmConnection { RowsAffected = 1 });

        var snapshot = SnapshotBody(context);

        Assert.Contains("Dm:HiLoSequenceName", snapshot, StringComparison.Ordinal);
    }

    private static string SnapshotBody(DbContext context)
    {
        var typeMappingSource = context.GetService<IRelationalTypeMappingSource>();
        var generator = new CSharpSnapshotGenerator(
            new CSharpSnapshotGeneratorDependencies(
                new CSharpHelper(typeMappingSource),
                typeMappingSource,
                new AnnotationCodeGenerator(new AnnotationCodeGeneratorDependencies(typeMappingSource))));

        var builder = new IndentedStringBuilder();
        generator.Generate("modelBuilder", context.GetService<IDesignTimeModel>().Model, builder);

        return builder.ToString();
    }

    /// <summary>
    /// Builds the file the tooling writes, header included, through the design time services the
    /// provider declares.
    /// </summary>
    private static string SnapshotFile(DbContext context)
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkDesignTimeServices();
        new DmDesignTimeServices().ConfigureDesignTimeServices(services);
        services.AddDbContextDesignTimeServices(context);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IMigrationsCodeGenerator>()
            .GenerateSnapshot(
                "DM.EntityFrameworkCore.Tests.Migrations",
                context.GetType(),
                "SnapshotContextModelSnapshot",
                context.GetService<IDesignTimeModel>().Model);
    }

    private abstract class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        protected ProbeContext(FakeDmConnection connection) => _connection = connection;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SnapshotProbe>(e => e.Property(p => p.Name).HasMaxLength(50));
    }

    private sealed class SnapshotContext : ProbeContext
    {
        public SnapshotContext(FakeDmConnection connection)
            : base(connection)
        {
        }
    }

    private sealed class SeededSnapshotContext : ProbeContext
    {
        public SeededSnapshotContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<SnapshotProbe>(e => e.Property(p => p.Id).UseIdentityColumn(100, 5));
        }
    }

    private sealed class HiLoSnapshotContext : ProbeContext
    {
        public HiLoSnapshotContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<SnapshotProbe>(e => e.Property(p => p.Id).UseHiLo("ProbeSequence"));
        }
    }

    private sealed class SnapshotProbe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
