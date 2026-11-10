using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for the <see cref="TimeOnly" /> conversions. Each call is compared with a column,
/// so a client side implementation cannot make the test pass.
/// </summary>
public class DmTimeOnlyTranslationTests
{
    [Fact]
    public void FromDateTime_CastsToTime()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."At", "p"."CreatedAt", "p"."Span"
            FROM "Probes" AS "p"
            WHERE CAST("p"."CreatedAt" AS TIME) > "p"."At"
            """,
            Statement(
                context.Probes
                    .Where(p => TimeOnly.FromDateTime(p.CreatedAt) > p.At)
                    .ToQueryString()));
    }

    [Fact]
    public void FromTimeSpan_AddsTheIntervalToTheEpoch()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."At", "p"."CreatedAt", "p"."Span"
            FROM "Probes" AS "p"
            WHERE CAST(TIMESTAMP '1970-01-01 00:00:00' + "p"."Span" AS TIME) > "p"."At"
            """,
            Statement(
                context.Probes
                    .Where(p => TimeOnly.FromTimeSpan(p.Span) > p.At)
                    .ToQueryString()));
    }

    /// <summary>
    /// A time of day reads its parts like a timestamp does. The reference provider reads them with
    /// <c>DATEPART</c>; this dialect uses <c>EXTRACT</c> throughout, which is the shape the live matrix
    /// has to confirm against a server.
    /// </summary>
    [Theory]
    [InlineData("Hour", "EXTRACT(HOUR FROM \"p\".\"At\") > 12")]
    [InlineData("Minute", "EXTRACT(MINUTE FROM \"p\".\"At\") > 12")]
    [InlineData("Second", "EXTRACT(SECOND FROM \"p\".\"At\") > 12")]
    [InlineData("Millisecond", "DATEPART(millisecond, \"p\".\"At\") > 12")]
    public void Member_ReadsThePart(string member, string predicate)
    {
        using var context = new ProbeContext();

        var query = member switch
        {
            "Hour" => context.Probes.Where(p => p.At.Hour > 12),
            "Minute" => context.Probes.Where(p => p.At.Minute > 12),
            "Millisecond" => context.Probes.Where(p => p.At.Millisecond > 12),
            _ => context.Probes.Where(p => p.At.Second > 12)
        };

        Assert.Equal(
            $"""
             SELECT "p"."Id", "p"."At", "p"."CreatedAt", "p"."Span"
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

        public TimeOnly At { get; set; }

        public TimeSpan Span { get; set; }
    }
}
