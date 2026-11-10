using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for the <see cref="TimeSpan" /> parts. Each member is compared with a value, so a
/// client-side implementation cannot make the test pass.
/// </summary>
public class DmTimeSpanTranslationTests
{
    private static readonly Dictionary<string, Func<IQueryable<Probe>, IQueryable<Probe>>> Queries = new()
    {
        ["Days"] = q => q.Where(p => p.Span.Days > 0),
        ["Hours"] = q => q.Where(p => p.Span.Hours > 0),
        ["Minutes"] = q => q.Where(p => p.Span.Minutes > 0),
        ["Seconds"] = q => q.Where(p => p.Span.Seconds > 0)
    };

    [Theory]
    [InlineData("Days", """EXTRACT(DAY FROM "p"."Span") > 0""")]
    [InlineData("Hours", """EXTRACT(HOUR FROM "p"."Span") > 0""")]
    [InlineData("Minutes", """EXTRACT(MINUTE FROM "p"."Span") > 0""")]
    [InlineData("Seconds", """TRUNC(EXTRACT(SECOND FROM "p"."Span")) > 0""")]
    public void TimeSpanPart_ReadsTheIntervalPart(string name, string predicate)
    {
        using var context = new ProbeContext();

        Assert.Equal(
            $"""
             SELECT "p"."Id", "p"."Span"
             FROM "Probes" AS "p"
             WHERE {predicate}
             """,
            Statement(Queries[name](context.Probes).ToQueryString()));
    }

    /// <summary>
    /// The whole interval in seconds, which every total member is derived from. The parts are summed
    /// as numbers rather than read as a count, and the fraction of a second is kept.
    /// </summary>
    private const string Seconds =
        """(((EXTRACT(DAY FROM "p"."Span") * 86400.0) + (EXTRACT(HOUR FROM "p"."Span") * 3600.0)) + ((EXTRACT(MINUTE FROM "p"."Span") * 60.0) + EXTRACT(SECOND FROM "p"."Span")))""";

    [Theory]
    [InlineData("TotalSeconds", Seconds + " > 1.0")]
    [InlineData("TotalMinutes", "(" + Seconds + " / 60.0) > 1.0")]
    [InlineData("TotalHours", "(" + Seconds + " / 3600.0) > 1.0")]
    [InlineData("TotalDays", "(" + Seconds + " / 86400.0) > 1.0")]
    [InlineData("TotalMilliseconds", "(" + Seconds + " * 1000.0) > 1.0")]
    public void TotalMember_ExpressesTheIntervalInOneUnit(string name, string predicate)
    {
        using var context = new ProbeContext();

        var query = name switch
        {
            "TotalSeconds" => context.Probes.Where(p => p.Span.TotalSeconds > 1),
            "TotalMinutes" => context.Probes.Where(p => p.Span.TotalMinutes > 1),
            "TotalHours" => context.Probes.Where(p => p.Span.TotalHours > 1),
            "TotalDays" => context.Probes.Where(p => p.Span.TotalDays > 1),
            _ => context.Probes.Where(p => p.Span.TotalMilliseconds > 1)
        };

        Assert.Equal(
            $"""
             SELECT "p"."Id", "p"."Span"
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

        public TimeSpan Span { get; set; }
    }
}
