using Microsoft.EntityFrameworkCore.Query;

namespace DM.EntityFrameworkCore.Query.Internal;

internal sealed class DmQuerySqlGeneratorFactory : IQuerySqlGeneratorFactory
{
    private readonly QuerySqlGeneratorDependencies _dependencies;

    public DmQuerySqlGeneratorFactory(QuerySqlGeneratorDependencies dependencies) => _dependencies = dependencies;

    public QuerySqlGenerator Create() => new DmQuerySqlGenerator(_dependencies);
}
