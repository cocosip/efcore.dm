using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Fluent configuration of how a property takes its value.
/// </summary>
/// <remarks>
/// Declared in <see cref="Microsoft.EntityFrameworkCore" /> rather than in a provider namespace, so
/// that a model written for the Dameng provider keeps compiling against this one.
/// </remarks>
public static class DmPropertyBuilderExtensions
{
    /// <summary>
    /// Takes the value from hi-lo blocks reserved from a sequence.
    /// </summary>
    /// <param name="propertyBuilder">The builder for the property.</param>
    /// <param name="name">The sequence name, or <see langword="null" /> for the model's default.</param>
    /// <param name="schema">The sequence schema, or <see langword="null" /> for the connection's default.</param>
    public static PropertyBuilder UseHiLo(
        this PropertyBuilder propertyBuilder,
        string? name = null,
        string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(propertyBuilder);

        propertyBuilder.Metadata.SetDmValueGenerationStrategy(DmValueGenerationStrategy.SequenceHiLo);
        propertyBuilder.Metadata.SetDmHiLoSequenceName(name);
        propertyBuilder.Metadata.SetDmHiLoSequenceSchema(schema);

        return propertyBuilder;
    }

    /// <inheritdoc cref="UseHiLo(PropertyBuilder, string?, string?)" />
    public static PropertyBuilder<TProperty> UseHiLo<TProperty>(
        this PropertyBuilder<TProperty> propertyBuilder,
        string? name = null,
        string? schema = null)
        => (PropertyBuilder<TProperty>)UseHiLo((PropertyBuilder)propertyBuilder, name, schema);

    /// <summary>
    /// Takes the value from an identity column.
    /// </summary>
    /// <param name="propertyBuilder">The builder for the property.</param>
    /// <param name="seed">The first value the column produces.</param>
    /// <param name="increment">The step between the values the column produces.</param>
    public static PropertyBuilder UseIdentityColumn(
        this PropertyBuilder propertyBuilder,
        int seed = 1,
        int increment = 1)
    {
        ArgumentNullException.ThrowIfNull(propertyBuilder);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(increment);

        propertyBuilder.Metadata.SetDmValueGenerationStrategy(DmValueGenerationStrategy.IdentityColumn);
        propertyBuilder.Metadata.SetDmIdentitySeed(seed);
        propertyBuilder.Metadata.SetDmIdentityIncrement(increment);

        return propertyBuilder;
    }

    /// <inheritdoc cref="UseIdentityColumn(PropertyBuilder, int, int)" />
    public static PropertyBuilder<TProperty> UseIdentityColumn<TProperty>(
        this PropertyBuilder<TProperty> propertyBuilder,
        int seed = 1,
        int increment = 1)
        => (PropertyBuilder<TProperty>)UseIdentityColumn((PropertyBuilder)propertyBuilder, seed, increment);
}
