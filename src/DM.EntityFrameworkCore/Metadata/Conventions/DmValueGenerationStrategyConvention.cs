using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Chooses the value generation strategy for a property the store has to fill.
/// </summary>
/// <remarks>
/// A strategy the model or the property names is used as given. Otherwise an integer key the model
/// asks the store to generate becomes an identity column, which is what Dameng does by default and
/// what a model written for the Dameng provider already relies on.
/// <para>
/// A key that already carries a default expression is not an identity column: the store fills it
/// from that expression, so the strategy records where the value comes from and the insert reads it
/// back instead of writing one.
/// </para>
/// </remarks>
internal sealed class DmValueGenerationStrategyConvention : IModelFinalizingConvention
{
    private readonly ITypeMappingSource _typeMappingSource;

    public DmValueGenerationStrategyConvention(ITypeMappingSource typeMappingSource)
        => _typeMappingSource = typeMappingSource;

    /// <summary>
    /// The types a Dameng identity column accepts.
    /// </summary>
    /// <remarks>
    /// Measured on 8.1.4.80: `IDENTITY` on `INT`, `BIGINT`, `INTEGER`, `NUMBER` and `DECIMAL` was accepted,
    /// while `TINYINT` and `SMALLINT` answered `非法IDENTITY列类型`, so the server's identity column is an
    /// integer of at least 32 bits.
    /// </remarks>
    private static readonly HashSet<Type> IdentityTypes = [typeof(int), typeof(long)];

    /// <summary>
    /// The number of values a default hi-lo sequence hands out per round trip.
    /// </summary>
    private const int DefaultHiLoBlockSize = 10;

    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        var modelDefault = modelBuilder.Metadata.GetDmValueGenerationStrategy();

        foreach (var property in modelBuilder.Metadata.GetEntityTypes().SelectMany(e => e.GetDeclaredProperties()))
        {
            var declared = property.GetDmValueGenerationStrategy();
            var strategy = declared ?? StrategyFor(property, modelDefault);

            if (strategy is null)
            {
                continue;
            }

            if (declared is null)
            {
                property.SetDmValueGenerationStrategy(strategy);
            }

            if (strategy is DmValueGenerationStrategy.SequenceHiLo)
            {
                EnsureHiLoSequence(modelBuilder, property);
            }
        }
    }

    /// <summary>
    /// The strategy for a property that names none: a key the store fills from a default expression
    /// is sequence generated, otherwise the model's default applies, and an integer key with no
    /// default at all becomes an identity column.
    /// </summary>
    private DmValueGenerationStrategy? StrategyFor(IConventionProperty property, DmValueGenerationStrategy? modelDefault)
    {
        if (!IsGenerated(property))
        {
            return null;
        }

        if (property.IsKey() && property.GetDefaultValueSql() is not null)
        {
            return DmValueGenerationStrategy.Sequence;
        }

        return modelDefault ?? (property.IsKey() ? DmValueGenerationStrategy.IdentityColumn : null);
    }

    /// <summary>
    /// Whether Dameng accepts an identity column for this property's column type.
    /// </summary>
    /// <remarks>
    /// The column is what the store generates into, so a converted property is judged by the type its
    /// converter produces: an enum or a wrapped integer whose column is an <c>INT</c> is an identity column,
    /// while an integer key converted onto a character column is not. A converter named by type is only
    /// instantiated by the type mapping source, which is why this reads the mapping rather than the
    /// property's converter annotation. Measured on 8.1.4.80: <c>IDENTITY</c> on a character column answered
    /// <c>非法IDENTITY列类型</c>.
    /// </remarks>
    internal static bool IsIdentityType(IReadOnlyProperty property, ITypeMappingSource typeMappingSource)
        => IsIdentityType(
            typeMappingSource.FindMapping((IProperty)property)?.Converter?.ProviderClrType
            ?? property.GetProviderClrType()
            ?? property.ClrType);

    /// <summary>
    /// Whether Dameng accepts an identity column of this CLR type, which is the type the column holds.
    /// </summary>
    internal static bool IsIdentityType(Type providerClrType)
    {
        var type = Nullable.GetUnderlyingType(providerClrType) ?? providerClrType;

        return IdentityTypes.Contains(type.IsEnum ? Enum.GetUnderlyingType(type) : type);
    }

    /// <summary>
    /// A column can only carry a strategy if the store is the one producing the value and the column's
    /// type is one Dameng accepts.
    /// </summary>
    private bool IsGenerated(IConventionProperty property)
        => property.ValueGenerated == ValueGenerated.OnAdd && IsIdentityType(property, _typeMappingSource);

    /// <summary>
    /// A hi-lo property needs the sequence it takes blocks from to be part of the model, so that a
    /// migration creates it. The default one is added here, with an increment that makes a block
    /// worth a round trip; a sequence the model already declares is left exactly as declared.
    /// </summary>
    private static void EnsureHiLoSequence(IConventionModelBuilder modelBuilder, IConventionProperty property)
    {
        var model = modelBuilder.Metadata;
        var name = property.GetDmHiLoSequenceName() ?? model.GetHiLoSequenceName();
        var schema = property.GetDmHiLoSequenceSchema() ?? model.GetHiLoSequenceSchema();

        if (model.FindSequence(name, schema) is null)
        {
            modelBuilder.HasSequence(name, schema)?.IncrementsBy(DefaultHiLoBlockSize);
        }
    }
}
