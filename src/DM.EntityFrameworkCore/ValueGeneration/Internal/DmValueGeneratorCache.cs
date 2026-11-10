using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace DM.EntityFrameworkCore.ValueGeneration.Internal;

/// <summary>
/// Caches the generator state, so that a block reserved from the database is shared by every
/// context in the service provider instead of being reserved again per context.
/// </summary>
internal sealed class DmValueGeneratorCache : ValueGeneratorCache
{
    private readonly ConcurrentDictionary<(string Name, string? Schema, long IncrementBy), DmSequenceValueGeneratorState> _sequenceStates = new();

    public DmValueGeneratorCache(ValueGeneratorCacheDependencies dependencies)
        : base(dependencies)
    {
    }

    public DmSequenceValueGeneratorState GetOrAddSequenceState(ISequence sequence)
        => _sequenceStates.GetOrAdd(
            (sequence.Name, sequence.Schema, sequence.IncrementBy),
            static (_, current) => new DmSequenceValueGeneratorState(current),
            sequence);
}
