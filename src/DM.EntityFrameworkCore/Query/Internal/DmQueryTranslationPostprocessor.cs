using Microsoft.EntityFrameworkCore.Query;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Post-processes the translated query tree.
/// </summary>
internal sealed class DmQueryTranslationPostprocessor : RelationalQueryTranslationPostprocessor
{
    public DmQueryTranslationPostprocessor(
        QueryTranslationPostprocessorDependencies dependencies,
        RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
        RelationalQueryCompilationContext queryCompilationContext)
        : base(dependencies, relationalDependencies, queryCompilationContext)
    {
    }
}
