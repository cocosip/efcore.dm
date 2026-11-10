using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace DM.EntityFrameworkCore.ValueGeneration.Internal;

/// <summary>
/// The thread safe state shared by every value a hi-lo property generates from one sequence.
/// </summary>
/// <remarks>
/// The block size is the sequence's own increment, so a fetch that returns <c>n</c> reserves the
/// values <c>n + 1</c> through <c>n + increment</c> and the next fetch starts at
/// <c>n + increment</c>. The sequence therefore has to be declared with the increment the model
/// wants, which is why the convention creates the default sequence with a block size instead of
/// leaving the increment at one.
/// </remarks>
internal sealed class DmSequenceValueGeneratorState : HiLoValueGeneratorState
{
    public DmSequenceValueGeneratorState(ISequence sequence)
        : base(BlockSize(sequence))
        => Sequence = sequence;

    public ISequence Sequence { get; }

    private static int BlockSize(ISequence sequence)
    {
        if (sequence.IncrementBy <= 0 || sequence.IncrementBy > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"The sequence '{sequence.Schema}.{sequence.Name}' is used for hi-lo value generation but its "
                + $"increment is {sequence.IncrementBy}. A hi-lo sequence has to increment by the block size, which "
                + "is a positive number of values.");
        }

        return (int)sequence.IncrementBy;
    }
}
