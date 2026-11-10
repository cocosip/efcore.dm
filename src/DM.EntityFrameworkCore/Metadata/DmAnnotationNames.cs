namespace DM.EntityFrameworkCore.Metadata;

/// <summary>
/// The annotation names the provider reads and writes beyond the ones EF Core defines.
/// </summary>
internal static class DmAnnotationNames
{
    /// <summary>The prefix every provider annotation carries.</summary>
    public const string Prefix = "Dm:";

    /// <summary>The sequence a hi-lo property takes its blocks from.</summary>
    public const string HiLoSequenceName = Prefix + "HiLoSequenceName";

    /// <summary>The schema of the sequence a hi-lo property takes its blocks from.</summary>
    public const string HiLoSequenceSchema = Prefix + "HiLoSequenceSchema";
}
