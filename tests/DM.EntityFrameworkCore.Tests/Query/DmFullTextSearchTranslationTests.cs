using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Dm.Extensions;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for <see cref="DmDbFunctionsExtensions.Contains" />. The call sits in a predicate and
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
