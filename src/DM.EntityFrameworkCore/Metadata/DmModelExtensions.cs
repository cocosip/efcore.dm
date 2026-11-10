using DM.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// The sequence a hi-lo property takes its blocks from, and the defaults used when the model does
/// not name one.
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
    /// Returns the hi-lo sequence name the model sets, or the default name when it sets none.
    /// </summary>
    public static string GetHiLoSequenceName(this IReadOnlyModel model)
        => (string?)model.FindAnnotation(DmAnnotationNames.HiLoSequenceName)?.Value ?? DefaultHiLoSequenceName;

    /// <summary>
    /// Returns the schema of the hi-lo sequence, or <see langword="null" /> when the model leaves it
    /// to the connection's default schema.
    /// </summary>
    public static string? GetHiLoSequenceSchema(this IReadOnlyModel model)
        => (string?)model.FindAnnotation(DmAnnotationNames.HiLoSequenceSchema)?.Value;
}
