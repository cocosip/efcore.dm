using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Generates SELECT statements.
/// </summary>
/// <remarks>
/// ponytail: the relational base generator is used for everything except the Dameng shapes below:
/// string concatenation, which is <c>||</c> rather than <c>+</c>; <c>EXTRACT</c> and <c>CAST</c>,
/// whose second token is a keyword rather than a comma-separated argument; and the functions Dameng
/// spells without parentheses, which the base generator would render with an empty pair. Dameng's
/// paging shapes and its full function set are settled against a live server in roadmap phase P2.
/// </remarks>
internal sealed class DmQuerySqlGenerator : QuerySqlGenerator
{
    private const string ExtractFunction = "EXTRACT";

    /// <summary>
    /// Functions Dameng spells without parentheses. Emitting <c>CURRENT_TIMESTAMP()</c> makes the
    /// statement a syntax error.
    /// </summary>
    private static readonly HashSet<string> NiladicFunctions = new(StringComparer.Ordinal)
    {
        "CURRENT_TIMESTAMP",
        "CURRENT_DATE",
        "SYSTIMESTAMP",
        "SYSDATE"
    };

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
    /// Dameng has no boolean type a select list can hold: a condition is a <c>CASE</c> answering
    /// <c>1</c> or <c>0</c>. The base generator renders the condition itself, which the server rejects
    /// with a syntax error — measured on a server with <c>SELECT EXISTS (...)</c>.
    /// </summary>
    /// <remarks>
    /// A column, a parameter or a constant of type <see cref="bool" /> is a value, not a condition, so
    /// it is left alone; only the shapes that express a predicate are wrapped.
    /// </remarks>
    protected override Expression VisitProjection(ProjectionExpression projectionExpression)
    {
        if (!IsCondition(projectionExpression.Expression))
        {
            return base.VisitProjection(projectionExpression);
        }

        Sql.Append("CASE WHEN ");
        Visit(projectionExpression.Expression);
        Sql.Append(" THEN 1 ELSE 0 END");

        if (projectionExpression.Alias != string.Empty)
        {
            Sql.Append(AliasSeparator)
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(projectionExpression.Alias));
        }

        return projectionExpression;
    }

    /// <summary>
    /// Dameng requires a FROM clause, so a statement without a table selects from <c>DUAL</c>.
    /// </summary>
    protected override void GeneratePseudoFromClause()
        => Sql.AppendLine().Append("FROM DUAL");

    private static bool IsCondition(SqlExpression expression)
        => expression.Type == typeof(bool)
            && expression is ExistsExpression
                or InExpression
                or LikeExpression
                or SqlUnaryExpression
                {
                    OperatorType: ExpressionType.Not or ExpressionType.Equal or ExpressionType.NotEqual
                }
                or SqlBinaryExpression
                {
                    OperatorType: ExpressionType.AndAlso
                    or ExpressionType.OrElse
                    or ExpressionType.Equal
                    or ExpressionType.NotEqual
                    or ExpressionType.LessThan
                    or ExpressionType.LessThanOrEqual
                    or ExpressionType.GreaterThan
                    or ExpressionType.GreaterThanOrEqual
                };

    /// <summary>
    /// Renders <c>EXTRACT(PART FROM value)</c> and <c>CAST(value AS type)</c>. The base generator
    /// separates arguments with a comma, which is not valid for either.
    /// </summary>
    protected override Expression VisitSqlFunction(SqlFunctionExpression functionExpression)
    {
        if (functionExpression.Name is { } name
            && functionExpression.Arguments is { Count: 0 }
            && NiladicFunctions.Contains(name))
        {
            Visit(new SqlFragmentExpression(name));

            return functionExpression;
        }

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
