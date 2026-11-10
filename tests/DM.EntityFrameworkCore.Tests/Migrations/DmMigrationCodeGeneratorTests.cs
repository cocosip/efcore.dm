using DM.EntityFrameworkCore.Design.Internal;
using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.Tests.Migrations;

/// <summary>
/// T2 tests for the migration file the tooling writes. The snapshot is what the next migration reads;
/// this file is what a deployment compiles, so an annotation the file cannot render is a build error in
/// the application rather than a provider defect the application can report.
/// </summary>
public class DmMigrationCodeGeneratorTests
{
    /// <summary>
    /// The identity attribute is written as a member of the provider enum, so the file needs the
    /// namespace that enum lives in. A file without it does not compile.
    /// </summary>
    [Fact]
    public void MigrationFile_ImportsTheNamespaceOfTheStrategy()
    {
        var file = MigrationFile(new CodeContext());

        Assert.Contains("using Microsoft.EntityFrameworkCore.Metadata;", file, StringComparison.Ordinal);
        Assert.Contains(
            ".Annotation(\"Dm:ValueGenerationStrategy\", DmValueGenerationStrategy.IdentityColumn)",
            file,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A seed and increment the model names travel with the column, so a replay of the migration asks
    /// the server for the same key the model does.
    /// </summary>
    [Fact]
    public void MigrationFile_CarriesTheExplicitSeedAndIncrement()
    {
        var file = MigrationFile(new CodeContext());

        Assert.Contains(".Annotation(\"Dm:IdentitySeed\", 100)", file, StringComparison.Ordinal);
        Assert.Contains(".Annotation(\"Dm:IdentityIncrement\", 5)", file, StringComparison.Ordinal);
    }

    /// <summary>
    /// The column reaches the file with the store type, the length and the nullability the model asked
    /// for, and the index with its uniqueness.
    /// </summary>
    [Fact]
    public void MigrationFile_DescribesTheColumnAndTheIndex()
    {
        var file = MigrationFile(new CodeContext());

        Assert.Contains("name: \"PROBES\"", file, StringComparison.Ordinal);
        Assert.Contains("type: \"VARCHAR(20)\", maxLength: 20, nullable: false", file, StringComparison.Ordinal);
        Assert.Contains("unique: true", file, StringComparison.Ordinal);
    }

    private static string MigrationFile(DbContext context)
    {
        var model = context.GetService<IDesignTimeModel>().Model;
        var upOperations = context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(null, model.GetRelationalModel());

        var services = new ServiceCollection();
        services.AddEntityFrameworkDesignTimeServices();
        new DmDesignTimeServices().ConfigureDesignTimeServices(services);
        services.AddDbContextDesignTimeServices(context);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IMigrationsCodeGenerator>()
            .GenerateMigration("DM.EntityFrameworkCore.Tests.Migrations", "Initial", upOperations, [])
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class CodeContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(new FakeDmConnection { RowsAffected = 1 });

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CodeProbe>(entity =>
            {
                entity.ToTable("PROBES");
                entity.Property(probe => probe.Id).UseIdentityColumn(100, 5);
                entity.Property(probe => probe.Code).HasMaxLength(20);
                entity.HasIndex(probe => probe.Code).IsUnique();
            });
    }

    private sealed class CodeProbe
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;
    }
}
