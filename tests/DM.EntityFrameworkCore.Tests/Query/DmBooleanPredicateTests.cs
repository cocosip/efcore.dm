using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for a boolean expression in a predicate position. Dameng accepts a condition
/// there, not a value: <c>WHERE "Flag"</c>, <c>WHERE NOT "Flag"</c> and <c>WHERE "Flag" AND "Other"</c>
/// are each rejected with <c>查询使用值表达式作为过滤条件</c>, while the same column compared with
/// <c>= 1</c> works. EF Core renders the column bare because SQL Server and PostgreSQL accept it, so the
/// provider rewrites the predicate rather than the column.
/// </summary>
public class DmBooleanPredicateTests
{
    [Fact]
    public void ABooleanColumnUsedAsAPredicate_IsComparedWithOne()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Flag", "p"."Name"
            FROM "Probes" AS "p"
            WHERE "p"."Flag" = 1
            """,
            Statement(context.Probes.Where(p => p.Flag).ToQueryString()));
    }

    [Fact]
    public void ANegatedBooleanColumn_ComparesTheColumnBeforeNegatingIt()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Flag", "p"."Name"
            FROM "Probes" AS "p"
            WHERE NOT ("p"."Flag" = 1)
            """,
            Statement(context.Probes.Where(p => !p.Flag).ToQueryString()));
    }

    [Fact]
    public void ACombinedPredicate_ComparesEachBooleanColumn()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Flag", "p"."Name"
            FROM "Probes" AS "p"
            WHERE "p"."Flag" = 1 AND "p"."Id" > 1
            """,
            Statement(context.Probes.Where(p => p.Flag && p.Id > 1).ToQueryString()));
    }

    /// <summary>
    /// The predicate is rebuilt rather than amended, so the rest of the statement has to survive it.
    /// </summary>
    [Fact]
    public void TheRestOfTheStatement_SurvivesTheRewrite()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT DISTINCT "p"."Name"
            FROM "Probes" AS "p"
            WHERE "p"."Flag" = 1
            """,
            Statement(context.Probes.Where(p => p.Flag).Select(p => p.Name).Distinct().ToQueryString()));
    }

    /// <summary>
    /// A condition is not a value, so a comparison against one is written as the <c>CASE</c> that answers
    /// <c>1</c> or <c>0</c>. The server rejects the condition itself as an operand with <c>语法分析出错</c>.
    /// </summary>
    [Fact]
    public void AConditionComparedWithAColumn_BecomesACaseExpression()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Flag", "p"."Name"
            FROM "Probes" AS "p"
            WHERE "p"."Flag" = CASE WHEN "p"."Id" > 1 THEN 1 ELSE 0 END
            """,
            Statement(context.Probes.Where(p => p.Flag == (p.Id > 1)).ToQueryString()));
    }

    [Fact]
    public void AConditionComparedWithAnotherCondition_WrapsBoth()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Flag", "p"."Name"
            FROM "Probes" AS "p"
            WHERE CASE WHEN "p"."Id" > 1 THEN 1 ELSE 0 END = CASE WHEN "p"."Id" > 2 THEN 1 ELSE 0 END
            """,
            Statement(context.Probes.Where(p => (p.Id > 1) == (p.Id > 2)).ToQueryString()));
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