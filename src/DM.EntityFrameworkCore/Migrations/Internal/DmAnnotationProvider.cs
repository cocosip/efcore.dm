using DM.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DM.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// Adds the provider annotations a generated migration has to carry.
/// </summary>
/// <remarks>
/// A migration is code, and the code is written from the operations the differ produces. An annotation
/// the differ never sees is an annotation the generated migration never writes, which leaves the reader
/// of that migration unable to tell an identity column from a plain one. The value is written per
/// column, because where a value is generated is a property of the table and type mapping, not of the
/// property alone: the derived tables of a table per type hierarchy repeat a key their base table
/// generates.
/// </remarks>
internal sealed class DmAnnotationProvider : RelationalAnnotationProvider
{
    private const int DefaultIdentitySeed = 1;
    private const int DefaultIdentityIncrement = 1;

    public DmAnnotationProvider(RelationalAnnotationProviderDependencies dependencies)
        : base(dependencies)
    {
    }

    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        foreach (var annotation in base.For(column, designTime))
        {
            yield return annotation;
        }

        if (!designTime || IdentityProperty(column) is not { } property)
        {
            yield break;
        }

        yield return new Annotation(
            DmPropertyExtensions.ValueGenerationStrategyAnnotation,
            DmValueGenerationStrategy.IdentityColumn);

        var seed = property.GetDmIdentitySeed();

        if (seed != DefaultIdentitySeed)
        {
            yield return new Annotation(DmAnnotationNames.IdentitySeed, seed);
        }

        var increment = property.GetDmIdentityIncrement();

        if (increment != DefaultIdentityIncrement)
        {
            yield return new Annotation(DmAnnotationNames.IdentityIncrement, increment);
        }
    }

    /// <summary>
    /// The property a column generates a value for, or null. A table that only mirrors the row above
    /// it, which is what a table-per-type hierarchy produces, repeats a value the base table
    /// generated, so the rule depends on the table and not only on the property.
    /// </summary>
    internal static IProperty? IdentityProperty(IColumn column)
    {
        if (column.PropertyMappings.FirstOrDefault()?.Property is not { } property)
        {
            return null;
        }

        var table = StoreObjectIdentifier.Table(column.Table.Name, column.Table.Schema);

        return property.GetDmValueGenerationStrategy(table) == DmValueGenerationStrategy.IdentityColumn
            ? property
            : null;
    }
}
