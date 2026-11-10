using Microsoft.EntityFrameworkCore.Query;

namespace DM.EntityFrameworkCore.Query.Internal;

internal sealed class DmQueryTranslationPostprocessorFactory : IQueryTranslationPostprocessorFactory
{
    private readonly QueryTranslationPostprocessorDependencies _dependencies;
    private readonly RelationalQueryTranslationPostprocessorDependencies _relationalDependencies;

    public DmQueryTranslationPostprocessorFactory(
        QueryTranslationPostprocessorDependencies dependencies,
        RelationalQueryTranslationPostprocessorDependencies relationalDependencies)
    {
        _dependencies = dependencies;
        _relationalDependencies = relationalDependencies;
    }

    public QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
        => new DmQueryTranslationPostprocessor(
            _dependencies,
            _relationalDependencies,
            (RelationalQueryCompilationContext)queryCompilationContext);
}
