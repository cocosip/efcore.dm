using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape test for a primitive collection. EF Core hands the provider the collection column through
/// <c>TranslatePrimitiveCollection</c>; what is pinned here is the row source the provider answers with, so
/// a regression that drops the translation fails without a server.
/// </summary>
public class DmPrimitiveCollectionTests
{
    [Fact]
    public void Contains_ExpandsTheCollectionWithJsonTable()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "t"."Id", "t"."Tags"
            FROM "Tagged" AS "t"
            WHERE 'alpha' IN (
                SELECT "t0"."value"
                FROM JSON_TABLE("t"."Tags", '$[*]' COLUMNS ("value" NCLOB PATH '$', "rn" FOR ORDINALITY)) AS "t0"
            )
            """,
            Sql(context.Tagged.Where(t => t.Tags.Contains("alpha")).ToQueryString()));
    }

    [Fact]
    public void Contains_ExpandsAnArrayCollectionWithJsonTable()
    {
        using var context = new ArrayProbeContext();

        Assert.Equal(
            """
            SELECT "a"."Id", "a"."Codes"
            FROM "ArrayTagged" AS "a"
            WHERE 'alpha' IN (
                SELECT "c"."value"
                FROM JSON_TABLE("a"."Codes", '$[*]' COLUMNS ("value" NCLOB PATH '$', "rn" FOR ORDINALITY)) AS "c"
            )
            """,
            Sql(context.ArrayTagged.Where(t => t.Codes.Contains("alpha")).ToQueryString()));
    }

    private static string Sql(string sql)
        => string.Join(
            "\n",
            sql.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Where(line => !line.StartsWith("--", StringComparison.Ordinal))
                .Select(line => line.TrimEnd()));

    private sealed class ProbeContext : DbContext
    {
        public DbSet<Tagged> Tagged => Set<Tagged>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");
    }

    private sealed class Tagged
    {
        public int Id { get; set; }

        public List<string> Tags { get; set; } = [];
    }

    private sealed class ArrayProbeContext : DbContext
    {
        public DbSet<ArrayTagged> ArrayTagged => Set<ArrayTagged>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");
    }

    private sealed class ArrayTagged
    {
        public int Id { get; set; }

        public string[] Codes { get; set; } = [];
    }
}
