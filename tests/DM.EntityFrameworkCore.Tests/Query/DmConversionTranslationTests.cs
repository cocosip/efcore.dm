using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for conversion translation. Every expression sits in a predicate, so a
/// client-side fallback cannot make the test pass.
/// </summary>
public class DmConversionTranslationTests
{
    private static readonly Dictionary<string, Func<IQueryable<Probe>, IQueryable<Probe>>> Queries = new()
    {
        ["ToStringInt"] = q => q.Where(p => p.Id.ToString() == "1"),
        ["ToStringUInt"] = q => q.Where(p => ((uint)p.Id).ToString() == "1"),
        ["ToStringDecimal"] = q => q.Where(p => p.Amount.ToString() == "1"),
        ["ConvertToString"] = q => q.Where(p => Convert.ToString(p.Id) == "1"),
        ["ConvertToInt32"] = q => q.Where(p => Convert.ToInt32(p.Amount) == 1),
        ["ConvertToInt64"] = q => q.Where(p => Convert.ToInt64(p.Id) == 1L),
        ["ConvertToDecimal"] = q => q.Where(p => Convert.ToDecimal(p.Id) == 1m),
        ["ConvertToDouble"] = q => q.Where(p => Convert.ToDouble(p.Id) == 1.0),
        ["ConvertToDateTime"] = q => q.Where(p => Convert.ToDateTime(p.CreatedAt) == p.CreatedAt),
        ["ToStringThenLike"] = q => q.Where(p => p.Amount.ToString().Contains("1"))
    };

    [Theory]
    [InlineData("ToStringInt", """TO_CHAR("p"."Id") = '1'""")]
    [InlineData("ToStringUInt", """TO_CHAR(CAST("p"."Id" AS BIGINT)) = '1'""")]
    [InlineData("ToStringDecimal", """TO_CHAR("p"."Amount") = '1'""")]
    [InlineData("ConvertToString", """TO_CHAR("p"."Id") = '1'""")]
    [InlineData("ConvertToInt32", """CAST("p"."Amount" AS INT) = 1""")]
    [InlineData("ConvertToInt64", """CAST("p"."Id" AS BIGINT) = 1""")]
    [InlineData("ConvertToDecimal", """CAST("p"."Id" AS DECIMAL(29,4)) = 1.0""")]
    [InlineData("ConvertToDouble", """CAST("p"."Id" AS DOUBLE) = 1.0""")]
    [InlineData("ConvertToDateTime", "CAST(\"p\".\"CreatedAt\" AS TIMESTAMP) = \"p\".\"CreatedAt\"")]
    [InlineData("ToStringThenLike", """TO_CHAR("p"."Amount") LIKE '%1%' ESCAPE '\'""")]
    public void Conversion_TranslatesToCastOrFormat(string name, string predicate)
    {
        using var context = new ProbeContext();

        Assert.Equal(
            $"""
             SELECT "p"."Id", "p"."Amount", "p"."CreatedAt"
             FROM "Probes" AS "p"
             WHERE {predicate}
             """,
            Statement(Queries[name](context.Probes).ToQueryString()));
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

        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
