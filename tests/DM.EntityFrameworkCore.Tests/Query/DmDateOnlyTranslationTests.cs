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
            SELECT "p"."Id", "p"."CreatedAt", "p"."Day"
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
            SELECT "p"."Id", "p"."CreatedAt", "p"."Day"
            FROM "Probes" AS "p"
            WHERE ("p"."Day" + 1) > "p"."Day"
            """,
            Statement(
                context.Probes
                    .Where(p => p.Day.AddDays(1) > p.Day)
                    .ToQueryString()));
    }

    /// <summary>
    /// A date reads its parts like a timestamp does, with the same <c>EXTRACT</c> shape.
    /// </summary>
    [Theory]
    [InlineData("Year", "EXTRACT(YEAR FROM \"p\".\"Day\") > 2000")]
    [InlineData("Month", "EXTRACT(MONTH FROM \"p\".\"Day\") > 6")]
    [InlineData("Day", "EXTRACT(DAY FROM \"p\".\"Day\") > 15")]
    public void Member_ReadsThePart(string member, string predicate)
    {
        using var context = new ProbeContext();

        var query = member switch
        {
            "Year" => context.Probes.Where(p => p.Day.Year > 2000),
            "Month" => context.Probes.Where(p => p.Day.Month > 6),
            _ => context.Probes.Where(p => p.Day.Day > 15)
        };

        Assert.Equal(
            $"""
             SELECT "p"."Id", "p"."CreatedAt", "p"."Day"
             FROM "Probes" AS "p"
             WHERE {predicate}
             """,
            Statement(query.ToQueryString()));
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
    }
}
