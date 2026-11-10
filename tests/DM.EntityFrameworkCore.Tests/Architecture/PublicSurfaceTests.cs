using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Dm.Extensions;

namespace DM.EntityFrameworkCore.Tests.Architecture;

/// <summary>
/// Pins the public surface, which is the source compatibility contract a consumer migrating from the
/// previous provider compiles against. A type that appears or disappears, or a member a consumer calls
/// that is no longer there, is a breaking change rather than a refactor, so the list is explicit.
/// </summary>
public class PublicSurfaceTests
{
    /// <summary>
    /// Every type a consumer can compile against, sorted by full name. Three of them are pipeline
    /// types rather than API: <c>DmOptionsExtension</c> is public because it is the type argument of
    /// the options builder's base class, <c>DmLoggingDefinitions</c> because the logging abstractions
    /// instantiate it, and <c>DmDesignTimeServices</c> because design time tooling discovers it by
    /// name. They live under <c>Internal</c> where the namespace says what they are.
    /// </summary>
    private static readonly string[] ExpectedTypes =
    [
        "DM.EntityFrameworkCore.Design.Internal.DmDesignTimeServices",
        "DM.EntityFrameworkCore.Diagnostics.Internal.DmLoggingDefinitions",
        "DM.EntityFrameworkCore.Infrastructure.DmOptionsExtension",
        "Microsoft.EntityFrameworkCore.Diagnostics.DmEventId",
        "Microsoft.EntityFrameworkCore.Dm.Extensions.DmDbFunctionsExtensions",
        "Microsoft.EntityFrameworkCore.DmDatabaseFacadeExtensions",
        "Microsoft.EntityFrameworkCore.DmDbContextOptionsExtensions",
        "Microsoft.EntityFrameworkCore.DmModelBuilderExtensions",
        "Microsoft.EntityFrameworkCore.DmModelExtensions",
        "Microsoft.EntityFrameworkCore.DmPropertyBuilderExtensions",
        "Microsoft.EntityFrameworkCore.DmPropertyExtensions",
        "Microsoft.EntityFrameworkCore.Infrastructure.DmDbContextOptionsBuilder",
        "Microsoft.EntityFrameworkCore.Metadata.DmValueGenerationStrategy",
        "Microsoft.EntityFrameworkCore.Migrations.DmMigrationBuilderExtensions",
        "Microsoft.EntityFrameworkCore.Migrations.Operations.DmCreateSchemaOperation",
        "Microsoft.EntityFrameworkCore.Migrations.Operations.DmCreateUserOperation",
        "Microsoft.EntityFrameworkCore.Migrations.Operations.DmDropSchemaOperation",
        "Microsoft.EntityFrameworkCore.Migrations.Operations.DmDropUserOperation",
        "Microsoft.Extensions.DependencyInjection.DmServiceCollectionExtensions"
    ];

    /// <summary>
    /// The date difference surface is the one the previous provider publishes, which a consumer calls
    /// as <c>EF.Functions.DateDiffX</c>. Every unit has a plain and a nullable form over a timestamp,
    /// an offset timestamp and an elapsed time, so the count is what proves none was dropped.
    /// </summary>
    private const int DateDiffOverloads = 36;

    private static readonly string[] DateDiffUnits =
    [
        "DateDiffYear",
        "DateDiffMonth",
        "DateDiffDay",
        "DateDiffHour",
        "DateDiffMinute",
        "DateDiffSecond",
        "DateDiffMillisecond"
    ];

    [Fact]
    public void ThePublicSurfaceIsUnchanged()
    {
        var actual = typeof(DmDbContextOptionsExtensions).Assembly
            .GetExportedTypes()
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedTypes, actual);
    }

    [Fact]
    public void TheDateDifferenceOverloadsAreAllPresent()
    {
        var overloads = typeof(DmDbFunctionsExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Count(method => DateDiffUnits.Contains(method.Name, StringComparer.Ordinal)
                && method.GetParameters().Length == 3);

        Assert.Equal(DateDiffOverloads, overloads);
    }
}
