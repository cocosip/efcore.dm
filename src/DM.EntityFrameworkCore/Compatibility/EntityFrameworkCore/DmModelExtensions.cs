using DM.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// The sequence a hi-lo property takes its blocks from, the identity defaults, and the defaults
/// used when the model names none.
/// </summary>
/// <remarks>
/// Declared in <see cref="Microsoft.EntityFrameworkCore" /> rather than in a provider namespace, so
/// that a model written for the Dameng provider keeps compiling against this one.
/// </remarks>
public static class DmModelExtensions
{
    /// <summary>
    /// The sequence name used when neither the property nor the model names one.
    /// </summary>
    public const string DefaultHiLoSequenceName = "EntityFrameworkHiLoSequence";

    /// <summary>
    /// The first value an identity column produces when the model sets no seed.
    /// </summary>
    public const int DefaultIdentitySeed = 1;

    /// <summary>
    /// The step between the values an identity column produces when the model sets no increment.
    /// </summary>
    public const int DefaultIdentityIncrement = 1;

    /// <summary>
    /// Returns the hi-lo sequence name the model sets, or the default name when it sets none.
    /// </summary>
    public static string GetHiLoSequenceName(this IReadOnlyModel model)
        => (string?)model.FindAnnotation(DmAnnotationNames.HiLoSequenceName)?.Value ?? DefaultHiLoSequenceName;

    /// <summary>
    /// Sets the hi-lo sequence every property without its own uses.
    /// </summary>
    public static void SetHiLoSequenceName(this IMutableModel model, string? name)
        => model.SetOrRemoveAnnotation(DmAnnotationNames.HiLoSequenceName, name);

    /// <summary>
    /// Returns the schema of the hi-lo sequence, or <see langword="null" /> when the model leaves it
    /// to the connection's default schema.
    /// </summary>
    public static string? GetHiLoSequenceSchema(this IReadOnlyModel model)
        => (string?)model.FindAnnotation(DmAnnotationNames.HiLoSequenceSchema)?.Value;

    /// <summary>
    /// Sets the schema of the hi-lo sequence every property without its own uses.
    /// </summary>
    public static void SetHiLoSequenceSchema(this IMutableModel model, string? schema)
        => model.SetOrRemoveAnnotation(DmAnnotationNames.HiLoSequenceSchema, schema);

    /// <summary>
    /// Returns the strategy the model applies to a property that names none, or
    /// <see langword="null" /> when the model does not choose one.
    /// </summary>
    public static DmValueGenerationStrategy? GetDmValueGenerationStrategy(this IReadOnlyModel model)
        => (DmValueGenerationStrategy?)model.FindAnnotation(DmPropertyExtensions.ValueGenerationStrategyAnnotation)?.Value;

    /// <summary>
    /// Sets the strategy the model applies to a property that names none.
    /// </summary>
    public static void SetDmValueGenerationStrategy(this IMutableModel model, DmValueGenerationStrategy? value)
        => model.SetOrRemoveAnnotation(DmPropertyExtensions.ValueGenerationStrategyAnnotation, value);

    /// <summary>
    /// Returns the first value an identity column produces.
    /// </summary>
    public static int GetIdentitySeed(this IReadOnlyModel model)
        => (int?)model.FindAnnotation(DmAnnotationNames.IdentitySeed)?.Value ?? DefaultIdentitySeed;

    /// <summary>
    /// Sets the first value an identity column produces.
    /// </summary>
    public static void SetIdentitySeed(this IMutableModel model, int? seed)
        => model.SetOrRemoveAnnotation(DmAnnotationNames.IdentitySeed, seed);

    /// <summary>
    /// Returns the step between the values an identity column produces.
    /// </summary>
    public static int GetIdentityIncrement(this IReadOnlyModel model)
        => (int?)model.FindAnnotation(DmAnnotationNames.IdentityIncrement)?.Value ?? DefaultIdentityIncrement;

    /// <summary>
    /// Sets the step between the values an identity column produces.
    /// </summary>
    public static void SetIdentityIncrement(this IMutableModel model, int? increment)
        => model.SetOrRemoveAnnotation(DmAnnotationNames.IdentityIncrement, increment);
}
