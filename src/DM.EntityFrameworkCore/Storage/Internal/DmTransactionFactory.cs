using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Creates <see cref="RelationalTransaction" /> instances. The base implementation is
/// sufficient for Dameng; the type exists so that the provider owns the service slot.
/// </summary>
internal sealed class DmTransactionFactory : RelationalTransactionFactory
{
    public DmTransactionFactory(RelationalTransactionFactoryDependencies dependencies)
        : base(dependencies)
    {
    }
}
