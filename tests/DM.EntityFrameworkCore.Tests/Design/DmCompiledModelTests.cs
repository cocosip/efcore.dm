using System.Reflection;
using System.Runtime.Loader;
using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.Tests.Design;

/// <summary>
/// Pins what the compiled model tool writes for this provider, and that the result behaves like the
/// model it was generated from. A compiled model carries the store model as annotations, so an
/// annotation the generator drops is a capability the optimized model loses.
/// </summary>
public class DmCompiledModelTests
{
    [Fact]
    public void GenerateModel_KeepsTheProviderAnnotations()
    {
        var code = Code();

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
        var code = Code();

        Assert.Contains("""("SampleSequence", "app")""", code);
        Assert.Contains("incrementBy: 10", code);
    }

    [Fact]
    public void CompiledModel_ProducesTheSameCreateScriptAsTheOrdinaryModel()
    {
        // The generated source is compiled and loaded here, so this covers the whole capability:
        // what the tool writes, and that a context built from it resolves the same store model.
        var compiled = LoadModel(Compile(Files()));

        using var ordinary = new SampleContext(new FakeDmConnection());
        using var optimized = new SampleContext(new FakeDmConnection(), compiled);

        Assert.Equal(
            ordinary.Database.GenerateCreateScript(),
            optimized.Database.GenerateCreateScript());

        // The query pipeline reads the store model too, so the SQL it produces is the same proof.
        Assert.Equal(
            ordinary.Samples.Where(sample => sample.Name == "x").ToQueryString(),
            optimized.Samples.Where(sample => sample.Name == "x").ToQueryString());
    }

    [Fact]
    public void CompiledModel_CarriesTheProviderMetadata()
    {
        var compiled = LoadModel(Compile(Files()));

        var entity = compiled.FindEntityType("DM.EntityFrameworkCore.Tests.Design.DmCompiledModelTests+Sample")!;
        var key = entity.FindProperty(nameof(Sample.Id))!;

        Assert.Equal(DmValueGenerationStrategy.SequenceHiLo, key.GetDmValueGenerationStrategy());
        Assert.Equal("SampleSequence", key.GetDmHiLoSequenceName());
        Assert.Equal("app", key.GetDmHiLoSequenceSchema());
    }

    private static string Code() => string.Join("\n", Files().Select(file => file.Code));

    private static IReadOnlyCollection<ScaffoldedFile> Files()
    {
        using var services = ProviderDesignTimeServices.Build();
        var generator = services.GetRequiredService<ICompiledModelCodeGenerator>();

        using var context = new SampleContext(new FakeDmConnection());
        var model = context.GetService<IDesignTimeModel>().Model;

        return generator.GenerateModel(
            model,
            new CompiledModelCodeGenerationOptions
            {
                ModelNamespace = ModelNamespace,
                ContextType = typeof(SampleContext),
                UseNullableReferenceTypes = true
            });
    }

    private static Assembly Compile(IReadOnlyCollection<ScaffoldedFile> files)
    {
        var trees = files
            .Select(file => CSharpSyntaxTree.ParseText(file.Code, path: file.Path))
            .ToArray();

        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        var references = platform
            .Concat(Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();

        // Each compilation needs its own name: the load context rejects a second assembly with the
        // name of one it already holds.
        var compilation = CSharpCompilation.Create(
            $"CompiledModelProbe{Interlocked.Increment(ref _compilations)}",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var assembly = new MemoryStream();
        var result = compilation.Emit(assembly);

        Assert.True(
            result.Success,
            "The generated model must compile: " + string.Join(
                "\n",
                result.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => diagnostic.ToString())));

        assembly.Position = 0;

        return AssemblyLoadContext.Default.LoadFromStream(assembly);
    }

    private static IModel LoadModel(Assembly assembly)
    {
        var type = assembly
            .GetTypes()
            .Single(candidate => candidate.Name == "SampleContextModel");

        var instance = type
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);

        var model = instance ?? Activator.CreateInstance(type, nonPublic: true);

        return Assert.IsAssignableFrom<IModel>(model);
    }

    private const string ModelNamespace = "Sample.Models";

    private static int _compilations;

    public sealed class SampleContext : DbContext
    {
        private readonly FakeDmConnection _connection;
        private readonly IModel? _model;

        internal SampleContext(FakeDmConnection connection, IModel? model = null)
        {
            _connection = connection;
            _model = model;
        }

        public DbSet<Sample> Samples => Set<Sample>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseDm(_connection);

            if (_model is not null)
            {
                optionsBuilder.UseModel(_model);
            }
        }

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

    public sealed class Sample
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
