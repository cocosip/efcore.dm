using Microsoft.EntityFrameworkCore.Query;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates the LINQ method chain into relational query roots.
/// </summary>
internal sealed class DmQueryableMethodTranslatingExpressionVisitor : RelationalQueryableMethodTranslatingExpressionVisitor
{
    public DmQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
        RelationalQueryCompilationContext queryCompilationContext)
        : base(dependencies, relationalDependencies, queryCompilationContext)
    {
    }
}
