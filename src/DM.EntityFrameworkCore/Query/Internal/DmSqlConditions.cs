using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Tells a condition from a value, and turns one into the other where Dameng needs it.
/// </summary>
/// <remarks>
/// Dameng accepts a condition in a predicate position, not a value: a boolean column used on its own is
/// rejected with <c>查询使用值表达式作为过滤条件</c>, measured on 8.1.4.80, and so is the same column under
/// <c>NOT</c> or beside another as an operand of <c>AND</c> and <c>OR</c>. Comparing it with <c>1</c> is
/// accepted in every one of those positions. EF Core renders the column bare because SQL Server and
/// PostgreSQL accept it, so the provider compares it instead.
/// </remarks>
internal static class DmSqlConditions
{
    /// <summary>
    /// Whether the expression is already a condition, which a predicate position accepts as it is.
    /// </summary>
    public static bool IsCondition(SqlExpression expression)
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
    /// Rewrites a predicate so that every boolean it contains is compared rather than used as a value.
    /// </summary>
    /// <remarks>
    /// A conjunction is followed into both operands and a negation into its operand, because the server
    /// reads those operands as predicates in their own right. Any other boolean expression is left as it
    /// is when it is already a condition, and compared with <c>1</c> when it is a value. The nodes are
    /// built directly, as the date arithmetic translator does: the comparison is with a numeric literal,
    /// which no boolean type mapping would produce.
    /// </remarks>
    public static SqlExpression AsPredicate(SqlExpression expression)
        => expression switch
        {
            SqlBinaryExpression { OperatorType: ExpressionType.AndAlso } binary
                => new SqlBinaryExpression(
                    ExpressionType.AndAlso,
                    AsPredicate(binary.Left),
                    AsPredicate(binary.Right),
                    typeof(bool),
                    typeMapping: null),
            SqlBinaryExpression { OperatorType: ExpressionType.OrElse } binary
                => new SqlBinaryExpression(
                    ExpressionType.OrElse,
                    AsPredicate(binary.Left),
                    AsPredicate(binary.Right),
                    typeof(bool),
                    typeMapping: null),
            SqlUnaryExpression { OperatorType: ExpressionType.Not } unary
                => new SqlUnaryExpression(
                    ExpressionType.Not,
                    AsPredicate(unary.Operand),
                    typeof(bool),
                    typeMapping: null),
            _ => IsCondition(expression) ? expression : Compared(expression)
        };

    /// <summary>
    /// Compares a boolean with <c>1</c>, which is the value a <c>BIT</c> column holds and the form the
    /// server accepts wherever a condition is expected.
    /// </summary>
    private static SqlExpression Compared(SqlExpression expression)
        => new SqlBinaryExpression(
            ExpressionType.Equal,
            expression,
            new SqlFragmentExpression("1"),
            typeof(bool),
            typeMapping: null);
}
