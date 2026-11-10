using Microsoft.EntityFrameworkCore.Query;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates CLR expressions inside a query into SQL expressions.
/// </summary>
internal sealed class DmSqlTranslatingExpressionVisitor : RelationalSqlTranslatingExpressionVisitor
{
    public DmSqlTranslatingExpressionVisitor(
        RelationalSqlTranslatingExpressionVisitorDependencies dependencies,
        QueryCompilationContext queryCompilationContext,
        QueryableMethodTranslatingExpressionVisitor subqueryVisitor)
        : base(dependencies, queryCompilationContext, subqueryVisitor)
    {
    }
}
