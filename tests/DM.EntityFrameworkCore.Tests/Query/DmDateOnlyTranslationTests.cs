using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for the <see cref="DateOnly" /> conversions and members. Each call is compared with
/// a column, so a client side implementation cannot make the test pass.
/// </summary>
public class DmDateOnlyTranslationTests
{
    [Fact]
    public void FromDateTime_TruncatesThenCastsToDate()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."CreatedAt", "p"."Day", "p"."Months"
            FROM "Probes" AS "p"
            WHERE CAST(TRUNC("p"."CreatedAt") AS DATE) > "p"."Day"
            """,
            Statement(
                context.Probes
                    .Where(p => DateOnly.FromDateTime(p.CreatedAt) > p.Day)
                    .ToQueryString()));
    }

    [Fact]
    public void AddDays_AddsTheDayCountToTheDate()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."CreatedAt", "p"."Day", "p"."Months"
            FROM "Probes" AS "p"
            WHERE ("p"."Day" + 1) > "p"."Day"
            """,
            Statement(
                context.Probes
                    .Where(p => p.Day.AddDays(1) > p.Day)
                    .ToQueryString()));
    }

    /// <summary>
    /// Month arithmetic is <c>ADD_MONTHS</c>, the same function the timestamp path uses, and a year is
    /// twelve months of it. The counts are literals, because a captured variable reaches the translator
    /// as a parameter and cannot be folded.
    /// </summary>
    [Fact]
    public void AddMonths_AddsTheMonthCount()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."CreatedAt", "p"."Day", "p"."Months"
            FROM "Probes" AS "p"
            WHERE ADD_MONTHS("p"."Day", 3) > "p"."Day"
            """,
            Statement(
                context.Probes
                    .Where(p => p.Day.AddMonths(3) > p.Day)
                    .ToQueryString()));

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."CreatedAt", "p"."Day", "p"."Months"
            FROM "Probes" AS "p"
            WHERE ADD_MONTHS("p"."Day", 12) > "p"."Day"
            """,
            Statement(
                context.Probes
                    .Where(p => p.Day.AddYears(1) > p.Day)
                    .ToQueryString()));

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."CreatedAt", "p"."Day", "p"."Months"
            FROM "Probes" AS "p"
            WHERE ADD_MONTHS("p"."Day", -24) > "p"."Day"
            """,
            Statement(
                context.Probes
                    .Where(p => p.Day.AddYears(-2) > p.Day)
                    .ToQueryString()));
    }
    /// <summary>
    /// A count whose product would wrap is not folded into a number the server would then apply.
    /// </summary>
    [Fact]
    public void AddYears_WithACountThatWouldWrap_IsNotFolded()
    {
        using var context = new ProbeContext();

        var statement = Statement(
            context.Probes
                .Where(p => p.Day.AddYears(int.MaxValue) > p.Day)
                .ToQueryString());

        Assert.Contains($"""ADD_MONTHS("p"."Day", {int.MaxValue} * 12)""", statement, StringComparison.Ordinal);
    }
    /// <summary>
    /// A count that is not a literal cannot be folded, so a year keeps its factor of twelve.
    /// </summary>
    [Fact]
    public void AddMonths_WithAColumnCount_KeepsTheFactor()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."CreatedAt", "p"."Day", "p"."Months"
            FROM "Probes" AS "p"
            WHERE ADD_MONTHS("p"."Day", "p"."Months" * 12) > "p"."Day"
            """,
            Statement(
                context.Probes
                    .Where(p => p.Day.AddYears(p.Months) > p.Day)
                    .ToQueryString()));
    }

    /// <summary>
    /// A date reads its parts like a timestamp does, with the same <c>EXTRACT</c> shape.
    /// </summary>
    [Theory]
    [InlineData("Year", "EXTRACT(YEAR FROM \"p\".\"Day\") > 2000")]
    [InlineData("Month", "EXTRACT(MONTH FROM \"p\".\"Day\") > 6")]
    [InlineData("Day", "EXTRACT(DAY FROM \"p\".\"Day\") > 15")]
    [InlineData("DayOfYear", "DATEPART(dayofyear, \"p\".\"Day\") > 15")]
    public void Member_ReadsThePart(string member, string predicate)
    {
        using var context = new ProbeContext();

        var query = member switch
        {
            "Year" => context.Probes.Where(p => p.Day.Year > 2000),
            "Month" => context.Probes.Where(p => p.Day.Month > 6),
            "DayOfYear" => context.Probes.Where(p => p.Day.DayOfYear > 15),
            _ => context.Probes.Where(p => p.Day.Day > 15)
        };

        Assert.Equal(
            $"""
             SELECT "p"."Id", "p"."CreatedAt", "p"."Day", "p"."Months"
             FROM "Probes" AS "p"
             WHERE {predicate}
             """,
            Statement(query.ToQueryString()));
    }

    /// <summary>
    /// The day number is a difference from 1970-01-01 plus that day's own number, because the server's
    /// calendar skips the ten days of the Gregorian reform: a count from 0001-01-01 answered 739619 for
    /// 2026-01-02 where .NET answers 739617, measured on 8.1.4.80. The anchored count answered the value
    /// .NET computes.
    /// </summary>
    [Fact]
    public void DayNumber_CountsDaysFromTheEpoch()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."CreatedAt", "p"."Day", "p"."Months"
            FROM "Probes" AS "p"
            WHERE (DATEDIFF(DAY, DATE '1970-01-01', "p"."Day") + 719162) > 739000
            """,
            Statement(
                context.Probes
                    .Where(p => p.Day.DayNumber > 739000)
                    .ToQueryString()));
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

        public DateOnly Day { get; set; }

        public int Months { get; set; }
    }
}
