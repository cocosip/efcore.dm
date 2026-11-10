using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace DM.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Chooses the value generation strategy for keys the store has to fill.
/// </summary>
/// <remarks>
/// An integer key that the model asks the store to generate becomes an identity column, which is
/// what Dameng does by default and what a model written for the Dameng provider already relies on.
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

    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var property in modelBuilder.Metadata.GetEntityTypes().SelectMany(e => e.GetDeclaredProperties()))
        {
            var strategy = property.GetDmValueGenerationStrategy();

            if (strategy is DmValueGenerationStrategy.SequenceHiLo or DmValueGenerationStrategy.Sequence)
            {
                throw new NotSupportedException(
                    $"The value generation strategy {strategy} is declared for "
                    + $"'{property.DeclaringType.DisplayName()}.{property.Name}' but this provider does not implement it yet. "
                    + $"Use {nameof(DmValueGenerationStrategy)}.{nameof(DmValueGenerationStrategy.IdentityColumn)} or "
                    + $"{nameof(DmValueGenerationStrategy)}.{nameof(DmValueGenerationStrategy.None)}.");
            }

            if (strategy is null && IsIdentityCandidate(property))
            {
                property.SetDmValueGenerationStrategy(DmValueGenerationStrategy.IdentityColumn);
            }
        }
    }

    /// <summary>
    /// A column can only be an identity column if the store is the one producing the value, the
    /// type is one Dameng accepts, and nothing else already supplies a default.
    /// </summary>
    private static bool IsIdentityCandidate(IConventionProperty property)
        => property.ValueGenerated == ValueGenerated.OnAdd
            && property.IsKey()
            && IdentityTypes.Contains(Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType)
            && property.GetDefaultValueSql() is null;
}
