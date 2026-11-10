using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace DM.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Chooses the value generation strategy for a property the store has to fill.
/// </summary>
/// <remarks>
/// A strategy the model or the property names is used as given. Otherwise an integer key the model
/// asks the store to generate becomes an identity column, which is what Dameng does by default and
/// what a model written for the Dameng provider already relies on.
/// <para>
/// A strategy the provider cannot honour fails the model rather than being ignored: a silently
/// dropped strategy produces an insert that never reads the generated key back.
/// </para>
/// </remarks>
internal sealed class DmValueGenerationStrategyConvention : IModelFinalizingConvention
{
    /// <summary>
    /// The types a Dameng identity column accepts.
    /// </summary>
    private static readonly HashSet<Type> IdentityTypes = [typeof(byte), typeof(sbyte), typeof(short), typeof(int), typeof(long)];

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

            if (strategy is DmValueGenerationStrategy.Sequence)
            {
                throw new NotSupportedException(
                    $"The value generation strategy {strategy} is declared for "
                    + $"'{property.DeclaringType.DisplayName()}.{property.Name}' but this provider does not implement it yet. "
                    + $"Use {nameof(DmValueGenerationStrategy)}.{nameof(DmValueGenerationStrategy.SequenceHiLo)}, "
                    + $"{nameof(DmValueGenerationStrategy)}.{nameof(DmValueGenerationStrategy.IdentityColumn)} or "
                    + $"{nameof(DmValueGenerationStrategy)}.{nameof(DmValueGenerationStrategy.None)}.");
            }

            if (strategy is DmValueGenerationStrategy.SequenceHiLo)
            {
                EnsureHiLoSequence(modelBuilder, property);
            }
        }
    }

    /// <summary>
    /// The strategy for a property that names none: the model's default when the store generates the
    /// value, and an identity column for an integer key when the model has no default.
    /// </summary>
    private static DmValueGenerationStrategy? StrategyFor(IConventionProperty property, DmValueGenerationStrategy? modelDefault)
    {
        if (!IsGenerated(property))
        {
            return null;
        }

        if (modelDefault is not null)
        {
            return modelDefault;
        }

        return property.IsKey() ? DmValueGenerationStrategy.IdentityColumn : null;
    }

    /// <summary>
    /// A column can only carry a strategy if the store is the one producing the value, the type is
    /// one Dameng accepts, and nothing else already supplies a default.
    /// </summary>
    private static bool IsGenerated(IConventionProperty property)
        => property.ValueGenerated == ValueGenerated.OnAdd
            && IdentityTypes.Contains(Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType)
            && property.GetDefaultValueSql() is null;

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
