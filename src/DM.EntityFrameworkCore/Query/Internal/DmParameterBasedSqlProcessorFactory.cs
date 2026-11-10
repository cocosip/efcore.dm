using Microsoft.EntityFrameworkCore.Query;

namespace DM.EntityFrameworkCore.Query.Internal;

internal sealed class DmParameterBasedSqlProcessorFactory : IRelationalParameterBasedSqlProcessorFactory
{
    private readonly RelationalParameterBasedSqlProcessorDependencies _dependencies;

    public DmParameterBasedSqlProcessorFactory(RelationalParameterBasedSqlProcessorDependencies dependencies)
        => _dependencies = dependencies;

    public RelationalParameterBasedSqlProcessor Create(RelationalParameterBasedSqlProcessorParameters parameters)
        => new DmParameterBasedSqlProcessor(_dependencies, parameters);
}
