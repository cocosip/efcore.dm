using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates <see cref="DateTime" /> arithmetic to Dameng SQL.
/// </summary>
/// <remarks>
/// Dameng (in Oracle-compatible mode) adds a day count directly to a date, so sub-day units are
/// scaled into a fraction of a day. Month arithmetic uses <c>TIMESTAMPADD(MONTH, n, x)</c>, which
/// clamps to the end of the month the way .NET does.
/// <para>
/// <c>ADD_MONTHS</c> would be the obvious choice and clamps the same way, but on a default-mode
/// instance it answers at day precision: measured on 8.1.4.80, it turned <c>03:04:05</c> into
/// midnight, while both <c>TIMESTAMPADD</c> and interval addition kept the time on the default and
/// the Oracle-compatible instance alike. A timestamp that loses its time is a wrong answer, not a
/// formatting difference, so the statement names the function that keeps it.
/// </para>
/// <para>
/// The <c>+</c> node is built directly rather than through <see cref="ISqlExpressionFactory" />:
/// there is no CLR <c>DateTime + double</c> operator, so the factory would reject it even though
/// the database accepts it. Both operands still have to carry a type mapping, or the generator
/// cannot render them.
/// </para>
/// </remarks>
internal sealed class DmDateTimeMethodTranslator : IMethodCallTranslator
{
    private const string TimestampAddFunction = "TIMESTAMPADD";

    /// <summary>The seconds in a day, which scales a difference of dates to the unit the interval takes.</summary>
    private const double SecondsPerDay = 86400.0;
    private const string MonthArgument = "MONTH";

    private static readonly Dictionary<string, long> DaysPerUnit = new(StringComparer.Ordinal)
    {
        [nameof(DateTime.AddDays)] = 1,
        [nameof(DateTime.AddHours)] = 24,
        [nameof(DateTime.AddMinutes)] = 60 * 24,
        [nameof(DateTime.AddSeconds)] = 60 * 60 * 24,
        [nameof(DateTime.AddMilliseconds)] = 1000 * 60 * 60 * 24,
        [nameof(DateTime.AddMicroseconds)] = 1000L * 1000 * 60 * 60 * 24
    };

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    private readonly RelationalTypeMapping _timeSpanMapping;

    private readonly RelationalTypeMapping _doubleMapping;

    public DmDateTimeMethodTranslator(
        ISqlExpressionFactory sqlExpressionFactory,
        IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _timeSpanMapping = typeMappingSource.FindMapping(typeof(TimeSpan))!;
        _doubleMapping = typeMappingSource.FindMapping(typeof(double))!;
    }

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        // A span is an interval, so it adds and subtracts as a value rather than as a date.
        if (instance?.Type == typeof(TimeSpan) && method.DeclaringType == typeof(TimeSpan))
        {
            return method.Name switch
            {
                nameof(TimeSpan.Add) => Interval(ExpressionType.Add, instance, arguments[0]),
                nameof(TimeSpan.Subtract) => Interval(ExpressionType.Subtract, instance, arguments[0]),
                nameof(TimeSpan.Duration) => Duration(instance),
                _ => null
            };
        }

        // Every member translated here takes exactly one argument. A method that takes none — such as
        // ToUniversalTime or ToLocalTime — reads the zone the stored value does not carry, so it is
        // left untranslated rather than read as an argument it does not have.
        if (instance is null
            || arguments.Count != 1
            || (method.DeclaringType != typeof(DateTime) && method.DeclaringType != typeof(DateTimeOffset))
            || (instance.Type != typeof(DateTime) && instance.Type != typeof(DateTimeOffset)))
        {
            return null;
        }

        // The amount arrives without a type mapping, because nothing has had to render it yet.
        var amount = _sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0]);

        if (DaysPerUnit.TryGetValue(method.Name, out var days))
        {
            return AddDays(instance, amount, days);
        }

        if (method.Name == nameof(DateTime.AddMonths))
        {
            return MonthArithmetic(instance, amount);
        }

        if (method.Name == nameof(DateTime.AddYears))
        {
            return MonthArithmetic(instance, YearsInMonths(amount));
        }

        if (method.Name == nameof(DateTime.Subtract))
        {
            return Subtract(instance, amount);
        }

        return null;
    }

    /// <summary>
    /// A date or timestamp minus an interval is a timestamp, and the difference of two timestamps is the
    /// interval between them, built with <c>NUMTODSINTERVAL</c>: subtracting two dates answers a number of
    /// days, so that number is scaled to seconds and the function turns it into an interval. Measured on
    /// 8.1.4.80 in both compatibility modes, which the migration notes had placed the function behind:
    /// <c>NUMTODSINTERVAL(86400, 'SECOND')</c> answered one day, a fractional count kept the fraction, and a
    /// difference that runs backwards answered a negative interval.
    /// </summary>
    private SqlExpression? Subtract(SqlExpression instance, SqlExpression amount)
        => amount.Type == typeof(TimeSpan)
            ? new SqlBinaryExpression(
                ExpressionType.Subtract,
                instance,
                amount,
                instance.Type,
                instance.TypeMapping)
            : amount.Type == typeof(DateTime)
                ? Interval(instance, amount)
                : null;

    private SqlExpression Interval(SqlExpression end, SqlExpression start)
        => _sqlExpressionFactory.Function(
            "NUMTODSINTERVAL",
            [
                _sqlExpressionFactory.Multiply(
                    new SqlBinaryExpression(
                        ExpressionType.Subtract,
                        _sqlExpressionFactory.ApplyDefaultTypeMapping(end)!,
                        _sqlExpressionFactory.ApplyDefaultTypeMapping(start)!,
                        typeof(double),
                        _doubleMapping),
                    _sqlExpressionFactory.Constant(SecondsPerDay)),
                _sqlExpressionFactory.Fragment("'SECOND'")
            ],
            nullable: true,
            argumentsPropagateNullability: [true, false],
            typeof(TimeSpan),
            _timeSpanMapping);

    /// <summary>
    /// The magnitude of an interval, built as a new interval from its absolute second count: <c>ABS</c>
    /// does not accept an interval (参数不兼容), measured on 8.1.4.80.
    /// </summary>
    private SqlExpression Duration(SqlExpression instance)
        => _sqlExpressionFactory.Function(
            "NUMTODSINTERVAL",
            [
                _sqlExpressionFactory.Function(
                    "ABS",
                    [DmIntervals.Seconds(_sqlExpressionFactory, instance)],
                    nullable: true,
                    argumentsPropagateNullability: [true],
                    typeof(double),
                    typeMapping: null),
                _sqlExpressionFactory.Fragment("'SECOND'")
            ],
            nullable: true,
            argumentsPropagateNullability: [true, false],
            typeof(TimeSpan),
            _timeSpanMapping);

    private SqlExpression Interval(ExpressionType operation, SqlExpression instance, SqlExpression amount)
        => new SqlBinaryExpression(
            operation,
            instance,
            _sqlExpressionFactory.ApplyDefaultTypeMapping(amount)!,
            typeof(TimeSpan),
            _timeSpanMapping);

    /// <summary>
    /// A literal year count is folded here, so the statement reads <c>TIMESTAMPADD(MONTH, 12, x)</c>
    /// rather than <c>TIMESTAMPADD(MONTH, 1 * 12, x)</c>. A count whose product would wrap is left
    /// unfolded, so the server sees the factor instead of a wrapped number.
    /// </summary>
    private SqlExpression YearsInMonths(SqlExpression years)
        => years is SqlConstantExpression { Value: int value } && value is >= int.MinValue / 12 and <= int.MaxValue / 12
            ? _sqlExpressionFactory.Constant(value * 12, years.TypeMapping)
            : _sqlExpressionFactory.Multiply(years, Constant(12, years));

    private SqlExpression AddDays(SqlExpression instance, SqlExpression days, long daysPerUnit)
    {
        var scaled = daysPerUnit == 1
            ? days
            : new SqlBinaryExpression(
                ExpressionType.Divide,
                days,
                Constant(daysPerUnit, days),
                typeof(double),
                days.TypeMapping);

        return new SqlBinaryExpression(
            ExpressionType.Add,
            instance,
            scaled,
            instance.Type,
            instance.TypeMapping);
    }

    private SqlExpression Constant(long value, SqlExpression like)
        => _sqlExpressionFactory.Constant(
            Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
            like.TypeMapping);

    /// <summary>
    /// <c>TIMESTAMPADD(MONTH, n, x)</c> takes the part as a bare word, the count and then the value,
    /// which is the order <see cref="DmQuerySqlGenerator" /> renders unchanged.
    /// </summary>
    private SqlExpression MonthArithmetic(SqlExpression instance, SqlExpression months)
        => Function(
            TimestampAddFunction,
            [_sqlExpressionFactory.Fragment(MonthArgument), months, instance],
            instance);

    private SqlExpression Function(string name, IReadOnlyList<SqlExpression> arguments, SqlExpression instance)
        => _sqlExpressionFactory.Function(
            name,
            arguments,
            nullable: true,
            argumentsPropagateNullability: arguments.Select(_ => true).ToArray(),
            instance.Type,
            typeMapping: null);
}
