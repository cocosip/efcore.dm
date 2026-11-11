using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates date and time members to Dameng SQL.
/// </summary>
/// <remarks>
/// Dameng's Oracle-compatible mode reads date parts with <c>EXTRACT(PART FROM value)</c>, which
/// is not a normal function call shape: the first argument is a keyword, not an expression.
/// <see cref="DmQuerySqlGenerator" /> renders that shape.
/// <para>
/// The clock members are static, so they arrive with no instance. They read the server clock,
/// which means a query using them is not repeatable within a statement.
/// </para>
/// </remarks>
internal sealed class DmDateTimeMemberTranslator : IMemberTranslator
{
    private const string ExtractFunction = "EXTRACT";
    private const string DatePartFunction = "DATEPART";
    private const string TruncFunction = "TRUNC";
    private const string CurrentTimestampFunction = "CURRENT_TIMESTAMP";
    private const string SysTimestampFunction = "SYSTIMESTAMP";
    private const string UtcFunction = "SYS_EXTRACT_UTC";

    private static readonly Dictionary<string, string> DateParts = new(StringComparer.Ordinal)
    {
        [nameof(DateTime.Year)] = "YEAR",
        [nameof(DateTime.Month)] = "MONTH",
        [nameof(DateTime.Day)] = "DAY",
        [nameof(DateTime.Hour)] = "HOUR",
        [nameof(DateTime.Minute)] = "MINUTE",
        [nameof(DateTime.Second)] = "SECOND"
    };

    /// <summary>
    /// The two parts <c>EXTRACT</c> does not have, which <c>DATEPART</c> reads with the part as its
    /// first argument. The reference provider reads every part this way; this dialect reads the
    /// common ones with <c>EXTRACT</c> and these two here, because <c>EXTRACT</c> cannot express them.
    /// </summary>
    private static readonly Dictionary<string, string> DatePartArguments = new(StringComparer.Ordinal)
    {
        [nameof(DateTime.DayOfYear)] = "dayofyear",
        [nameof(DateTime.Millisecond)] = "millisecond"
    };

    /// <summary>
    /// A span is stored as an interval, whose parts are read with the same <c>EXTRACT</c>. The member
    /// names are plural, and the total members are not parts and are left untranslated.
    /// </summary>
    private static readonly Dictionary<string, string> IntervalParts = new(StringComparer.Ordinal)
    {
        [nameof(TimeSpan.Days)] = "DAY",
        [nameof(TimeSpan.Hours)] = "HOUR",
        [nameof(TimeSpan.Minutes)] = "MINUTE",
        [nameof(TimeSpan.Seconds)] = "SECOND"
    };

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmDateTimeMemberTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MemberInfo member,
        Type returnType,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (member.DeclaringType == typeof(TimeSpan))
        {
            if (instance is null)
            {
                return null;
            }

            if (IntervalParts.TryGetValue(member.Name, out var intervalPart))
            {
                return Extract(intervalPart, instance, returnType, member.Name);
            }

            return Total(instance, member.Name) ?? Span(instance, member.Name);
        }

        if (member.DeclaringType != typeof(DateTime)
            && member.DeclaringType != typeof(DateTimeOffset)
            && member.DeclaringType != typeof(DateOnly)
            && member.DeclaringType != typeof(TimeOnly))
        {
            return null;
        }

        // A date or a time of day reads its parts the same way a timestamp does.
        if (member.DeclaringType == typeof(DateOnly) || member.DeclaringType == typeof(TimeOnly))
        {
            return instance is null ? null : Instance(instance, member.Name, returnType);
        }

        return instance is null
            ? Clock(member.Name, returnType)
            : Instance(instance, member.Name, returnType);
    }

    /// <summary>
    /// The members that are not a part of the interval: the tick count, the millisecond component and the
    /// magnitude.
    /// </summary>
    /// <remarks>
    /// ponytail: the tick count is real arithmetic, so a span beyond about 292 years loses precision; a
    /// time span that wide has no place in a schema, and an exact count would need an integer path.
    /// The tick count is the interval in seconds scaled, and the millisecond component is the seconds part
    /// scaled and reduced, which answered -500 for -5.5 seconds, the value .NET answers; both were measured
    /// on 8.1.4.80. The magnitude is a method rather than a member, so it is translated there.
    /// </remarks>
    private SqlExpression? Span(SqlExpression instance, string memberName)
        => memberName switch
        {
            nameof(TimeSpan.Ticks) => _sqlExpressionFactory.ApplyDefaultTypeMapping(
                _sqlExpressionFactory.Convert(
                    _sqlExpressionFactory.Multiply(
                        DmIntervals.Seconds(_sqlExpressionFactory, instance),
                        _sqlExpressionFactory.Constant(10000000L)),
                    typeof(long)))!,

            nameof(TimeSpan.Milliseconds) => _sqlExpressionFactory.ApplyDefaultTypeMapping(
                _sqlExpressionFactory.Modulo(
                    _sqlExpressionFactory.Convert(
                        _sqlExpressionFactory.Multiply(
                            _sqlExpressionFactory.ApplyDefaultTypeMapping(
                                DmIntervals.Part(_sqlExpressionFactory, "SECOND", instance))!,
                            _sqlExpressionFactory.Constant(1000.0)),
                        typeof(int)),
                    _sqlExpressionFactory.Constant(1000)))!,


            _ => null
        };

    /// <summary>
    /// A total member is the interval expressed in one unit, which is a sum over the parts. The
    /// fraction of a second is kept here, because a total is not truncated, unlike the parts.
    /// </summary>
    private SqlExpression? Total(SqlExpression instance, string memberName)
    {
        var seconds = DmIntervals.Seconds(_sqlExpressionFactory, instance);

        return memberName switch
        {
            nameof(TimeSpan.TotalDays) => _sqlExpressionFactory.Divide(seconds, _sqlExpressionFactory.Constant(86400.0)),
            nameof(TimeSpan.TotalHours) => _sqlExpressionFactory.Divide(seconds, _sqlExpressionFactory.Constant(3600.0)),
            nameof(TimeSpan.TotalMinutes) => _sqlExpressionFactory.Divide(seconds, _sqlExpressionFactory.Constant(60.0)),
            nameof(TimeSpan.TotalSeconds) => seconds,
            nameof(TimeSpan.TotalMilliseconds) => _sqlExpressionFactory.Multiply(
                seconds,
                _sqlExpressionFactory.Constant(1000.0)),
            _ => null
        };
    }


    /// <summary>
    /// The part is read as a number here, because a total is arithmetic over the parts rather than a
    /// count read straight from the interval.
    /// </summary>
    private SqlExpression ExtractPart(string part, SqlExpression instance)
        => _sqlExpressionFactory.ApplyDefaultTypeMapping(
            Function(ExtractFunction, [_sqlExpressionFactory.Fragment(part), instance], typeof(double)))!;

    /// <summary>
    /// <c>EXTRACT(SECOND ...)</c> keeps the fraction of a second, which .NET's <c>Seconds</c> member
    /// does not, so the seconds are truncated. A negative interval truncates towards zero, which is
    /// the direction .NET takes as well.
    /// </summary>
    private SqlExpression Extract(string part, SqlExpression instance, Type returnType, string memberName)
    {
        var extracted = Function(ExtractFunction, [_sqlExpressionFactory.Fragment(part), instance], returnType);

        return memberName == nameof(TimeSpan.Seconds)
            ? Function(TruncFunction, [extracted], returnType)
            : extracted;
    }

    /// <summary>
    /// <c>TRUNC</c> drops the time of day, which is what <c>Date</c> means.
    /// </summary>
    private SqlExpression? Instance(SqlExpression instance, string memberName, Type returnType)
    {
        if (memberName == nameof(DateTime.Date))
        {
            return Function(TruncFunction, [instance], returnType);
        }

        if (memberName == nameof(DateTimeOffset.UtcDateTime))
        {
            return Function(UtcFunction, [instance], returnType);
        }

        if (DateParts.TryGetValue(memberName, out var part))
        {
            return Function(ExtractFunction, [_sqlExpressionFactory.Fragment(part), instance], returnType);
        }

        return DatePartArguments.TryGetValue(memberName, out var argument)
            ? Function(DatePartFunction, [_sqlExpressionFactory.Fragment(argument), instance], returnType)
            : null;
    }

    private SqlExpression? Clock(string memberName, Type returnType)
        => memberName switch
        {
            // CURRENT_TIMESTAMP carries the column precision, unlike SYSDATE, which is a DATE and
            // would round a row created a moment ago into the future.
            nameof(DateTime.Now) => Function(CurrentTimestampFunction, [], returnType),
            nameof(DateTime.Today) => Function(
                TruncFunction,
                [Function(CurrentTimestampFunction, [], returnType)],
                returnType),

            // SYSTIMESTAMP carries the server time zone, and SYS_EXTRACT_UTC removes it.
            nameof(DateTime.UtcNow) => Function(
                UtcFunction,
                [Function(SysTimestampFunction, [], returnType)],
                returnType),
            _ => null
        };

    private SqlExpression Function(string name, IReadOnlyList<SqlExpression> arguments, Type returnType)
        => _sqlExpressionFactory.Function(
            name,
            arguments,
            nullable: true,
            argumentsPropagateNullability: arguments.Select(_ => true).ToArray(),
            returnType,
            typeMapping: null);
}
