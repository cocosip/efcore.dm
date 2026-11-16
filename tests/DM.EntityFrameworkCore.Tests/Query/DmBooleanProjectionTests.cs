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

    /// <summary>
    /// The <c>WHEN</c> test is a condition position, so a boolean value there is compared with 1 like
    /// the predicate positions; <c>CASE WHEN "p"."Flag"</c> is the value-expression form the server
    /// rejects.
    /// </summary>
    [Fact]
    public void ABooleanValueInACaseTest_IsComparedWithOne()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", CASE
            WHEN "p"."Flag" = 1 THEN 1
            ELSE 0
            END AS "Value"
            FROM "Probes" AS "p"
            """,
            Statement(
                context.Probes
                    .Select(p => new { p.Id, Value = p.Flag ? 1 : 0 })
                    .ToQueryString()));
    }

    /// <summary>
    /// The server rejects a condition as an ordering key (measured: <c>ORDER BY "p"."Id" &gt; 1</c>
    /// answered <c>[-2007] 语法分析出错</c>), so the key is written as the CASE that answers a value.
    /// A boolean column is a value already and keeps the server's own NULL ordering.
    /// </summary>
    [Fact]
    public void AConditionAsAnOrderingKey_BecomesACaseExpression()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id"
            FROM "Probes" AS "p"
            ORDER BY CASE WHEN "p"."Id" > 1 THEN 1 ELSE 0 END
            """,
            Statement(context.Probes.OrderBy(p => p.Id > 1).Select(p => p.Id).ToQueryString()));
    }

    [Fact]
    public void ABooleanColumnAsAnOrderingKey_StaysAColumn()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id"
            FROM "Probes" AS "p"
            ORDER BY "p"."Flag"
            """,
            Statement(context.Probes.OrderBy(p => p.Flag).Select(p => p.Id).ToQueryString()));
    }

    /// <summary>
    /// A parameter-list key can arrive wrapped in <c>COALESCE(key, false)</c>, which the server rejects
    /// in <c>ORDER BY</c>; opening the wrapper leaves the predicate the CASE states as a value.
    /// </summary>
    [Fact]
    public void ACoalescedConditionAsAnOrderingKey_BecomesACaseExpression()
    {
        using var context = new CustomerContext();
        var list = new List<string> { "a", "b" };

        Assert.Equal(
            """
            SELECT "c"."CustomerID"
            FROM "Customers" AS "c"
            ORDER BY CASE WHEN "c"."CustomerID" IN (
                SELECT "l"."value"
                FROM JSON_TABLE(:__list_0, '$[*]' COLUMNS ("value" NCLOB PATH '$', "rn" FOR ORDINALITY)) AS "l"
            ) THEN 1 ELSE 0 END
            """,
            Statement(context.Customers.OrderBy(c => list.Contains(c.CustomerID)).Select(c => c.CustomerID).ToQueryString()));
    }
    private static string Statement(string sql)
        => string.Join(
            "\n",
            sql.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Where(line => !line.StartsWith("--", StringComparison.Ordinal))
                .Select(line => line.TrimEnd()));

    private sealed class CustomerContext : DbContext
    {
        public DbSet<Customer> Customers => Set<Customer>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");
    }

    private sealed class Customer
    {
        public string CustomerID { get; set; } = string.Empty;

        public string? Region { get; set; }
    }
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


