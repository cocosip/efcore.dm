using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    /// Dameng has no boolean type a comparison can hold as an operand: <c>"Flag" = ("Id" &gt; 1)</c> is
    /// rejected with <c>语法分析出错</c>, while the same condition written as a <c>CASE</c> answering
    /// <c>1</c> or <c>0</c> is accepted. The predicate rewrite does not reach this shape, because the
    /// condition is an operand rather than a predicate of the statement.
    /// </summary>
    protected override Expression VisitSqlBinary(SqlBinaryExpression sqlBinaryExpression)
    {
        if (sqlBinaryExpression.OperatorType
                is not (ExpressionType.Equal
                    or ExpressionType.NotEqual
                    or ExpressionType.LessThan
                    or ExpressionType.LessThanOrEqual
                    or ExpressionType.GreaterThan
                    or ExpressionType.GreaterThanOrEqual)
            || (!DmSqlConditions.IsCondition(sqlBinaryExpression.Left)
                && !DmSqlConditions.IsCondition(sqlBinaryExpression.Right)))
        {
            return base.VisitSqlBinary(sqlBinaryExpression);
        }

        WriteOperand(sqlBinaryExpression.Left);
        Sql.Append(GetOperator(sqlBinaryExpression));
        WriteOperand(sqlBinaryExpression.Right);

        return sqlBinaryExpression;

        void WriteOperand(SqlExpression operand)
        {
            if (DmSqlConditions.IsCondition(operand))
            {
                Sql.Append("CASE WHEN ");
                Visit(operand);
                Sql.Append(" THEN 1 ELSE 0 END");

                return;
            }

            var requiresParentheses = RequiresParentheses(sqlBinaryExpression, operand);

            if (requiresParentheses)
            {
                Sql.Append("(");
            }

            Visit(operand);

            if (requiresParentheses)
            {
                Sql.Append(")");
            }
        }
    }

    /// <summary>
    /// Dameng has no <c>VALUES</c> row source. Measured on 8.1.4.80: neither
    /// <c>(VALUES (1), (2)) AS "t" ("x")</c> nor a bare <c>VALUES</c> inside a set operation parses
    /// (<c>语法分析出错</c>), while a <c>SELECT ... FROM DUAL</c> per row joined by <c>UNION ALL</c>
    /// answers. The base class writes the first row that way and the rest as <c>VALUES</c>, which this
    /// override replaces.
    /// </summary>
    protected override void GenerateValues(ValuesExpression valuesExpression)
    {
        var rowValues = valuesExpression.RowValues!;

        if (rowValues.Count == 0)
        {
            throw new InvalidOperationException(RelationalStrings.EmptyCollectionNotSupportedAsInlineQueryRoot);
        }

        for (var row = 0; row < rowValues.Count; row++)
        {
            if (row > 0)
            {
                Sql.Append(" UNION ALL ");
            }

            Sql.Append("SELECT ");

            var values = rowValues[row].Values;

            for (var index = 0; index < values.Count; index++)
            {
                if (index > 0)
                {
                    Sql.Append(", ");
                }

                Visit(values[index]);

                if (row == 0)
                {
                    Sql.Append(AliasSeparator)
                        .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(valuesExpression.ColumnNames[index]));
                }
            }

            Sql.Append(" FROM DUAL");
        }
    }

    /// <summary>
    /// Dameng rejects a value in a join predicate the same way it rejects one in a where clause: a bare
    /// boolean answers <c>查询使用值表达式作为过滤条件</c>. A tautology therefore has to be written as
    /// <c>1 = 1</c>, which the predicate rewrite already does for the statement's own predicate.
    /// </summary>
    protected override Expression VisitInnerJoin(InnerJoinExpression innerJoinExpression)
    {
        Sql.Append("INNER JOIN ");
        Visit(innerJoinExpression.Table);
        Sql.Append(" ON ");
        Visit(DmSqlConditions.AsPredicate(innerJoinExpression.JoinPredicate));

        return innerJoinExpression;
    }

    protected override Expression VisitLeftJoin(LeftJoinExpression leftJoinExpression)
    {
        Sql.Append("LEFT JOIN ");
        Visit(leftJoinExpression.Table);
        Sql.Append(" ON ");
        Visit(DmSqlConditions.AsPredicate(leftJoinExpression.JoinPredicate));

        return leftJoinExpression;
    }

    protected override Expression VisitRightJoin(RightJoinExpression rightJoinExpression)
    {
        Sql.Append("RIGHT JOIN ");
        Visit(rightJoinExpression.Table);
        Sql.Append(" ON ");
        Visit(DmSqlConditions.AsPredicate(rightJoinExpression.JoinPredicate));

        return rightJoinExpression;
    }

    /// <summary>
    /// A <c>CASE</c> result is a value position, and Dameng rejects a condition there the same way it
    /// rejects one in an operand: <c>CASE WHEN "a" IS NULL THEN 1 ELSE "b" &lt; 100 END</c> answered
    /// <c>语法分析出错</c> at the <c>&lt;</c>. A condition therefore becomes the same
    /// <c>CASE WHEN ... THEN 1 ELSE 0 END</c> the operand rewrite emits.
    /// </summary>
    protected override Expression VisitCase(CaseExpression caseExpression)
    {
        Sql.Append("CASE");

        if (caseExpression.Operand != null)
        {
            Sql.Append(" ");
            Visit(caseExpression.Operand);
        }

        foreach (var whenClause in caseExpression.WhenClauses)
        {
            Sql.AppendLine().Append("WHEN ");
            Visit(whenClause.Test);
            Sql.Append(" THEN ");
            WriteResult(whenClause.Result);
        }

        if (caseExpression.ElseResult != null)
        {
            Sql.AppendLine().Append("ELSE ");
            WriteResult(caseExpression.ElseResult);
        }

        Sql.AppendLine().Append("END");

        return caseExpression;

        void WriteResult(SqlExpression result)
        {
            if (DmSqlConditions.IsCondition(result))
            {
                Sql.Append("CASE WHEN ");
                Visit(result);
                Sql.Append(" THEN 1 ELSE 0 END");

                return;
            }

            Visit(result);
        }
    }

    /// <summary>
    /// Rewrites the predicate into the form Dameng accepts: every boolean it contains is compared with
    /// <c>1</c> rather than used as a value, which the server rejects with
    /// <c>查询使用值表达式作为过滤条件</c>. The generator is the place that knows an expression is a
    /// predicate, so the rewrite happens here: on the translated tree a predicate and a projected value
    /// look alike.
    /// </summary>
    protected override Expression VisitSelect(SelectExpression selectExpression)
    {
        var predicate = selectExpression.Predicate is null
            ? null
            : DmSqlConditions.AsPredicate(selectExpression.Predicate);
        var having = selectExpression.Having is null
            ? null
            : DmSqlConditions.AsPredicate(selectExpression.Having);

        return predicate == selectExpression.Predicate && having == selectExpression.Having
            ? base.VisitSelect(selectExpression)
            : base.VisitSelect(
                selectExpression.Update(
                    selectExpression.Tables,
                    predicate,
                    selectExpression.GroupBy,
                    having,
                    selectExpression.Projection,
                    selectExpression.Orderings,
                    selectExpression.Offset,
                    selectExpression.Limit));
    }

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
        if (!DmSqlConditions.IsCondition(projectionExpression.Expression))
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
    /// Reads a member of a JSON document with <c>JSON_VALUE</c>, measured on 8.1.4.80: it answers a scalar
    /// as text, and the <c>RETURNING</c> form that would type it is rejected with <c>返回值的数据类型无效</c>,
    /// so a member that is not text is cast from that text. A boolean is not cast, because the server stores
    /// it as <c>true</c> and casting that text to <c>BIT</c> answers <c>无效的数字</c>; it is compared with
    /// that text and answered as the condition shape the provider uses. <c>JSON_QUERY</c> answers an object
    /// or an array only and returns null for a scalar, so it is not used. The path is a literal, because a
    /// member of a JSON document is named by the model rather than by a value.
    /// </summary>
    protected override Expression VisitJsonScalar(JsonScalarExpression jsonScalarExpression)
    {
        // An empty path is the document itself, which is the column: a projection of the entity reads it
        // that way, and the framework parses the text the column holds.
        if (jsonScalarExpression.Path.Count == 0)
        {
            return Visit(jsonScalarExpression.Json);
        }

        var storeType = jsonScalarExpression.TypeMapping?.StoreType;
        var type = Nullable.GetUnderlyingType(jsonScalarExpression.Type) ?? jsonScalarExpression.Type;
        var path = JsonPath(jsonScalarExpression.Path);

        // A boolean is stored as `true` and answered as that text, so it is not cast: casting it to BIT
        // answers 无效的数字, measured on 8.1.4.80. The provider answers a condition as 1 or 0, which is
        // the same shape a projected condition takes.
        if (type == typeof(bool))
        {
            Sql.Append("CASE WHEN JSON_VALUE(");
            Visit(jsonScalarExpression.Json);
            Sql.Append(", '").Append(path).Append("') = 'true' THEN 1 ELSE 0 END");

            return jsonScalarExpression;
        }

        var cast = storeType is not null && type != typeof(string);

        if (cast)
        {
            Sql.Append("CAST(");
        }

        Sql.Append("JSON_VALUE(");
        Visit(jsonScalarExpression.Json);
        Sql.Append(", '").Append(path).Append("')");

        if (cast)
        {
            Sql.Append(" AS ").Append(storeType!).Append(")");
        }

        return jsonScalarExpression;
    }

    private static string JsonPath(IReadOnlyList<PathSegment> path)
    {
        var builder = new StringBuilder("$");

        foreach (var segment in path)
        {
            if (segment.PropertyName is { } propertyName)
            {
                builder.Append('.').Append(propertyName);
            }
            else if (segment.ArrayIndex is SqlConstantExpression { Value: int index })
            {
                builder.Append('[').Append(index).Append(']');
            }
            else
            {
                throw new NotSupportedException(
                    "A JSON path that names an array element by a value other than a constant cannot be "
                    + "rendered as the literal Dameng requires.");
            }
        }

        return builder.ToString().Replace("'", "''", StringComparison.Ordinal);
    }

    /// <summary>
    /// Dameng requires a FROM clause, so a statement without a table selects from <c>DUAL</c>.
    /// </summary>
    protected override void GeneratePseudoFromClause()
        => Sql.AppendLine().Append("FROM DUAL");


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