using System.Globalization;
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
/// </remarks>
internal sealed class DmTimeSpanTypeMapping : TimeSpanTypeMapping
{
    public DmTimeSpanTypeMapping(string storeType)
        : base(storeType, System.Data.DbType.Time)
    {
    }

    protected override string GenerateNonNullSqlLiteral(object value)
    {
        // A mapping retyped onto a time column keeps the base form, which is what that column holds.
        if (!StoreType.StartsWith("INTERVAL", StringComparison.OrdinalIgnoreCase))
        {
            return base.GenerateNonNullSqlLiteral(value);
        }

        var span = (TimeSpan)value;
        var magnitude = span.Duration();

        var text = string.Format(
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

        return "INTERVAL '" + text + "' DAY TO SECOND";
    }
}
