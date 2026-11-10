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
/// number of days; adding months and years is <c>ADD_MONTHS</c>, which clamps to the end of the month
/// the way .NET does.
/// </remarks>
internal sealed class DmDateOnlyMethodTranslator : IMethodCallTranslator
{
    private const string AddMonthsFunction = "ADD_MONTHS";

    private static readonly MethodInfo FromDateTimeMethod = typeof(DateOnly).GetRuntimeMethod(
        nameof(DateOnly.FromDateTime), [typeof(DateTime)])!;

    private static readonly MethodInfo AddDaysMethod = typeof(DateOnly).GetRuntimeMethod(
        nameof(DateOnly.AddDays), [typeof(int)])!;

    private static readonly MethodInfo AddMonthsMethod = typeof(DateOnly).GetRuntimeMethod(
        nameof(DateOnly.AddMonths), [typeof(int)])!;

    private static readonly MethodInfo AddYearsMethod = typeof(DateOnly).GetRuntimeMethod(
        nameof(DateOnly.AddYears), [typeof(int)])!;

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

        if (method == AddMonthsMethod && instance is not null)
        {
            return AddMonths(instance, _sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0])!);
        }

        if (method == AddYearsMethod && instance is not null)
        {
            return AddMonths(instance, YearsInMonths(_sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0])!));
        }

        return null;
    }

    private SqlExpression AddMonths(SqlExpression instance, SqlExpression months)
        => _sqlExpressionFactory.Function(
            AddMonthsFunction,
            [instance, months],
            nullable: true,
            argumentsPropagateNullability: [true, true],
            typeof(DateOnly),
            _dateMapping);

    /// <summary>
    /// A literal year count is folded here, so the statement reads <c>ADD_MONTHS(x, 12)</c> rather
    /// than <c>ADD_MONTHS(x, 1 * 12)</c>. A count whose product would wrap is left unfolded, so the
    /// server sees the factor instead of a wrapped number.
    /// </summary>
    private SqlExpression YearsInMonths(SqlExpression years)
        => years is SqlConstantExpression { Value: int value } && value is >= int.MinValue / 12 and <= int.MaxValue / 12
            ? _sqlExpressionFactory.Constant(value * 12, years.TypeMapping)
            : _sqlExpressionFactory.Multiply(years, _sqlExpressionFactory.Constant(12, years.TypeMapping));
}
