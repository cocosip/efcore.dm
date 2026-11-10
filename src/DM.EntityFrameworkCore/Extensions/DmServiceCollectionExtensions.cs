using DM.EntityFrameworkCore.Diagnostics.Internal;
using DM.EntityFrameworkCore.Infrastructure;
using DM.EntityFrameworkCore.Metadata.Conventions;
using DM.EntityFrameworkCore.Migrations.Internal;
using DM.EntityFrameworkCore.Query.Internal;
using DM.EntityFrameworkCore.Storage.Internal;
using DM.EntityFrameworkCore.Update.Internal;
using DM.EntityFrameworkCore.ValueGeneration.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.ValueGeneration;
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
            .TryAdd<IRelationalDatabaseCreator, DmDatabaseCreator>()
            .TryAdd<IMigrationsSqlGenerator, DmMigrationsSqlGenerator>()
            .TryAdd<IHistoryRepository, DmHistoryRepository>()
            .TryAdd<IQuerySqlGeneratorFactory, DmQuerySqlGeneratorFactory>()
            .TryAdd<IQueryableMethodTranslatingExpressionVisitorFactory, DmQueryableMethodTranslatingExpressionVisitorFactory>()
            .TryAdd<IRelationalSqlTranslatingExpressionVisitorFactory, DmSqlTranslatingExpressionVisitorFactory>()
            .TryAdd<IMemberTranslatorProvider, DmMemberTranslatorProvider>()
            .TryAdd<IMethodCallTranslatorProvider, DmMethodCallTranslatorProvider>()
            .TryAdd<IQueryTranslationPostprocessorFactory, DmQueryTranslationPostprocessorFactory>()
            .TryAdd<IRelationalParameterBasedSqlProcessorFactory, DmParameterBasedSqlProcessorFactory>()
            .TryAdd<IProviderConventionSetBuilder, DmConventionSetBuilder>()
            .TryAdd<IValueGeneratorSelector, DmValueGeneratorSelector>()
            .TryAdd<IValueGeneratorCache, DmValueGeneratorCache>();

        builder.TryAddCoreServices();

        return serviceCollection;
    }
}
