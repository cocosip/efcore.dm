using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Generates SELECT statements.
/// </summary>
/// <remarks>
/// ponytail: the relational base generator is used for everything except three Dameng shapes:
/// string concatenation, which is <c>||</c> rather than <c>+</c>; <c>EXTRACT</c> and
/// <c>CAST</c>, whose second token is a keyword rather than a comma-separated argument. Dameng's
/// paging shapes and its full function set are settled against a live server in roadmap phase P2.
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
    /// Renders <c>EXTRACT(PART FROM value)</c> and <c>CAST(value AS type)</c>. The base generator
    /// separates arguments with a comma, which is not valid for either.
    /// </summary>
    protected override Expression VisitSqlFunction(SqlFunctionExpression functionExpression)
    {
        switch (functionExpression.Name, functionExpression.Arguments)
        {
            case (ExtractFunction, [SqlFragmentExpression part, SqlExpression value]):
                return Render("EXTRACT(", part, " FROM ", value);

            case (DmConvertTranslator.CastFunction, [SqlExpression operand, SqlFragmentExpression storeType]):
                return Render("CAST(", operand, " AS ", storeType);
        }

        return base.VisitSqlFunction(functionExpression);

        Expression Render(string open, SqlExpression first, string separator, SqlExpression second)
        {
            Visit(new SqlFragmentExpression(open));
            Visit(first);
            Visit(new SqlFragmentExpression(separator));
            Visit(second);
            Visit(new SqlFragmentExpression(")"));

            return functionExpression;
        }
    }
}
