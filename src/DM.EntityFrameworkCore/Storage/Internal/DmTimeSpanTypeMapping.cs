using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Maps a <see cref="TimeSpan" /> to a Dameng interval.
/// </summary>
/// <remarks>
/// The base mapping writes a <see cref="TimeSpan" /> literal as a bare time string, which is a
/// character value rather than an interval and is not comparable with an interval column. An interval
/// literal names its type and its leading field, and a negative span carries the sign in front of the
/// whole value rather than on a field.
/// <para>
/// A reader hands an interval column over as a <see cref="TimeSpan" /> but an interval expression, such as
/// the one <c>NUMTODSINTERVAL</c> builds, as the driver's own interval type, so the value is read as an
/// object and converted: reading it as a <see cref="TimeSpan" /> answered
/// <c>Object must implement IConvertible</c>, measured on 8.1.4.80.
/// </para>
/// </remarks>
internal sealed class DmTimeSpanTypeMapping : TimeSpanTypeMapping
{
    private static readonly MethodInfo ToTimeSpanMethod = typeof(DmTimeSpanTypeMapping).GetMethod(
        nameof(ToTimeSpan),
        BindingFlags.NonPublic | BindingFlags.Static)!;

    public DmTimeSpanTypeMapping(string storeType)
        : base(storeType, System.Data.DbType.Time)
    {
    }

    public override MethodInfo GetDataReaderMethod()
        => typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetValue), [typeof(int)])!;

    public override Expression CustomizeDataReaderExpression(Expression expression)
        => Expression.Call(ToTimeSpanMethod, expression);

    protected override string GenerateNonNullSqlLiteral(object value)
    {
        // A mapping retyped onto a time column keeps the base form, which is what that column holds.
        if (!StoreType.StartsWith("INTERVAL", StringComparison.OrdinalIgnoreCase))
        {
            return base.GenerateNonNullSqlLiteral(value);
        }

        return "INTERVAL '" + Literal((TimeSpan)value) + "' DAY TO SECOND";
    }

    private static TimeSpan ToTimeSpan(object value)
        => value switch
        {
            TimeSpan span => span,
            global::Dm.DmIntervalDT interval => FromInterval(interval),
            _ => throw new InvalidCastException(
                $"A Dameng interval was read as {value.GetType().FullName}, which is neither an "
                + "interval nor a time span.")
        };

    /// <summary>
    /// The accessors answer the magnitude only — the sign of a negative interval is in its text — and the
    /// fraction is in microseconds, measured on 8.1.4.80: <c>-1 1:1:1.333</c> answered 1 day, 1 hour, 1
    /// minute, 1 second and 333000. The parts are summed as ticks here, so the fraction survives and the
    /// sign is applied to the whole value.
    /// </summary>
    private static TimeSpan FromInterval(global::Dm.DmIntervalDT interval)
    {
        var ticks = (((interval.getDay() * 24L + interval.getHour()) * 60 + interval.getMinute()) * 60
                     + interval.getSecond()) * TimeSpan.TicksPerSecond
                    + interval.getMsec() * 10;

        var span = new TimeSpan(ticks);

        return interval.GetTimeSpanFormatString().StartsWith("-", StringComparison.Ordinal) ? span.Negate() : span;
    }

    private static string Literal(TimeSpan span)
    {
        var magnitude = span.Duration();

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}{1} {2:00}:{3:00}:{4:00}{5}",
            span < TimeSpan.Zero ? "-" : string.Empty,
            magnitude.Days,
            magnitude.Hours,
            magnitude.Minutes,
            magnitude.Seconds,
            magnitude.Milliseconds == 0
                ? string.Empty
                : "." + magnitude.Milliseconds.ToString("000", CultureInfo.InvariantCulture));
    }
}
