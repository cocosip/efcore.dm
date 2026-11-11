using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for a boolean expression that lands in a select list. Dameng has no boolean
/// type to select, so a condition is answered as <c>1</c> or <c>0</c> through a <c>CASE</c>; a
/// statement that selects <c>EXISTS (...)</c> or <c>x = y</c> directly is rejected with a syntax
/// error, which is what the first run of the acceptance console measured on a server.
/// </summary>
public class DmBooleanProjectionTests
{
    [Fact]
    public void ComparisonInAProjection_BecomesACaseExpression()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", CASE WHEN "p"."Id" > 1 THEN 1 ELSE 0 END AS "Flag"
            FROM "Probes" AS "p"
            """,
            Statement(
                context.Probes
                    .Select(p => new { p.Id, Flag = p.Id > 1 })
                    .ToQueryString()));
    }

    [Fact]
    public void NullCheckInAProjection_BecomesACaseExpression()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", CASE WHEN "p"."Name" IS NOT NULL THEN 1 ELSE 0 END AS "Flag"
            FROM "Probes" AS "p"
            """,
            Statement(
                context.Probes
                    .Select(p => new { p.Id, Flag = p.Name != null })
                    .ToQueryString()));
    }

    [Fact]
    public void ExistsInAProjection_BecomesACaseExpression()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", CASE WHEN EXISTS (
                SELECT 1
                FROM "Probes" AS "p0"
                WHERE "p0"."Id" = "p"."Id") THEN 1 ELSE 0 END AS "Flag"
            FROM "Probes" AS "p"
            """,
            Statement(
                context.Probes
                    .Select(p => new { p.Id, Flag = context.Probes.Any(q => q.Id == p.Id) })
                    .ToQueryString()));
    }

    [Fact]
    public void ABooleanColumnInAProjection_IsSelectedAsItIs()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Flag"
            FROM "Probes" AS "p"
            """,
            Statement(
                context.Probes
                    .Select(p => new { p.Id, p.Flag })
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

        public string? Name { get; set; }

        public bool Flag { get; set; }
    }
}
