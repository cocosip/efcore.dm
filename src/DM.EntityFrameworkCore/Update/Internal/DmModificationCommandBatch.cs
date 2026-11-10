using Microsoft.EntityFrameworkCore.Update;

namespace DM.EntityFrameworkCore.Update.Internal;

/// <summary>
/// A batch of modification commands executed as one round trip.
/// </summary>
/// <remarks>
/// ponytail: the batch executes commands one statement at a time; multi-statement batching
/// and store-generated readback arrive in roadmap phase P3. The ceiling is throughput, not
/// correctness, because every statement is still executed and its affected row count checked.
/// </remarks>
internal sealed class DmModificationCommandBatch : AffectedCountModificationCommandBatch
{
    public DmModificationCommandBatch(ModificationCommandBatchFactoryDependencies dependencies, int maxBatchSize)
        : base(dependencies, maxBatchSize)
    {
    }
}
