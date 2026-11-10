using Microsoft.EntityFrameworkCore.Update;

namespace DM.EntityFrameworkCore.Update.Internal;

internal sealed class DmModificationCommandBatchFactory : IModificationCommandBatchFactory
{
    private readonly ModificationCommandBatchFactoryDependencies _dependencies;

    public DmModificationCommandBatchFactory(ModificationCommandBatchFactoryDependencies dependencies)
        => _dependencies = dependencies;

    public ModificationCommandBatch Create()
    {
        // One statement per round trip: the affected row count comes from the driver's return
        // value, which cannot be attributed to individual commands when several share a batch.
        return new DmModificationCommandBatch(_dependencies, maxBatchSize: 1);
    }
}
