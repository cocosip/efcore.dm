using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for string translation. Every expression sits in a predicate, so a
/// client-side fallback cannot make the test pass.
/// </summary>
public class DmStringTranslationTests
{
    [Fact]
    public void Contains_WithConstant_GeneratesLikeWithEscapedWildcards()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.Contains("a")),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE "p"."Name" LIKE '%a%' ESCAPE '\'
            """);
    }

    [Fact]
    public void StartsWith_WithConstant_GeneratesTrailingWildcardOnly()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.StartsWith("a")),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE "p"."Name" LIKE 'a%' ESCAPE '\'
            """);
    }

    [Fact]
    public void EndsWith_WithConstant_GeneratesLeadingWildcardOnly()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.EndsWith("a")),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE "p"."Name" LIKE '%a' ESCAPE '\'
            """);
    }

    [Fact]
    public void Contains_WithParameter_ConcatenatesWildcards()
    {
        using var context = new ProbeContext();
        var term = "a";

        AssertSql(
            context.Probes.Where(p => p.Name.Contains(term)),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE "p"."Name" LIKE CONCAT(CONCAT('%', :term), '%') ESCAPE '\'
            """);
    }

    [Fact]
    public void Contains_WithWildcardInValue_EscapesTheWildcard()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.Contains("50%")),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE "p"."Name" LIKE '%50\%%' ESCAPE '\'
            """);
    }

    [Fact]
    public void ToUpper_GeneratesUpperFunction()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.ToUpper() == "A"),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE UPPER("p"."Name") = 'A'
            """);
    }

    [Fact]
    public void Length_GeneratesLengthFunction()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.Length > 2),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE LENGTH("p"."Name") > 2
            """);
    }

    [Fact]
    public void Substring_ShiftsToTheOneBasedIndex()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.Substring(1, 2) == "ab"),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE SUBSTR("p"."Name", 2, 2) = 'ab'
            """);
    }

    [Fact]
    public void Replace_GeneratesReplaceFunction()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.Replace("a", "b") == "c"),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE REPLACE("p"."Name", 'a', 'b') = 'c'
            """);
    }

    [Fact]
    public void StringConcatenation_UsesTheDoublePipeOperator()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Select(p => p.Name + p.Name),
            """
            SELECT "p"."Name" || "p"."Name"
            FROM "Probes" AS "p"
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

        public string Name { get; set; } = string.Empty;
    }
}
