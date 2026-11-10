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

    [Fact]
    public void PadLeft_GeneratesLpad()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.PadLeft(5) == "x"),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE LPAD("p"."Name", 5) = 'x'
            """);
    }

    [Fact]
    public void PadLeft_WithPaddingCharacter_GeneratesLpadWithTheSet()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.PadLeft(5, '0') == "x"),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE LPAD("p"."Name", 5, '0') = 'x'
            """);
    }

    [Fact]
    public void PadRight_GeneratesRpad()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.PadRight(5) == "x"),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE RPAD("p"."Name", 5) = 'x'
            """);
    }

    /// <summary>
    /// Dameng's INSTR is one-based and answers zero for a value it did not find, so the shift to
    /// .NET's zero-based index also turns "not found" into -1.
    /// </summary>
    [Fact]
    public void IndexOf_ShiftsToTheZeroBasedIndex()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.IndexOf("x") > 0),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE (INSTR("p"."Name", 'x') - 1) > 0
            """);
    }

    [Fact]
    public void TrimStart_WithCharacter_GeneratesLtrimWithTheSet()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.TrimStart('x') == "y"),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE LTRIM("p"."Name", 'x') = 'y'
            """);
    }

    [Fact]
    public void TrimEnd_WithCharacter_GeneratesRtrimWithTheSet()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.TrimEnd('x') == "y"),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE RTRIM("p"."Name", 'x') = 'y'
            """);
    }

    [Fact]
    public void Remove_KeepsThePartBeforeTheIndex()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.Remove(3) == "x"),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE SUBSTR("p"."Name", 1, 3) = 'x'
            """);
    }

    [Fact]
    public void Remove_WithCount_JoinsThePartAfterTheRemovedRange()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.Remove(3, 2) == "x"),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE (SUBSTR("p"."Name", 1, 3) || SUBSTR("p"."Name", 6)) = 'x'
            """);
    }

    [Fact]
    public void Insert_JoinsTheValueBetweenTheTwoParts()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Name.Insert(3, "y") == "x"),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE ((SUBSTR("p"."Name", 1, 3) || 'y') || SUBSTR("p"."Name", 4)) = 'x'
            """);
    }

    /// <summary>
    /// The column is required here, so the null tests are dropped and only the empty test is left.
    /// The nullable column below keeps them.
    /// </summary>
    [Fact]
    public void IsNullOrWhiteSpace_OnARequiredColumn_KeepsTheEmptyTestOnly()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => string.IsNullOrWhiteSpace(p.Name)),
            """
            SELECT "p"."Id", "p"."Name"
            FROM "Probes" AS "p"
            WHERE TRIM("p"."Name") = ''
            """);
    }

    /// <summary>
    /// An empty string is a value of its own in one Dameng mode and NULL in another, so both are
    /// tested and the one that does not hold never matches.
    /// </summary>
    [Fact]
    public void IsNullOrWhiteSpace_OnANullableColumn_TestsNullAndTheTrimmedValue()
    {
        using var context = new NullableContext();

        AssertSql(
            context.Names.Where(name => string.IsNullOrWhiteSpace(name.Value)),
            """
            SELECT "n"."Id", "n"."Value"
            FROM "Names" AS "n"
            WHERE "n"."Value" IS NULL OR TRIM("n"."Value") = ''
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

    private sealed class NullableContext : DbContext
    {
        public DbSet<NullableProbe> Names => Set<NullableProbe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");
    }

    private sealed class NullableProbe
    {
        public int Id { get; set; }

        public string? Value { get; set; }
    }
}
