using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for <see cref="Math" /> translation. Every expression sits in a predicate,
/// so a client-side fallback cannot make the test pass.
/// </summary>
public class DmMathTranslationTests
{
    private static readonly Dictionary<string, Func<IQueryable<Probe>, IQueryable<Probe>>> Queries = new()
    {
        ["Abs"] = q => q.Where(p => Math.Abs(p.Amount) > 1),
        ["Acos"] = q => q.Where(p => Math.Acos(p.Rate) > 1),
        ["Asin"] = q => q.Where(p => Math.Asin(p.Rate) > 1),
        ["Atan"] = q => q.Where(p => Math.Atan(p.Rate) > 1),
        ["Ceiling"] = q => q.Where(p => Math.Ceiling(p.Amount) > 1),
        ["Cos"] = q => q.Where(p => Math.Cos(p.Rate) > 1),
        ["Exp"] = q => q.Where(p => Math.Exp(p.Rate) > 1),
        ["Floor"] = q => q.Where(p => Math.Floor(p.Amount) > 1),
        ["Round"] = q => q.Where(p => Math.Round(p.Amount) > 1),
        ["RoundDigits"] = q => q.Where(p => Math.Round(p.Amount, 2) > 1),
        ["RoundMidpoint"] = q => q.Where(p => Math.Round(p.Amount, 2, MidpointRounding.AwayFromZero) > 1),
        ["Pow"] = q => q.Where(p => Math.Pow(p.Rate, 2) > 1),
        ["Sin"] = q => q.Where(p => Math.Sin(p.Rate) > 1),
        ["Sqrt"] = q => q.Where(p => Math.Sqrt(p.Rate) > 1),
        ["Tan"] = q => q.Where(p => Math.Tan(p.Rate) > 1),
        ["Log"] = q => q.Where(p => Math.Log(p.Rate) > 1),
        ["LogBase"] = q => q.Where(p => Math.Log(p.Rate, 2) > 1),
        ["Log10"] = q => q.Where(p => Math.Log10(p.Rate) > 1),
        ["Truncate"] = q => q.Where(p => Math.Truncate(p.Amount) > 1),
        ["Sign"] = q => q.Where(p => Math.Sign(p.Amount) > 1),
        ["AbsInt"] = q => q.Where(p => Math.Abs(p.Id) > 1),
        ["Max"] = q => q.Where(p => Math.Max(p.Id, 3) > 1),
        ["Min"] = q => q.Where(p => Math.Min(p.Id, 3) > 1)
    };

    [Theory]
    [InlineData("Abs", """ABS("p"."Amount") > 1.0""")]
    [InlineData("Acos", """ACOS("p"."Rate") > 1.0""")]
    [InlineData("Asin", """ASIN("p"."Rate") > 1.0""")]
    [InlineData("Atan", """ATAN("p"."Rate") > 1.0""")]
    [InlineData("Ceiling", """CEIL("p"."Amount") > 1.0""")]
    [InlineData("Cos", """COS("p"."Rate") > 1.0""")]
    [InlineData("Exp", """EXP("p"."Rate") > 1.0""")]
    [InlineData("Floor", """FLOOR("p"."Amount") > 1.0""")]
    [InlineData("Round", """ROUND("p"."Amount") > 1.0""")]
    [InlineData("RoundDigits", """ROUND("p"."Amount", 2) > 1.0""")]
    [InlineData("RoundMidpoint", """ROUND("p"."Amount") > 1.0""")]
    [InlineData("Pow", """POWER("p"."Rate", 2.0) > 1.0""")]
    [InlineData("Sin", """SIN("p"."Rate") > 1.0""")]
    [InlineData("Sqrt", """SQRT("p"."Rate") > 1.0""")]
    [InlineData("Tan", """TAN("p"."Rate") > 1.0""")]
    [InlineData("Log", """LN("p"."Rate") > 1.0""")]
    [InlineData("LogBase", """LOG(2.0, "p"."Rate") > 1.0""")]
    [InlineData("Log10", """LOG(10, "p"."Rate") > 1.0""")]
    [InlineData("Truncate", """TRUNC("p"."Amount") > 1.0""")]
    [InlineData("Sign", """SIGN("p"."Amount") > 1""")]
    [InlineData("AbsInt", """ABS("p"."Id") > 1""")]
    [InlineData("Max", """GREATEST("p"."Id", 3) > 1""")]
    [InlineData("Min", """LEAST("p"."Id", 3) > 1""")]
    public void MathCall_TranslatesToDamengFunction(string name, string predicate)
    {
        using var context = new ProbeContext();

        Assert.Equal(
            $"""
             SELECT "p"."Id", "p"."Amount", "p"."Rate"
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

        public double Rate { get; set; }
    }
}
