using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.Tests.Design;

/// <summary>
/// Pins what the compiled model tool writes for this provider. A compiled model carries the store
/// model as annotations, so an annotation the generator drops is a capability the optimized model
/// loses and the ordinary model keeps.
/// </summary>
public class DmCompiledModelTests
{
    [Fact]
    public void GenerateModel_KeepsTheProviderAnnotations()
    {
        var code = GenerateModelCode();

        Assert.Contains(
            """
            .AddAnnotation("Dm:ValueGenerationStrategy", DmValueGenerationStrategy.SequenceHiLo)
            """,
            code);

        Assert.Contains("""AddAnnotation("Dm:HiLoSequenceName", "SampleSequence")""", code);
        Assert.Contains("""AddAnnotation("Dm:HiLoSequenceSchema", "app")""", code);
    }

    [Fact]
    public void GenerateModel_KeepsTheHiLoSequenceItReadsBlocksFrom()
    {
        // The block size is the sequence increment, so the compiled model has to carry the sequence,
        // not only the property annotation.
        var code = GenerateModelCode();

        Assert.Contains("""("SampleSequence", "app")""", code);
        Assert.Contains("incrementBy: 10", code);
    }

    private static string GenerateModelCode()
    {
        using var services = ProviderDesignTimeServices.Build();
        var generator = services.GetRequiredService<ICompiledModelCodeGenerator>();

        using var context = new SampleContext(new FakeDmConnection());
        var model = context.GetService<IDesignTimeModel>().Model;

        var files = generator.GenerateModel(
            model,
            new CompiledModelCodeGenerationOptions
            {
                ModelNamespace = "Sample.Models",
                ContextType = typeof(SampleContext),
                UseNullableReferenceTypes = true
            });

        return string.Join("\n", files.Select(file => file.Code));
    }

    private sealed class SampleContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public SampleContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<Sample> Samples => Set<Sample>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Sample>(
                entity =>
                {
                    entity.Property(sample => sample.Id)
                        .ValueGeneratedOnAdd()
                        .UseHiLo("SampleSequence", "app");

                    entity.Property(sample => sample.Name).HasMaxLength(50);
                });
    }

    private sealed class Sample
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
