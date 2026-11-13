using System.Linq.Expressions;
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
    /// The length of a byte array is a byte count, and the framework hands it over as
    /// <c>ArrayLength</c> rather than as a member a translator sees.
    /// </summary>
    /// <remarks>
    /// <c>LENGTHB</c> rather than <c>LENGTH</c>, measured on 8.1.4.80: <c>LENGTH</c> of a
    /// <c>VARBINARY(64)</c> holding four bytes answered 8, the length of the hexadecimal form the server
    /// stores, and the byte count only of a <c>BLOB</c>, while <c>LENGTHB</c> answered 4 for both.
    /// </remarks>
    protected override Expression VisitUnary(UnaryExpression unaryExpression)
        => unaryExpression.NodeType == ExpressionType.ArrayLength
            && unaryExpression.Operand.Type == typeof(byte[])
            && Visit(unaryExpression.Operand) is SqlExpression operand
                ? Dependencies.SqlExpressionFactory.Function(
                    "LENGTHB",
                    [operand],
                    nullable: true,
                    argumentsPropagateNullability: [true],
                    typeof(int),
                    typeMapping: null)
                : base.VisitUnary(unaryExpression);

    /// <summary>
    /// A boolean exclusive-or keeps the framework's <c>^</c> operator, which the server rejects
    /// (<c>语法分析出错</c> at the operator, measured on 8.1.4.80). Two boolean values differ exactly when
    /// one or the other is true, so the operator becomes a comparison, which the generator writes as the
    /// <c>CASE</c> form of its operands.
    /// </summary>
    /// <remarks>
    /// A difference of two dates is answered by the client. Dameng answers <c>date - date</c> with
    /// the number of days, which the framework would then read back with a <see cref="TimeSpan" />
    /// mapping and fail to materialize ("No coercion operator is defined"); leaving the operator
    /// untranslated projects the date column and subtracts the values in memory, as the reference
    /// provider does.
    /// </remarks>
    protected override Expression VisitBinary(BinaryExpression binaryExpression)
    {
        if (binaryExpression.NodeType == ExpressionType.ExclusiveOr
            && binaryExpression.Left.Type == typeof(bool)
            && binaryExpression.Right.Type == typeof(bool)
            && Visit(binaryExpression.Left) is SqlExpression xorLeft
            && Visit(binaryExpression.Right) is SqlExpression xorRight)
        {
            return Dependencies.SqlExpressionFactory.NotEqual(xorLeft, xorRight);
        }

        return binaryExpression.NodeType == ExpressionType.Subtract
            && IsDate(binaryExpression.Left.Type)
            && IsDate(binaryExpression.Right.Type)
                ? binaryExpression
                : base.VisitBinary(binaryExpression);
    }

    private static bool IsDate(Type type)
        => type == typeof(DateTime) || type == typeof(DateTime?);

}
