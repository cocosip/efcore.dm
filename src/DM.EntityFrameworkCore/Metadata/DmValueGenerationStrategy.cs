namespace Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// How the provider generates a value for a property that the store has to produce.
/// </summary>
/// <remarks>
/// The member order and the numeric values match the Dameng provider so that an existing model
/// keeps its annotations. <see cref="SequenceHiLo" /> and <see cref="Sequence" /> are declared for
/// that reason only: the model rejects them until the sequence machinery exists, rather than
/// accepting them and generating a key the database never made.
/// </remarks>
public enum DmValueGenerationStrategy
{
    /// <summary>No provider-specific strategy.</summary>
    None = 0,

    /// <summary>A hi-lo block taken from a database sequence. Not implemented yet.</summary>
    SequenceHiLo = 1,

    /// <summary>A column the database fills from its own identity counter.</summary>
    IdentityColumn = 2,

    /// <summary>A value taken directly from a sequence. Not implemented yet.</summary>
    Sequence = 3
}
