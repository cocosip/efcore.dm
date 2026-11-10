using DM.EntityFrameworkCore.Diagnostics.Internal;
using DM.EntityFrameworkCore.Infrastructure;
using DM.EntityFrameworkCore.Storage.Internal;
using DM.EntityFrameworkCore.Update.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the services the Dameng provider contributes to the internal service provider.
/// </summary>
public static class DmServiceCollectionExtensions
{
    public static IServiceCollection AddEntityFrameworkDm(this IServiceCollection serviceCollection)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);

        var builder = new EntityFrameworkRelationalServicesBuilder(serviceCollection)
            .TryAdd<LoggingDefinitions, DmLoggingDefinitions>()
            .TryAdd<IDatabaseProvider, DatabaseProvider<DmOptionsExtension>>()
            .TryAdd<IRelationalTypeMappingSource, DmTypeMappingSource>()
            .TryAdd<ISqlGenerationHelper, DmSqlGenerationHelper>()
            .TryAdd<IRelationalConnection, DmRelationalConnection>()
            .TryAdd<IRelationalTransactionFactory, DmTransactionFactory>()
            .TryAdd<IUpdateSqlGenerator, DmUpdateSqlGenerator>()
            .TryAdd<IModificationCommandBatchFactory, DmModificationCommandBatchFactory>()
            .TryAdd<IRelationalDatabaseCreator, DmDatabaseCreator>();

        builder.TryAddCoreServices();

        return serviceCollection;
    }
}
