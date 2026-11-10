using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Generates SELECT statements.
/// </summary>
/// <remarks>
/// ponytail: the relational base generator is used for everything except string concatenation,
/// which Dameng spells <c>||</c>. Dameng's paging shapes and its function set are settled
/// against a live server in roadmap phase P2.
/// </remarks>
internal sealed class DmQuerySqlGenerator : QuerySqlGenerator
{
    public DmQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }

    /// <summary>
    /// Dameng concatenates strings with <c>||</c>, not with <c>+</c>, which for strings is
    /// arithmetic. Emitting the wrong operator produces a query that parses and then fails.
    /// </summary>
    protected override string GetOperator(SqlBinaryExpression binaryExpression)
        => binaryExpression.OperatorType == ExpressionType.Add && binaryExpression.Type == typeof(string)
            ? " || "
            : base.GetOperator(binaryExpression);
}
