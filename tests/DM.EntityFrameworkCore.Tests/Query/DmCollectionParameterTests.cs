using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for a collection parameter. On this line EF Core translates the collection to a
/// single JSON parameter, which the provider expands with the server's <c>JSON_TABLE</c>; the constant
/// mode writes the values as literals instead. The predicate is the only place the collection is used,
/// so the test fails if the values are pulled to the client.
/// </summary>
/// <remarks>
/// Each mode has its own context type. The translation is cached against the compiled query, so two
/// contexts that share a model but differ only in the collection mode would reuse the first translation.
/// </remarks>
public class DmCollectionParameterTests
{
    [Fact]
    public void Contains_WithTheDefaultMode_ExpandsTheCollectionThroughJsonTable()
    {
        using var context = new ParameterContext();
        var ids = new[] { 1, 2 };

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Text"
            FROM "Probes" AS "p"
            WHERE "p"."Id" IN (
                SELECT "i"."value"
                FROM JSON_TABLE(:__ids_0, '$[*]' COLUMNS ("value" INT PATH '$')) AS "i"
            )
            """,
            Statement(context.Probes.Where(p => ids.Contains(p.Id)).ToQueryString()));
    }

    [Fact]
    public void Contains_WithTheConstantMode_RendersTheValuesAsLiterals()
    {
        using var context = new ConstantContext();
        var ids = new[] { 1, 2 };

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Text"
            FROM "Probes" AS "p"
            WHERE "p"."Id" IN (1, 2)
            """,
            Statement(context.Probes.Where(p => ids.Contains(p.Id)).ToQueryString()));
    }

    /// <summary>
    /// A string collection is written with its values quoted, which the integer case does not exercise.
    /// </summary>
    [Fact]
    public void Contains_WithStringsInTheConstantMode_QuotesEachValue()
    {
        using var context = new ConstantContext();
        var names = new[] { "a", "b" };

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Text"
            FROM "Probes" AS "p"
            WHERE "p"."Text" IN ('a', 'b')
            """,
            Statement(context.Probes.Where(p => names.Contains(p.Text)).ToQueryString()));
    }

    private static string Statement(string sql)
        => string.Join(
            "\n",
            sql.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Where(line => !line.StartsWith("--", StringComparison.Ordinal))
                .Select(line => line.TrimEnd()));

    private sealed class ParameterContext : DbContext
    {
        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");
    }

    private sealed class ConstantContext : DbContext
    {
        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(
                "Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;",
                options => options.TranslateParameterizedCollectionsToConstants());
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Text { get; set; } = string.Empty;
    }
}
