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
}
