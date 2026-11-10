using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for date and time translation. Every expression sits in a predicate, so a
/// client-side fallback cannot make the test pass.
/// </summary>
public class DmDateTimeMethodTranslationTests
{
    private static readonly Dictionary<string, Func<IQueryable<Probe>, IQueryable<Probe>>> Queries = new()
    {
        ["AddDays"] = q => q.Where(p => p.CreatedAt.AddDays(1) > p.CreatedAt),
        ["AddHours"] = q => q.Where(p => p.CreatedAt.AddHours(1) > p.CreatedAt),
        ["AddMinutes"] = q => q.Where(p => p.CreatedAt.AddMinutes(1) > p.CreatedAt),
        ["AddSeconds"] = q => q.Where(p => p.CreatedAt.AddSeconds(1) > p.CreatedAt),
        ["AddMilliseconds"] = q => q.Where(p => p.CreatedAt.AddMilliseconds(1) > p.CreatedAt),
        ["AddMicroseconds"] = q => q.Where(p => p.CreatedAt.AddMicroseconds(1) > p.CreatedAt),
        ["AddMonths"] = q => q.Where(p => p.CreatedAt.AddMonths(1) > p.CreatedAt),
        ["AddYears"] = q => q.Where(p => p.CreatedAt.AddYears(1) > p.CreatedAt),
        ["AddDaysColumn"] = q => q.Where(p => p.CreatedAt.AddDays(p.Days) > p.CreatedAt),
        ["Date"] = q => q.Where(p => p.CreatedAt.Date > p.CreatedAt),
        ["Now"] = q => q.Where(p => p.CreatedAt < DateTime.Now),
        ["UtcNow"] = q => q.Where(p => p.CreatedAt < DateTime.UtcNow),
        ["Today"] = q => q.Where(p => p.CreatedAt < DateTime.Today)
    };

    [Theory]
    [InlineData("AddDays", "(\"p\".\"CreatedAt\" + 1.0) > \"p\".\"CreatedAt\"")]
    [InlineData("AddHours", "(\"p\".\"CreatedAt\" + (1.0 / 24.0)) > \"p\".\"CreatedAt\"")]
    [InlineData("AddMinutes", "(\"p\".\"CreatedAt\" + (1.0 / 1440.0)) > \"p\".\"CreatedAt\"")]
    [InlineData("AddSeconds", "(\"p\".\"CreatedAt\" + (1.0 / 86400.0)) > \"p\".\"CreatedAt\"")]
    [InlineData("AddMilliseconds", "(\"p\".\"CreatedAt\" + (1.0 / 86400000.0)) > \"p\".\"CreatedAt\"")]
    [InlineData("AddMicroseconds", "(\"p\".\"CreatedAt\" + (1.0 / 86400000000.0)) > \"p\".\"CreatedAt\"")]
    [InlineData("AddMonths", "ADD_MONTHS(\"p\".\"CreatedAt\", 1) > \"p\".\"CreatedAt\"")]
    [InlineData("AddYears", "ADD_MONTHS(\"p\".\"CreatedAt\", 12) > \"p\".\"CreatedAt\"")]
    [InlineData("AddDaysColumn", "(\"p\".\"CreatedAt\" + \"p\".\"Days\") > \"p\".\"CreatedAt\"")]
    [InlineData("Date", "TRUNC(\"p\".\"CreatedAt\") > \"p\".\"CreatedAt\"")]
    [InlineData("Now", "\"p\".\"CreatedAt\" < CURRENT_TIMESTAMP")]
    [InlineData("UtcNow", "\"p\".\"CreatedAt\" < SYS_EXTRACT_UTC(SYSTIMESTAMP)")]
    [InlineData("Today", "\"p\".\"CreatedAt\" < TRUNC(CURRENT_TIMESTAMP)")]
    public void DateTimeMember_TranslatesToDameng(string name, string predicate)
    {
        using var context = new ProbeContext();

        Assert.Equal(
            $"""
             SELECT "p"."Id", "p"."CreatedAt", "p"."Days"
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

        public double Days { get; set; }
    }
}
