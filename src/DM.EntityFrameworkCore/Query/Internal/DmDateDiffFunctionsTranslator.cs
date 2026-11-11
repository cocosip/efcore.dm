using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Dm.Extensions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates the date difference methods of <see cref="DmDbFunctionsExtensions" /> to Dameng's
/// <c>DATEDIFF(datepart, date1, date2)</c>, which returns the boundaries of the named part crossed
/// between its two arguments.
/// </summary>
internal sealed class DmDateDiffFunctionsTranslator : IMethodCallTranslator
{
    private static readonly Dictionary<MethodInfo, string> DateParts = BuildDateParts();

    private static readonly Dictionary<MethodInfo, double> IntervalUnits = BuildIntervalUnits();

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmDateDiffFunctionsTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (IntervalUnits.TryGetValue(method, out var secondsPerUnit))
        {
            return IntervalDifference(arguments[1], arguments[2], secondsPerUnit);
        }

        if (!DateParts.TryGetValue(method, out var datePart))
        {
            return null;
        }

        // The two dates are given a type mapping before the call is built: one of them can be a
        // parameter, and the server needs the type of both sides of the difference.
        return _sqlExpressionFactory.Function(
            "DATEDIFF",
            [
                _sqlExpressionFactory.Fragment(datePart),
                _sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[1])!,
                _sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[2])!
            ],
            nullable: true,
            argumentsPropagateNullability: [false, true, true],
            typeof(int),
            typeMapping: null);
    }

    /// <summary>
    /// The difference of two intervals, which the server will not compute for us: `DATEDIFF` refuses an
    /// interval where it takes a date (`参数不兼容`, measured on 8.1.4.80 for every unit).
    /// </summary>
    /// <remarks>
    /// The elapsed time is read from the interval the subtraction produces and then reduced to the unit with
    /// a truncation, because `CAST(2.9 AS INT)` rounds to 3 on this server. `TRUNC` truncates toward zero,
    /// which is what the in-memory form does, and it was measured to answer 2, 2 and 1 for 2.9, -2.9 and
    /// -1.5, so a query and a client side call agree on the sign as well.
    /// <para>
    /// ponytail: the seconds are real arithmetic, so a span that lands exactly on a unit boundary is truncated
    /// through a floating point value. The in-memory form divides the same way, and a schema that stores such
    /// spans has bigger questions to answer than the last digit.
    /// </para>
    /// </remarks>
    private SqlExpression IntervalDifference(SqlExpression start, SqlExpression end, double secondsPerUnit)
    {
        // Each interval is read for its seconds and the numbers are subtracted, rather than subtracting
        // the intervals: an interval parameter is bound as a time, which the server rejects in interval
        // arithmetic (`数据类型不匹配`), while reading its parts is accepted.
        var seconds = _sqlExpressionFactory.Subtract(
            DmIntervals.Seconds(_sqlExpressionFactory, _sqlExpressionFactory.ApplyDefaultTypeMapping(end)!),
            DmIntervals.Seconds(_sqlExpressionFactory, _sqlExpressionFactory.ApplyDefaultTypeMapping(start)!));

        var scaled = secondsPerUnit == 1.0
            ? seconds
            : secondsPerUnit < 1.0
                ? _sqlExpressionFactory.Multiply(seconds, _sqlExpressionFactory.Constant(1.0 / secondsPerUnit))
                : _sqlExpressionFactory.Divide(seconds, _sqlExpressionFactory.Constant(secondsPerUnit));

        var truncated = _sqlExpressionFactory.Function(
            "TRUNC",
            [scaled],
            nullable: true,
            argumentsPropagateNullability: [true],
            typeof(double),
            typeMapping: null);

        return _sqlExpressionFactory.ApplyDefaultTypeMapping(
            _sqlExpressionFactory.Convert(truncated, typeof(int)))!;
    }

    /// <summary>
    /// The seconds one unit of each overload that takes two intervals stands for. Only the units an interval
    /// can be reduced to are here: a month or a year of a duration is not a number of seconds.
    /// </summary>
    private static Dictionary<MethodInfo, double> BuildIntervalUnits()
    {
        (string Method, double SecondsPerUnit)[] mapping =
        [
            (nameof(DmDbFunctionsExtensions.DateDiffHour), 3600.0),
            (nameof(DmDbFunctionsExtensions.DateDiffMinute), 60.0),
            (nameof(DmDbFunctionsExtensions.DateDiffSecond), 1.0),
            (nameof(DmDbFunctionsExtensions.DateDiffMillisecond), 0.001)
        ];

        var units = new Dictionary<MethodInfo, double>();

        foreach (var (method, secondsPerUnit) in mapping)
        {
            foreach (var overload in typeof(DmDbFunctionsExtensions).GetMethods().Where(m => m.Name == method))
            {
                var dateType = overload.GetParameters()[1].ParameterType;

                if ((Nullable.GetUnderlyingType(dateType) ?? dateType) == typeof(TimeSpan))
                {
                    units[overload] = secondsPerUnit;
                }
            }
        }

        return units;
    }

    private static Dictionary<MethodInfo, string> BuildDateParts()
    {
        (string Method, string DatePart)[] mapping =
        [
            (nameof(DmDbFunctionsExtensions.DateDiffYear), "YEAR"),
            (nameof(DmDbFunctionsExtensions.DateDiffMonth), "MONTH"),
            (nameof(DmDbFunctionsExtensions.DateDiffDay), "DAY"),
            (nameof(DmDbFunctionsExtensions.DateDiffHour), "HOUR"),
            (nameof(DmDbFunctionsExtensions.DateDiffMinute), "MINUTE"),
            (nameof(DmDbFunctionsExtensions.DateDiffSecond), "SECOND"),
            (nameof(DmDbFunctionsExtensions.DateDiffMillisecond), "MILLISECOND"),
            (nameof(DmDbFunctionsExtensions.DateDiffMicrosecond), "MICROSECOND")
        ];

        var dateParts = new Dictionary<MethodInfo, string>();

        // Every overload of a unit differs only in the CLR type of its arguments, so the mapping is
        // resolved by name. The overloads that take an interval are handled apart from these, because the
        // server refuses an interval where `DATEDIFF` takes a date (`参数不兼容`, measured on 8.1.4.80 for
        // every unit), so their difference is computed from the interval instead.
        foreach (var (method, datePart) in mapping)
        {
            foreach (var overload in typeof(DmDbFunctionsExtensions).GetMethods().Where(m => m.Name == method))
            {
                var dateType = overload.GetParameters()[1].ParameterType;

                if ((Nullable.GetUnderlyingType(dateType) ?? dateType) == typeof(TimeSpan))
                {
                    continue;
                }

                dateParts[overload] = datePart;
            }
        }

        return dateParts;
    }
}
