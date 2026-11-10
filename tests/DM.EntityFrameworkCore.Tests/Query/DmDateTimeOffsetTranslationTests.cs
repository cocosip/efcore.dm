using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for <see cref="DateTimeOffset" /> translation. Every expression sits in a
/// predicate, so a client-side fallback cannot make the test pass.
/// </summary>
public class DmDateTimeOffsetTranslationTests
{
    private static readonly Dictionary<string, Func<IQueryable<Probe>, IQueryable<Probe>>> Queries = new()
    {
        ["AddDays"] = q => q.Where(p => p.Moment.AddDays(1) > p.Moment),
        ["AddHours"] = q => q.Where(p => p.Moment.AddHours(1) > p.Moment),
        ["AddMonths"] = q => q.Where(p => p.Moment.AddMonths(1) > p.Moment),
        ["AddYears"] = q => q.Where(p => p.Moment.AddYears(1) > p.Moment),
        ["UtcDateTime"] = q => q.Where(p => p.Moment.UtcDateTime > p.CreatedAt)
    };

    [Theory]
    [InlineData("AddDays", "(\"p\".\"Moment\" + 1.0) > \"p\".\"Moment\"")]
    [InlineData("AddHours", "(\"p\".\"Moment\" + (1.0 / 24.0)) > \"p\".\"Moment\"")]
    [InlineData("AddMonths", "ADD_MONTHS(\"p\".\"Moment\", 1) > \"p\".\"Moment\"")]
    [InlineData("AddYears", "ADD_MONTHS(\"p\".\"Moment\", 12) > \"p\".\"Moment\"")]
    [InlineData("UtcDateTime", "SYS_EXTRACT_UTC(\"p\".\"Moment\") > \"p\".\"CreatedAt\"")]
    public void DateTimeOffsetMember_TranslatesToDameng(string name, string predicate)
    {
        using var context = new ProbeContext();

        Assert.Equal(
            $"""
             SELECT "p"."Id", "p"."CreatedAt", "p"."Moment"
             FROM "Probes" AS "p"
             WHERE {predicate}
             """,
            Statement(Queries[name](context.Probes).ToQueryString()));
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

        public DateTimeOffset Moment { get; set; }
    }
}
