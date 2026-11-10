using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Fluent configuration of how the model generates values.
/// </summary>
/// <remarks>
/// Declared in <see cref="Microsoft.EntityFrameworkCore" /> rather than in a provider namespace, so
/// that a model written for the Dameng provider keeps compiling against this one.
/// </remarks>
public static class DmModelBuilderExtensions
{
    /// <summary>
    /// Makes every generated value come from hi-lo blocks reserved from a sequence.
    /// </summary>
    /// <param name="modelBuilder">The builder for the model.</param>
    /// <param name="name">The sequence name, or <see langword="null" /> for the default name.</param>
    /// <param name="schema">The sequence schema, or <see langword="null" /> for the connection's default.</param>
    public static ModelBuilder UseHiLo(
        this ModelBuilder modelBuilder,
        string? name = null,
        string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Model.SetDmValueGenerationStrategy(DmValueGenerationStrategy.SequenceHiLo);
        modelBuilder.Model.SetHiLoSequenceName(name);
        modelBuilder.Model.SetHiLoSequenceSchema(schema);

        return modelBuilder;
    }

    /// <summary>
    /// Makes every generated value come from an identity column.
    /// </summary>
    /// <param name="modelBuilder">The builder for the model.</param>
    /// <param name="seed">The first value the columns produce.</param>
    /// <param name="increment">The step between the values the columns produce.</param>
    public static ModelBuilder UseIdentityColumns(
        this ModelBuilder modelBuilder,
        int seed = 1,
        int increment = 1)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(increment);

        modelBuilder.Model.SetDmValueGenerationStrategy(DmValueGenerationStrategy.IdentityColumn);
        modelBuilder.Model.SetIdentitySeed(seed);
        modelBuilder.Model.SetIdentityIncrement(increment);

        return modelBuilder;
    }
}
