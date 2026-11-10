using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Generates SELECT statements.
/// </summary>
/// <remarks>
/// ponytail: the relational base generator is used for everything except two Dameng shapes:
/// string concatenation, which is <c>||</c> rather than <c>+</c>, and <c>EXTRACT</c>, whose
/// first argument is a keyword rather than an expression. Dameng's paging shapes and its
/// function set are settled against a live server in roadmap phase P2.
/// </remarks>
internal sealed class DmQuerySqlGenerator : QuerySqlGenerator
{
    private const string ExtractFunction = "EXTRACT";

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

    /// <summary>
    /// Renders <c>EXTRACT(PART FROM value)</c>. The base generator separates arguments with a
    /// comma, which is not valid for this function.
    /// </summary>
    protected override Expression VisitSqlFunction(SqlFunctionExpression functionExpression)
    {
        if (functionExpression.Name == ExtractFunction
            && functionExpression.Arguments is [SqlFragmentExpression part, SqlExpression value])
        {
            Visit(new SqlFragmentExpression("EXTRACT("));
            Visit(part);
            Visit(new SqlFragmentExpression(" FROM "));
            Visit(value);
            Visit(new SqlFragmentExpression(")"));

            return functionExpression;
        }

        return base.VisitSqlFunction(functionExpression);
    }
}
