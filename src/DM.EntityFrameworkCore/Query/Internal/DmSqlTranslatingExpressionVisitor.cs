using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

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

    /// <summary>
    /// <see cref="Math.Max" />, <see cref="Math.Min" /> and the <c>EF.Functions</c> equivalents
    /// never reach a method call translator: the relational visitor resolves them through these
    /// two hooks, so a provider that does not override them silently cannot translate them.
    /// </summary>
    public override SqlExpression? GenerateGreatest(IReadOnlyList<SqlExpression> expressions, Type resultType)
        => TwoArgumentFunction("GREATEST", expressions, resultType);

    public override SqlExpression? GenerateLeast(IReadOnlyList<SqlExpression> expressions, Type resultType)
        => TwoArgumentFunction("LEAST", expressions, resultType);

    private SqlExpression? TwoArgumentFunction(string name, IReadOnlyList<SqlExpression> expressions, Type resultType)
        => expressions.Count == 0
            ? null
            : Dependencies.SqlExpressionFactory.Function(
                name,
                expressions,
                nullable: true,
                argumentsPropagateNullability: expressions.Select(_ => true).ToArray(),
                resultType,
                typeMapping: null);
}
