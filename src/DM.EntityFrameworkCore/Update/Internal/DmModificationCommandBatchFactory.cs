using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace DM.EntityFrameworkCore.Update.Internal;

internal sealed class DmModificationCommandBatchFactory : IModificationCommandBatchFactory
{
    private readonly ModificationCommandBatchFactoryDependencies _dependencies;
    private readonly IDbContextOptions _options;

    public DmModificationCommandBatchFactory(
        ModificationCommandBatchFactoryDependencies dependencies,
        IDbContextOptions options)
    {
        _dependencies = dependencies;
        _options = options;
    }

    public ModificationCommandBatch Create()
    {
        var maxBatchSize = _options.FindExtension<RelationalOptionsExtension>()?.MaxBatchSize ?? 42;

        return new DmModificationCommandBatch(_dependencies, maxBatchSize);
    }
}
