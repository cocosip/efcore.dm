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
