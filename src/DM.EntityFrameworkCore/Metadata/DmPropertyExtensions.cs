using DM.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Fluent and metadata access to the Dameng value generation strategy annotation.
/// </summary>
/// <remarks>
/// Declared in <see cref="Microsoft.EntityFrameworkCore" /> rather than in a provider namespace, so
/// that a model written for the Dameng provider keeps compiling against this one.
/// </remarks>
public static class DmPropertyExtensions
{
    /// <summary>
    /// The annotation that carries <see cref="DmValueGenerationStrategy" />.
    /// </summary>
    public const string ValueGenerationStrategyAnnotation = "Dm:ValueGenerationStrategy";

    /// <summary>
    /// Returns the strategy set on this property, or <see langword="null" /> when none was set.
    /// A property with no annotation takes the strategy the model's conventions choose.
    /// </summary>
    public static DmValueGenerationStrategy? GetDmValueGenerationStrategy(this IReadOnlyProperty property)
        => (DmValueGenerationStrategy?)property.FindAnnotation(ValueGenerationStrategyAnnotation)?.Value;

    /// <summary>
    /// Sets the strategy, or clears it when <paramref name="value" /> is <see langword="null" />.
    /// </summary>
    public static void SetDmValueGenerationStrategy(this IMutableProperty property, DmValueGenerationStrategy? value)
        => property.SetOrRemoveAnnotation(ValueGenerationStrategyAnnotation, value);

    /// <summary>
    /// Sets the strategy from a convention, reporting the configuration source the value came from.
    /// </summary>
    public static DmValueGenerationStrategy? SetDmValueGenerationStrategy(
        this IConventionProperty property,
        DmValueGenerationStrategy? value,
        bool fromDataAnnotation = false)
        => (DmValueGenerationStrategy?)property.SetOrRemoveAnnotation(
            ValueGenerationStrategyAnnotation,
            value,
            fromDataAnnotation)?.Value;

    /// <summary>
    /// Returns the sequence this property takes hi-lo blocks from, or <see langword="null" /> when
    /// the property names none and the model's default applies.
    /// </summary>
    public static string? GetDmHiLoSequenceName(this IReadOnlyProperty property)
        => (string?)property.FindAnnotation(DmAnnotationNames.HiLoSequenceName)?.Value;

    /// <summary>
    /// Sets the sequence this property takes hi-lo blocks from.
    /// </summary>
    public static void SetDmHiLoSequenceName(this IMutableProperty property, string? name)
        => property.SetOrRemoveAnnotation(DmAnnotationNames.HiLoSequenceName, name);

    /// <summary>
    /// Returns the schema of the sequence this property takes hi-lo blocks from, or
    /// <see langword="null" /> when the connection's default schema applies.
    /// </summary>
    public static string? GetDmHiLoSequenceSchema(this IReadOnlyProperty property)
        => (string?)property.FindAnnotation(DmAnnotationNames.HiLoSequenceSchema)?.Value;

    /// <summary>
    /// Sets the schema of the sequence this property takes hi-lo blocks from.
    /// </summary>
    public static void SetDmHiLoSequenceSchema(this IMutableProperty property, string? schema)
        => property.SetOrRemoveAnnotation(DmAnnotationNames.HiLoSequenceSchema, schema);

    /// <summary>
    /// Returns the first value this property's identity column produces, falling back to the
    /// model's default.
    /// </summary>
    public static int GetDmIdentitySeed(this IReadOnlyProperty property)
        => (int?)property.FindAnnotation(DmAnnotationNames.IdentitySeed)?.Value
            ?? property.DeclaringType.Model.GetIdentitySeed();

    /// <summary>
    /// Sets the first value this property's identity column produces.
    /// </summary>
    public static void SetDmIdentitySeed(this IMutableProperty property, int? seed)
        => property.SetOrRemoveAnnotation(DmAnnotationNames.IdentitySeed, seed);

    /// <summary>
    /// Returns the step between the values this property's identity column produces, falling back
    /// to the model's default.
    /// </summary>
    public static int GetDmIdentityIncrement(this IReadOnlyProperty property)
        => (int?)property.FindAnnotation(DmAnnotationNames.IdentityIncrement)?.Value
            ?? property.DeclaringType.Model.GetIdentityIncrement();

    /// <summary>
    /// Sets the step between the values this property's identity column produces.
    /// </summary>
    public static void SetDmIdentityIncrement(this IMutableProperty property, int? increment)
        => property.SetOrRemoveAnnotation(DmAnnotationNames.IdentityIncrement, increment);
}
