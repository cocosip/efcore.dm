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

    public DmDateTimeMethodTranslator(
        ISqlExpressionFactory sqlExpressionFactory,
        IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _timeSpanMapping = typeMappingSource.FindMapping(typeof(TimeSpan))!;
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
    /// A date or timestamp minus an interval is a timestamp. The difference of two of them is not the
    /// interval between them in Dameng: the SQL development guide states that subtracting two dates
    /// gives a number of days, and turning that number into an interval needs <c>NUMTODSINTERVAL</c>,
    /// which the migration notes place behind Oracle compatible mode, so a dialect that must work in
    /// every mode cannot use it. The shape is left untranslated rather than declared a
    /// <see cref="TimeSpan" /> that the server would not send back.
    /// </summary>
    private SqlExpression? Subtract(SqlExpression instance, SqlExpression amount)
        => amount.Type == typeof(TimeSpan)
            ? new SqlBinaryExpression(
                ExpressionType.Subtract,
                instance,
                amount,
                instance.Type,
                instance.TypeMapping)
            : null;

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
