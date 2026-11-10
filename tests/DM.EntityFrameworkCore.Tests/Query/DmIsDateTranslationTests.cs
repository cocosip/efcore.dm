using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Dm.Extensions;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for <see cref="DmDbFunctionsExtensions.IsDate" />. The call sits in a predicate, so
/// a client side implementation cannot make the test pass.
/// </summary>
public class DmIsDateTranslationTests
{
    [Fact]
    public void IsDate_TranslatesToIsdateComparedWithOne()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            SELECT "p"."Id", "p"."Text"
            FROM "Probes" AS "p"
            WHERE ISDATE("p"."Text") = 1
            """,
            Statement(context.Probes.Where(p => EF.Functions.IsDate(p.Text)).ToQueryString()));
    }

    /// <summary>
    /// Outside a query the method answers from the value itself, so a fallback does not throw.
    /// </summary>
    [Fact]
    public void IsDate_OutsideAQuery_AnswersFromTheValue()
    {
        Assert.True(EF.Functions.IsDate("2024-01-31"));
        Assert.False(EF.Functions.IsDate("not a date"));
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
