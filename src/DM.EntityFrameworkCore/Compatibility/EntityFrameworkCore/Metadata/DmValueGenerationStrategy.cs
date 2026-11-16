namespace Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// How the provider generates a value for a property that the store has to produce.
/// </summary>
/// <remarks>
/// The member order and the numeric values match the Dameng provider so that an existing model
/// keeps its annotations. <see cref="SequenceHiLo" /> takes blocks from a model sequence, and
/// <see cref="Sequence" /> is the strategy of a key the store fills from a sequence default.
/// </remarks>
public enum DmValueGenerationStrategy
{
    /// <summary>No provider-specific strategy.</summary>
    None = 0,

    /// <summary>A hi-lo block taken from a database sequence.</summary>
    SequenceHiLo = 1,

    /// <summary>A column the database fills from its own identity counter.</summary>
    IdentityColumn = 2,

    /// <summary>A value taken directly from a sequence.</summary>
    Sequence = 3
}
