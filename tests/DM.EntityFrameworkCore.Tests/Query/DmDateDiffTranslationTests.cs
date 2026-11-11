using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Dm.Extensions;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for the <see cref="EF.Functions" /> date differences. Every call sits in a
/// predicate, so a client side implementation cannot make the test pass.
/// </summary>
public class DmDateDiffTranslationTests
{
    private static readonly Dictionary<string, Func<IQueryable<Probe>, IQueryable<Probe>>> Queries = new()
    {
        ["Year"] = q => q.Where(p => EF.Functions.DateDiffYear(p.CreatedAt, p.CreatedAt) > 0),
        ["Month"] = q => q.Where(p => EF.Functions.DateDiffMonth(p.CreatedAt, p.CreatedAt) > 0),
        ["Day"] = q => q.Where(p => EF.Functions.DateDiffDay(p.CreatedAt, p.CreatedAt) > 0),
        ["Hour"] = q => q.Where(p => EF.Functions.DateDiffHour(p.CreatedAt, p.CreatedAt) > 0),
        ["Minute"] = q => q.Where(p => EF.Functions.DateDiffMinute(p.CreatedAt, p.CreatedAt) > 0),
        ["Second"] = q => q.Where(p => EF.Functions.DateDiffSecond(p.CreatedAt, p.CreatedAt) > 0),
        ["Millisecond"] = q => q.Where(p => EF.Functions.DateDiffMillisecond(p.CreatedAt, p.CreatedAt) > 0)
    };

    [Theory]
    [InlineData("Year", "YEAR")]
    [InlineData("Month", "MONTH")]
    [InlineData("Day", "DAY")]
    [InlineData("Hour", "HOUR")]
    [InlineData("Minute", "MINUTE")]
    [InlineData("Second", "SECOND")]
    [InlineData("Millisecond", "MILLISECOND")]
    public void DateDiff_TranslatesToDatediff(string unit, string datePart)
    {
        using var context = new ProbeContext();

        Assert.Equal(
            $"""
             SELECT "p"."Id", "p"."CreatedAt", "p"."DeletedAt", "p"."OffsetAt", "p"."Span"
             FROM "Probes" AS "p"
             WHERE DATEDIFF({datePart}, "p"."CreatedAt", "p"."CreatedAt") > 0
             """,
            Statement(Queries[unit](context.Probes).ToQueryString()));
    }

    /// <summary>
    /// The nullable overload is the one a consumer reaches for when either date can be absent, so it has
    /// to translate as well rather than fall back to the client.
    /// </summary>
    [Fact]
    public void DateDiff_WithNullableDates_TranslatesToDatediff()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."CreatedAt", "p"."DeletedAt", "p"."OffsetAt", "p"."Span"
            FROM "Probes" AS "p"
            WHERE DATEDIFF(DAY, "p"."DeletedAt", "p"."DeletedAt") > 0
            """,
            Statement(
                context.Probes
                    .Where(p => EF.Functions.DateDiffDay(p.DeletedAt, p.DeletedAt) > 0)
                    .ToQueryString()));
    }

    /// <summary>
    /// A date with an offset is compared in UTC, which is what the in-memory form does with it too.
    /// </summary>
    [Fact]
    public void DateDiff_WithAnOffset_TranslatesToDatediff()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."CreatedAt", "p"."DeletedAt", "p"."OffsetAt", "p"."Span"
            FROM "Probes" AS "p"
            WHERE DATEDIFF(HOUR, "p"."OffsetAt", "p"."OffsetAt") > 0
            """,
            Statement(
                context.Probes
                    .Where(p => EF.Functions.DateDiffHour(p.OffsetAt, p.OffsetAt) > 0)
                    .ToQueryString()));
    }

    /// <summary>
    /// Outside a query the method computes the difference itself, so an expression the provider cannot
    /// translate does not throw.
    /// </summary>
    [Fact]
    public void DateDiff_OutsideAQuery_ComputesTheDifference()
    {
        var start = new DateTime(2024, 1, 31, 10, 0, 0);
        var end = new DateTime(2025, 3, 2, 12, 30, 0);

        Assert.Equal(1, EF.Functions.DateDiffYear(start, end));
        Assert.Equal(14, EF.Functions.DateDiffMonth(start, end));
        Assert.Equal(396, EF.Functions.DateDiffDay(start, end));
        Assert.Equal(9506, EF.Functions.DateDiffHour(start, end));
        Assert.Equal(570390, EF.Functions.DateDiffMinute(start, end));
        Assert.Equal(34223400, EF.Functions.DateDiffSecond(start, end));
        Assert.Equal(1500, EF.Functions.DateDiffMillisecond(start, start.AddMilliseconds(1500)));
        Assert.Null(EF.Functions.DateDiffDay((DateTime?)null, end));
    }

    /// <summary>
    /// An elapsed time is not sent as an argument of the statement: the server refuses an interval where
    /// <c>DATEDIFF</c> takes a date (<c>参数不兼容</c>), measured on 8.1.4.80 for every unit, so the call is
    /// reported as untranslatable rather than sent to fail. The previous provider emitted the statement
    /// anyway, which is why the shape used to be pinned here.
    /// </summary>
    [Fact]
    public void DateDiff_WithAnInterval_IsNotTranslated()
    {
        using var context = new ProbeContext();

        Assert.Throws<InvalidOperationException>(
            () => context.Probes
                .Where(p => EF.Functions.DateDiffHour(p.Span, p.Span) > 0)
                .ToQueryString());
    }

    /// <summary>
    /// The microsecond part is the finest one the server names: a pair two microseconds apart answered 2,
    /// measured on 8.1.4.80, and <c>NANOSECOND</c> is a syntax error there.
    /// </summary>
    [Fact]
    public void DateDiffMicrosecond_TranslatesToDatediff()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."CreatedAt", "p"."DeletedAt", "p"."OffsetAt", "p"."Span"
            FROM "Probes" AS "p"
            WHERE DATEDIFF(MICROSECOND, "p"."CreatedAt", "p"."DeletedAt") > 0
            """,
            Statement(
                context.Probes
                    .Where(p => EF.Functions.DateDiffMicrosecond(p.CreatedAt, p.DeletedAt) > 0)
                    .ToQueryString()));
    }

    /// <summary>
    /// Called outside a query the difference is computed here, and the elapsed time is what it counts:
    /// the previous provider subtracted the hour component only, which answered a value that could not
    /// grow past a day.
    /// </summary>
    [Fact]
    public void DateDiff_WithAnInterval_ComputesTheElapsedTime()
    {
        Assert.Equal(30, EF.Functions.DateDiffHour(TimeSpan.Zero, TimeSpan.FromHours(30)));
        Assert.Equal(-30, EF.Functions.DateDiffHour(TimeSpan.FromHours(30), TimeSpan.Zero));
        Assert.Equal(90, EF.Functions.DateDiffMinute(TimeSpan.Zero, TimeSpan.FromMinutes(90)));
        Assert.Equal(30, EF.Functions.DateDiffSecond(TimeSpan.Zero, TimeSpan.FromSeconds(30.5)));
        Assert.Equal(1500, EF.Functions.DateDiffMillisecond(TimeSpan.Zero, TimeSpan.FromMilliseconds(1500)));
        Assert.Null(EF.Functions.DateDiffHour(null, TimeSpan.FromHours(1)));
        Assert.Null(EF.Functions.DateDiffMinute(TimeSpan.FromMinutes(1), null));
    }
    private static string Statement(string sql)
        => string.Join(
            "\n",
            sql.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Where(line => !line.StartsWith("--", StringComparison.Ordinal))
                .Select(line => line.TrimEnd()));

    private sealed class ProbeContext : DbContext
    {
        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? DeletedAt { get; set; }

        public DateTimeOffset OffsetAt { get; set; }

        public TimeSpan Span { get; set; }
    }
}
