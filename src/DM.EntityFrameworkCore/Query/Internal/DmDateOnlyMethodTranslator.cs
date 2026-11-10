using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates the <see cref="DateOnly" /> conversions and members to Dameng SQL.
/// </summary>
/// <remarks>
/// A date is stored as <c>DATE</c>, which in Dameng keeps a time part, so building one from a timestamp
/// has to truncate as well as cast. Adding days is arithmetic on a date, which Dameng expresses as a
/// number of days.
/// </remarks>
internal sealed class DmDateOnlyMethodTranslator : IMethodCallTranslator
{
    private static readonly MethodInfo FromDateTimeMethod = typeof(DateOnly).GetRuntimeMethod(
        nameof(DateOnly.FromDateTime), [typeof(DateTime)])!;

    private static readonly MethodInfo AddDaysMethod = typeof(DateOnly).GetRuntimeMethod(
        nameof(DateOnly.AddDays), [typeof(int)])!;

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    private readonly RelationalTypeMapping _dateMapping;

    public DmDateOnlyMethodTranslator(
        ISqlExpressionFactory sqlExpressionFactory,
        IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _dateMapping = typeMappingSource.FindMapping(typeof(DateOnly))!;
    }

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method == FromDateTimeMethod)
        {
            return _sqlExpressionFactory.Convert(
                _sqlExpressionFactory.Function(
                    "TRUNC",
                    [arguments[0]],
                    nullable: true,
                    argumentsPropagateNullability: [true],
                    typeof(DateTime),
                    arguments[0].TypeMapping),
                typeof(DateOnly),
                _dateMapping);
        }

        if (method == AddDaysMethod && instance is not null)
        {
            // Built directly rather than through the factory: the factory applies the mapping it is given
            // to both operands, which would turn the day count into a date literal. The count keeps its
            // own mapping, which has to be assigned here because nothing above it will supply one.
            return new SqlBinaryExpression(
                ExpressionType.Add,
                instance,
                _sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0])!,
                typeof(DateOnly),
                _dateMapping);
        }

        return null;
    }
}
