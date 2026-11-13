using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Dm.Extensions;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for the full-text methods of <see cref="DmDbFunctionsExtensions" />. The call sits in a predicate and
/// the method has no client side implementation, so the test cannot pass without the translation.
/// </summary>
public class DmFullTextSearchTranslationTests
{
    [Fact]
    public void Contains_TranslatesToContains()
    {
        using var context = new ProbeContext();
        var term = "word";

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Text"
            FROM "Probes" AS "p"
            WHERE CONTAINS("p"."Text", :term) = 1
            """,
            Statement(context.Probes.Where(p => EF.Functions.Contains(p.Text, term)).ToQueryString()));
    }

    /// <summary>
    /// The server matches a full-text index, and an index is built on a column, so anything else is refused
    /// while the query is translated instead of being sent and failing.
    /// </summary>
    [Fact]
    public void Contains_OnAnExpression_IsRefused()
    {
        using var context = new ProbeContext();

        var exception = Assert.Throws<InvalidOperationException>(
            () => context.Probes.Where(p => EF.Functions.Contains(p.Text.ToUpper(), "word")).ToQueryString());

        Assert.Contains("needs a mapped column", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The language term is written into the statement as a fragment, the way the reference provider writes
    /// it; the server takes no parameter there.
    /// </summary>
    [Fact]
    public void Contains_WithLanguage_WritesTheLanguageTerm()
    {
        using var context = new ProbeContext();
        var term = "word";

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Text"
            FROM "Probes" AS "p"
            WHERE CONTAINS("p"."Text", :term, LANGUAGE 1033) = 1
            """,
            Statement(context.Probes.Where(p => EF.Functions.Contains(p.Text, term, 1033)).ToQueryString()));
    }

    /// <summary>
    /// <c>FREETEXT</c> is provided for parity with the reference provider; 8.1.4.80 does not resolve it.
    /// </summary>
    [Fact]
    public void FreeText_TranslatesToFreeText()
    {
        using var context = new ProbeContext();
        var term = "word";

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Text"
            FROM "Probes" AS "p"
            WHERE FREETEXT("p"."Text", :term) = 1
            """,
            Statement(context.Probes.Where(p => EF.Functions.FreeText(p.Text, term)).ToQueryString()));
    }

    /// <summary>
    /// The language term is not a bindable argument, so a computed one fails while the query is translated.
    /// </summary>
    [Fact]
    public void Language_ThatIsNotAConstant_IsRefused()
    {
        using var context = new ProbeContext();
        var language = 1033;

        var exception = Assert.Throws<InvalidOperationException>(
            () => context.Probes.Where(p => EF.Functions.FreeText(p.Text, "word", language)).ToQueryString());

        Assert.Contains("has to be a constant", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>Contains</c> on an entity list is the framework's identity-equality rewrite, not a full-text
    /// call. The translator is offered every method call, so it must decline a method declared by another
    /// type even when the name matches.
    /// </summary>
    [Fact]
    public void Contains_OnAnEntityList_IsNotClaimedByTheFullTextTranslator()
    {
        using var context = new ProbeContext();
        var probes = new List<Probe> { new() { Id = 1 } };

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Text"
            FROM "Probes" AS "p"
            WHERE "p"."Id" = :entity_equality_probes_Id1
            """,
            Statement(context.Probes.Where(p => probes.Contains(p)).ToQueryString()));
    }

    /// <summary>
    /// Outside a query the method throws, so a condition the provider cannot translate fails where it is
    /// written instead of quietly matching nothing.
    /// </summary>
    [Fact]
    public void Contains_OutsideAQuery_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => EF.Functions.Contains("value", "word"));

        Assert.Contains("full-text", exception.Message, StringComparison.Ordinal);
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

        public string Text { get; set; } = string.Empty;
    }
}


