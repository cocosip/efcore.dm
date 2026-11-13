using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for date part translation. Every expression sits in a predicate, so a
/// client-side fallback cannot make the test pass.
/// </summary>
public class DmDateTimeTranslationTests
{
    [Theory]
    [InlineData(nameof(DateTime.Year), "YEAR")]
    [InlineData(nameof(DateTime.Month), "MONTH")]
    [InlineData(nameof(DateTime.Day), "DAY")]
    [InlineData(nameof(DateTime.Hour), "HOUR")]
    [InlineData(nameof(DateTime.Minute), "MINUTE")]
    [InlineData(nameof(DateTime.Second), "SECOND")]
    public void DatePart_GeneratesExtractWithKeywordArgument(string member, string part)
    {
        using var context = new ProbeContext();
        var value = 1;

        var query = member switch
        {
            nameof(DateTime.Year) => context.Probes.Where(p => p.CreatedAt.Year == value),
            nameof(DateTime.Month) => context.Probes.Where(p => p.CreatedAt.Month == value),
            nameof(DateTime.Day) => context.Probes.Where(p => p.CreatedAt.Day == value),
            nameof(DateTime.Hour) => context.Probes.Where(p => p.CreatedAt.Hour == value),
            nameof(DateTime.Minute) => context.Probes.Where(p => p.CreatedAt.Minute == value),
            nameof(DateTime.Second) => context.Probes.Where(p => p.CreatedAt.Second == value),
            _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown date part.")
        };

        AssertSql(
            query,
            $"""
             SELECT "p"."Id", "p"."CreatedAt"
             FROM "Probes" AS "p"
             WHERE EXTRACT({part} FROM "p"."CreatedAt") = :__value_0
             """);
    }

    private static void AssertSql<T>(IQueryable<T> query, string expected)
        => Assert.Equal(expected, Statement(query.ToQueryString()));

    /// <summary>
    /// Drops the leading parameter comment lines, so the assertion covers the statement rather
    /// than EF Core's diagnostic formatting.
    /// </summary>
    private static string Statement(string toQueryString)
        => string.Join(
            "\n",
            toQueryString
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Where(line => !line.StartsWith("--", StringComparison.Ordinal)));

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
    }
}
