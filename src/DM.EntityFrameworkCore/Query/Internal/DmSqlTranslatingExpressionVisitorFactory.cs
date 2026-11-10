using Microsoft.EntityFrameworkCore.Query;

namespace DM.EntityFrameworkCore.Query.Internal;

internal sealed class DmSqlTranslatingExpressionVisitorFactory : IRelationalSqlTranslatingExpressionVisitorFactory
{
    private readonly RelationalSqlTranslatingExpressionVisitorDependencies _dependencies;

    public DmSqlTranslatingExpressionVisitorFactory(
        RelationalSqlTranslatingExpressionVisitorDependencies dependencies)
        => _dependencies = dependencies;

    public RelationalSqlTranslatingExpressionVisitor Create(
        QueryCompilationContext queryCompilationContext,
        QueryableMethodTranslatingExpressionVisitor subqueryVisitor)
        => new DmSqlTranslatingExpressionVisitor(_dependencies, queryCompilationContext, subqueryVisitor);
}
