using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape test for a collection parameter. EF Core translates the collection to a single JSON
/// parameter, which the provider expands with the server's <c>JSON_TABLE</c>; the predicate is the only
/// place the collection is used, so the test fails if the values are pulled to the client.
/// </summary>
public class DmCollectionParameterTests
{
    [Fact]
    public void Contains_ExpandsTheCollectionThroughJsonTable()
    {
        using var context = new ParameterContext();
        var ids = new[] { 1, 2 };

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Text"
            FROM "Probes" AS "p"
            WHERE "p"."Id" IN (
                SELECT "i"."value"
                FROM JSON_TABLE(:__ids_0, '$[*]' COLUMNS ("value" INT PATH '$', "rn" FOR ORDINALITY)) AS "i"
            )
            """,
            Statement(context.Probes.Where(p => ids.Contains(p.Id)).ToQueryString()));
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

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Text { get; set; } = string.Empty;
    }
}
