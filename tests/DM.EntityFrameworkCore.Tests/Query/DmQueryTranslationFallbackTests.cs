using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for the places a provider has to say no instead of letting the framework compose
/// a statement the server cannot answer, or has to attach a mapping the framework otherwise leaves
/// open.
/// </summary>
public class DmQueryTranslationFallbackTests
{
    /// <summary>
    /// Nested <c>Take</c> calls are composed as <c>LEAST</c>. A parameter there used to leave the
    /// function without a type mapping, which fails the statement with "does not have a type mapping
    /// assigned" before any SQL is sent.
    /// </summary>
    [Fact]
    public void NestedTake_AnswersLeastWithAMapping()
    {
        using var context = new StampedContext();

        var actual = context.Items.OrderBy(i => i.Id).Take(5).Take(3).ToQueryString().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

        Assert.Equal(
            """
            -- :p='5'
            -- :p1='3'
            SELECT "i"."Id", "i"."CreatedAt", "i"."Name"
            FROM "Items" AS "i"
            ORDER BY "i"."Id"
            FETCH FIRST LEAST(:p, :p1) ROWS ONLY
            """,
            actual);
    }

    /// <summary>
    /// A condition in a <c>CASE</c> result is a value position, and the server rejects the bare
    /// condition there (<c>语法分析出错</c> at the comparison), so it becomes a nested case.
    /// </summary>
    [Fact]
    public void AConditionInACaseResult_BecomesACaseValue()
    {
        using var context = new StampedContext();

        var actual = context.Items.Select(i => i.Name == null ? true : i.Id < 5).ToQueryString().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

        Assert.Equal(
            """
            SELECT CASE
            WHEN "i"."Name" IS NULL THEN 1
            ELSE CASE WHEN "i"."Id" < 5 THEN 1 ELSE 0 END
            END
            FROM "Items" AS "i"
            """,
            actual);
    }

    /// <summary>
    /// A difference of two dates is left to the client: the server answers it with a number of days,
    /// which cannot be read back as a <see cref="TimeSpan" />.
    /// </summary>
    [Fact]
    public void ADateSubtraction_SelectsTheDateAndLeavesTheOperatorToTheClient()
    {
        using var context = new StampedContext();

        Assert.Equal(
            """
            SELECT "i"."CreatedAt"
            FROM "Items" AS "i"
            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            context.Items.Select(i => i.CreatedAt - new DateTime(2020, 1, 1)).ToQueryString().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
    }

    private sealed class StampedContext : DbContext
    {
        public DbSet<Item> Items => Set<Item>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");
    }

    private sealed class Item
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
