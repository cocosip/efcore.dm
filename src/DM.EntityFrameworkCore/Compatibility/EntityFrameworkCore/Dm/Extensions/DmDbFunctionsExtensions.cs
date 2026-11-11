using System.Globalization;

namespace Microsoft.EntityFrameworkCore.Dm.Extensions;

/// <summary>
/// Dameng specific methods that extend <see cref="DbFunctions" /> in LINQ queries.
/// </summary>
/// <remarks>
/// <para>
/// Inside a query these are translated by the provider: the date differences become Dameng's
/// <c>DATEDIFF(datepart, date1, date2)</c>, which counts the boundaries of the named part crossed
/// between the two values, and <see cref="IsDate" /> becomes <c>ISDATE</c>. Called outside a query they
/// compute the same answer in memory, so an expression the provider cannot translate falls back instead
/// of failing.
/// </para>
/// <para>
/// The in-memory form truncates the elapsed time rather than counting boundaries, so it agrees with the
/// server for values of the same sign and can differ by one where the two dates straddle a boundary.
/// A difference that does not fit an <see cref="int" /> overflows in that form, as it does on the server.
/// </para>
/// </remarks>
public static class DmDbFunctionsExtensions
{
    /// <summary>
    /// The number of year boundaries crossed between two dates.
    /// </summary>
    public static int DateDiffYear(this DbFunctions _, DateTime startDate, DateTime endDate)
        => endDate.Year - startDate.Year;

    /// <inheritdoc cref="DateDiffYear(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffYear(this DbFunctions _, DateTime? startDate, DateTime? endDate)
        => startDate.HasValue && endDate.HasValue
            ? endDate.Value.Year - startDate.Value.Year
            : null;

    /// <inheritdoc cref="DateDiffYear(DbFunctions, DateTime, DateTime)" />
    public static int DateDiffYear(this DbFunctions _, DateTimeOffset startDate, DateTimeOffset endDate)
        => endDate.UtcDateTime.Year - startDate.UtcDateTime.Year;

    /// <inheritdoc cref="DateDiffYear(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffYear(
        this DbFunctions _,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate)
        => startDate.HasValue && endDate.HasValue
            ? endDate.Value.UtcDateTime.Year - startDate.Value.UtcDateTime.Year
            : null;

    /// <summary>
    /// The number of month boundaries crossed between two dates.
    /// </summary>
    public static int DateDiffMonth(this DbFunctions _, DateTime startDate, DateTime endDate)
        => 12 * (endDate.Year - startDate.Year) + endDate.Month - startDate.Month;

    /// <inheritdoc cref="DateDiffMonth(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffMonth(this DbFunctions _, DateTime? startDate, DateTime? endDate)
        => startDate.HasValue && endDate.HasValue
            ? 12 * (endDate.Value.Year - startDate.Value.Year) + endDate.Value.Month - startDate.Value.Month
            : null;

    /// <inheritdoc cref="DateDiffMonth(DbFunctions, DateTime, DateTime)" />
    public static int DateDiffMonth(this DbFunctions _, DateTimeOffset startDate, DateTimeOffset endDate)
        => 12 * (endDate.UtcDateTime.Year - startDate.UtcDateTime.Year)
            + endDate.UtcDateTime.Month
            - startDate.UtcDateTime.Month;

    /// <inheritdoc cref="DateDiffMonth(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffMonth(
        this DbFunctions _,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate)
        => startDate.HasValue && endDate.HasValue
            ? 12 * (endDate.Value.UtcDateTime.Year - startDate.Value.UtcDateTime.Year)
                + endDate.Value.UtcDateTime.Month
                - startDate.Value.UtcDateTime.Month
            : null;

    /// <summary>
    /// The number of day boundaries crossed between two dates.
    /// </summary>
    public static int DateDiffDay(this DbFunctions _, DateTime startDate, DateTime endDate)
        => (endDate.Date - startDate.Date).Days;

    /// <inheritdoc cref="DateDiffDay(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffDay(this DbFunctions _, DateTime? startDate, DateTime? endDate)
        => startDate.HasValue && endDate.HasValue
            ? (endDate.Value.Date - startDate.Value.Date).Days
            : null;

    /// <inheritdoc cref="DateDiffDay(DbFunctions, DateTime, DateTime)" />
    public static int DateDiffDay(this DbFunctions _, DateTimeOffset startDate, DateTimeOffset endDate)
        => (endDate.UtcDateTime.Date - startDate.UtcDateTime.Date).Days;

    /// <inheritdoc cref="DateDiffDay(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffDay(
        this DbFunctions _,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate)
        => startDate.HasValue && endDate.HasValue
            ? (endDate.Value.UtcDateTime.Date - startDate.Value.UtcDateTime.Date).Days
            : null;

    /// <summary>
    /// The number of hour boundaries crossed between two dates.
    /// </summary>
    public static int DateDiffHour(this DbFunctions _, DateTime startDate, DateTime endDate)
        => (int)(endDate - startDate).TotalHours;

    /// <inheritdoc cref="DateDiffHour(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffHour(this DbFunctions _, DateTime? startDate, DateTime? endDate)
        => startDate.HasValue && endDate.HasValue
            ? (int)(endDate.Value - startDate.Value).TotalHours
            : null;

    /// <inheritdoc cref="DateDiffHour(DbFunctions, DateTime, DateTime)" />
    public static int DateDiffHour(this DbFunctions _, DateTimeOffset startDate, DateTimeOffset endDate)
        => (int)(endDate.UtcDateTime - startDate.UtcDateTime).TotalHours;

    /// <inheritdoc cref="DateDiffHour(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffHour(
        this DbFunctions _,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate)
        => startDate.HasValue && endDate.HasValue
            ? (int)(endDate.Value.UtcDateTime - startDate.Value.UtcDateTime).TotalHours
            : null;

    /// <summary>
    /// The number of hours between two elapsed times, for a value stored as an interval rather than a
    /// date.
    /// </summary>
    /// <remarks>
    /// The statement receives the interval where it takes a date, which is what the previous provider
    /// does; whether <c>DATEDIFF</c> accepts one is unverified. The in-memory form counts the elapsed
    /// time and truncates it, so it can differ by one from a boundary count, as the date forms do.
    /// </remarks>
    public static int DateDiffHour(this DbFunctions _, TimeSpan startTimeSpan, TimeSpan endTimeSpan)
        => (int)(endTimeSpan - startTimeSpan).TotalHours;

    /// <inheritdoc cref="DateDiffHour(DbFunctions, TimeSpan, TimeSpan)" />
    public static int? DateDiffHour(this DbFunctions _, TimeSpan? startTimeSpan, TimeSpan? endTimeSpan)
        => startTimeSpan.HasValue && endTimeSpan.HasValue
            ? (int)(endTimeSpan.Value - startTimeSpan.Value).TotalHours
            : null;

    /// <summary>
    /// The number of minute boundaries crossed between two dates.
    /// </summary>
    public static int DateDiffMinute(this DbFunctions _, DateTime startDate, DateTime endDate)
        => (int)(endDate - startDate).TotalMinutes;

    /// <inheritdoc cref="DateDiffMinute(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffMinute(this DbFunctions _, DateTime? startDate, DateTime? endDate)
        => startDate.HasValue && endDate.HasValue
            ? (int)(endDate.Value - startDate.Value).TotalMinutes
            : null;

    /// <inheritdoc cref="DateDiffMinute(DbFunctions, DateTime, DateTime)" />
    public static int DateDiffMinute(this DbFunctions _, DateTimeOffset startDate, DateTimeOffset endDate)
        => (int)(endDate.UtcDateTime - startDate.UtcDateTime).TotalMinutes;

    /// <inheritdoc cref="DateDiffMinute(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffMinute(
        this DbFunctions _,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate)
        => startDate.HasValue && endDate.HasValue
            ? (int)(endDate.Value.UtcDateTime - startDate.Value.UtcDateTime).TotalMinutes
            : null;

    /// <inheritdoc cref="DateDiffHour(DbFunctions, TimeSpan, TimeSpan)" />
    public static int DateDiffMinute(this DbFunctions _, TimeSpan startTimeSpan, TimeSpan endTimeSpan)
        => (int)(endTimeSpan - startTimeSpan).TotalMinutes;

    /// <inheritdoc cref="DateDiffHour(DbFunctions, TimeSpan, TimeSpan)" />
    public static int? DateDiffMinute(this DbFunctions _, TimeSpan? startTimeSpan, TimeSpan? endTimeSpan)
        => startTimeSpan.HasValue && endTimeSpan.HasValue
            ? (int)(endTimeSpan.Value - startTimeSpan.Value).TotalMinutes
            : null;

    /// <summary>
    /// The number of second boundaries crossed between two dates.
    /// </summary>
    public static int DateDiffSecond(this DbFunctions _, DateTime startDate, DateTime endDate)
        => (int)(endDate - startDate).TotalSeconds;

    /// <inheritdoc cref="DateDiffSecond(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffSecond(this DbFunctions _, DateTime? startDate, DateTime? endDate)
        => startDate.HasValue && endDate.HasValue
            ? (int)(endDate.Value - startDate.Value).TotalSeconds
            : null;

    /// <inheritdoc cref="DateDiffSecond(DbFunctions, DateTime, DateTime)" />
    public static int DateDiffSecond(this DbFunctions _, DateTimeOffset startDate, DateTimeOffset endDate)
        => (int)(endDate.UtcDateTime - startDate.UtcDateTime).TotalSeconds;

    /// <inheritdoc cref="DateDiffSecond(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffSecond(
        this DbFunctions _,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate)
        => startDate.HasValue && endDate.HasValue
            ? (int)(endDate.Value.UtcDateTime - startDate.Value.UtcDateTime).TotalSeconds
            : null;

    /// <inheritdoc cref="DateDiffHour(DbFunctions, TimeSpan, TimeSpan)" />
    public static int DateDiffSecond(this DbFunctions _, TimeSpan startTimeSpan, TimeSpan endTimeSpan)
        => (int)(endTimeSpan - startTimeSpan).TotalSeconds;

    /// <inheritdoc cref="DateDiffHour(DbFunctions, TimeSpan, TimeSpan)" />
    public static int? DateDiffSecond(this DbFunctions _, TimeSpan? startTimeSpan, TimeSpan? endTimeSpan)
        => startTimeSpan.HasValue && endTimeSpan.HasValue
            ? (int)(endTimeSpan.Value - startTimeSpan.Value).TotalSeconds
            : null;

    /// <summary>
    /// The number of millisecond boundaries crossed between two dates.
    /// </summary>
    public static int DateDiffMillisecond(this DbFunctions _, DateTime startDate, DateTime endDate)
        => (int)(endDate - startDate).TotalMilliseconds;

    /// <inheritdoc cref="DateDiffMillisecond(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffMillisecond(this DbFunctions _, DateTime? startDate, DateTime? endDate)
        => startDate.HasValue && endDate.HasValue
            ? (int)(endDate.Value - startDate.Value).TotalMilliseconds
            : null;

    /// <inheritdoc cref="DateDiffMillisecond(DbFunctions, DateTime, DateTime)" />
    public static int DateDiffMillisecond(this DbFunctions _, DateTimeOffset startDate, DateTimeOffset endDate)
        => (int)(endDate.UtcDateTime - startDate.UtcDateTime).TotalMilliseconds;

    /// <inheritdoc cref="DateDiffMillisecond(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffMillisecond(
        this DbFunctions _,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate)
        => startDate.HasValue && endDate.HasValue
            ? (int)(endDate.Value.UtcDateTime - startDate.Value.UtcDateTime).TotalMilliseconds
            : null;

    /// <inheritdoc cref="DateDiffHour(DbFunctions, TimeSpan, TimeSpan)" />
    public static int DateDiffMillisecond(this DbFunctions _, TimeSpan startTimeSpan, TimeSpan endTimeSpan)
        => (int)(endTimeSpan - startTimeSpan).TotalMilliseconds;

    /// <inheritdoc cref="DateDiffHour(DbFunctions, TimeSpan, TimeSpan)" />
    public static int? DateDiffMillisecond(this DbFunctions _, TimeSpan? startTimeSpan, TimeSpan? endTimeSpan)
        => startTimeSpan.HasValue && endTimeSpan.HasValue
            ? (int)(endTimeSpan.Value - startTimeSpan.Value).TotalMilliseconds
            : null;

    /// <summary>
    /// The number of microsecond boundaries crossed between two dates.
    /// </summary>
    /// <remarks>
    /// The server reads the part as <c>MICROSECOND</c>, measured on 8.1.4.80: a pair two microseconds apart
    /// answered 2. It answers `数据溢出` as soon as the two values differ in their hour part — the hours are
    /// multiplied into an int of microseconds, so even two minutes answered an overflow from 03:59 to
    /// 04:01 — and above about thirty-five minutes within one hour, so a query needs both values inside one
    /// hour. A nanosecond part has no spelling there — <c>NANOSECOND</c> is a syntax error — so no
    /// nanosecond form is provided.
    /// </remarks>
    public static int DateDiffMicrosecond(this DbFunctions _, DateTime startDate, DateTime endDate)
        => (int)(endDate - startDate).TotalMicroseconds;

    /// <inheritdoc cref="DateDiffMicrosecond(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffMicrosecond(this DbFunctions _, DateTime? startDate, DateTime? endDate)
        => startDate.HasValue && endDate.HasValue
            ? (int)(endDate.Value - startDate.Value).TotalMicroseconds
            : null;

    /// <inheritdoc cref="DateDiffMicrosecond(DbFunctions, DateTime, DateTime)" />
    public static int DateDiffMicrosecond(this DbFunctions _, DateTimeOffset startDate, DateTimeOffset endDate)
        => (int)(endDate.UtcDateTime - startDate.UtcDateTime).TotalMicroseconds;

    /// <inheritdoc cref="DateDiffMicrosecond(DbFunctions, DateTime, DateTime)" />
    public static int? DateDiffMicrosecond(
        this DbFunctions _,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate)
        => startDate.HasValue && endDate.HasValue
            ? (int)(endDate.Value.UtcDateTime - startDate.Value.UtcDateTime).TotalMicroseconds
            : null;

    /// <summary>
    /// Whether a value is a valid date, which Dameng answers with <c>ISDATE</c>.
    /// </summary>
    public static bool IsDate(this DbFunctions _, string value) => ParsesAsDate(value);

    // The parse lives in its own method because this class names its receiver "_", and a discard in the
    // same scope would bind to that parameter instead.
    private static bool ParsesAsDate(string value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
