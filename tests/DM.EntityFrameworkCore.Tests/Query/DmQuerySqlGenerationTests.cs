using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests. Each assertion covers the complete generated statement, and each
/// expression sits in a position that requires server execution, so a client-side fallback
/// cannot make the test pass.
/// </summary>
public class DmQuerySqlGenerationTests
{
    [Fact]
    public void Select_WhereOrderByProjection_GeneratesExpectedSql()
    {
        using var context = new ProbeContext();

        var sql = context.Probes
            .Where(p => p.Id > 1)
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToQueryString();

        Assert.Equal(
            """
            SELECT "p"."Name"
            FROM "Probes" AS "p"
            WHERE "p"."Id" > 1
            ORDER BY "p"."Name"
            """,
            Statement(sql));
    }

    [Fact]
    public void Where_WithCapturedVariable_UsesColonPrefixedParameter()
    {
        using var context = new ProbeContext();
        var threshold = 5;

        var sql = context.Probes.Where(p => p.Id > threshold).ToQueryString();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE "p"."Id" > :__threshold_0
            """,
            Statement(sql));
    }

    [Fact]
    public void Paging_GeneratesOffsetFetch()
    {
        using var context = new ProbeContext();

        var sql = context.Probes.OrderBy(p => p.Id).Skip(10).Take(5).ToQueryString();

        // Dameng's own paging syntax is settled in roadmap phase P2. This test pins the current
        // ANSI form so that a change is visible rather than silent.
        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            ORDER BY "p"."Id"
            OFFSET :__p_0 ROWS FETCH NEXT :__p_1 ROWS ONLY
            """,
            Statement(sql));
    }

    [Fact]
    public void GroupBy_WithAggregate_GeneratesExpectedSql()
    {
        using var context = new ProbeContext();

        var sql = context.Probes
            .GroupBy(p => p.Name)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToQueryString();

        Assert.Equal(
            """
            SELECT "p"."Name" AS "Key", COUNT(*) AS "Count"
            FROM "Probes" AS "p"
            GROUP BY "p"."Name"
            """,
            Statement(sql));
    }

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

        public string Name { get; set; } = string.Empty;
    }
}
