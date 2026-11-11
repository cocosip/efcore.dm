using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for the parts of the query pipeline the provider does not translate itself:
/// loading, joins, grouping, subqueries and paging. They prove the composed statement is valid
/// Dameng SQL rather than a shape borrowed from another provider.
/// </summary>
public class DmQueryPipelineTests
{
    private const string BlogColumns = """"b"."Id", "b"."Name"""";

    private static readonly Dictionary<string, Func<ProbeContext, string>> Statements = new()
    {
        ["Include"] = c => c.Blogs.Include(b => b.Posts).ToQueryString(),
        ["IncludeThenWhere"] = c => c.Blogs.Include(b => b.Posts).Where(b => b.Name == "x").ToQueryString(),
        ["Join"] = c => c.Blogs
            .Join(c.Posts, b => b.Id, p => p.BlogId, (b, p) => new { b.Name, p.Title })
            .ToQueryString(),
        ["SelectMany"] = c => c.Blogs
            .SelectMany(b => b.Posts, (b, p) => new { b.Name, p.Title })
            .ToQueryString(),
        ["GroupBy"] = c => c.Posts
            .GroupBy(p => p.BlogId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToQueryString(),
        ["Aggregates"] = c => c.Posts
            .GroupBy(p => p.BlogId)
            .Select(g => new { g.Key, Total = g.Sum(p => p.Id), Smallest = g.Min(p => p.Id), Largest = g.Max(p => p.Id) })
            .ToQueryString(),
        ["SubqueryCount"] = c => c.Blogs.Where(b => b.Posts.Count() > 2).ToQueryString(),
        ["Any"] = c => c.Blogs.Where(b => b.Posts.Any(p => p.Title == "x")).ToQueryString(),
        ["SkipTake"] = c => c.Blogs.OrderBy(b => b.Id).Skip(10).Take(5).ToQueryString(),
        ["Take"] = c => c.Blogs.OrderBy(b => b.Id).Take(5).ToQueryString(),
        ["Skip"] = c => c.Blogs.OrderBy(b => b.Id).Skip(10).ToQueryString(),
        ["LeftJoin"] = c => c.Blogs
            .LeftJoin(c.Posts, b => b.Id, p => p.BlogId, (b, p) => new { b.Name, Title = p != null ? p.Title : null })
            .ToQueryString(),
        ["RightJoin"] = c => c.Blogs
            .RightJoin(c.Posts, b => b.Id, p => p.BlogId, (b, p) => new { Name = b != null ? b.Name : null, p.Title })
            .ToQueryString(),
        ["Union"] = c => c.Blogs.Select(b => b.Id).Union(c.Posts.Select(p => p.Id)).ToQueryString(),
        ["OrderByDescending"] = c => c.Blogs.OrderByDescending(b => b.Id).ToQueryString()
    };

    [Theory]
    [InlineData("Include", """
        SELECT "b"."Id", "b"."Name", "p"."Id", "p"."BlogId", "p"."Title"
        FROM "Blogs" AS "b"
        LEFT JOIN "Posts" AS "p" ON "b"."Id" = "p"."BlogId"
        ORDER BY "b"."Id"
        """)]
    [InlineData("IncludeThenWhere", """
        SELECT "b"."Id", "b"."Name", "p"."Id", "p"."BlogId", "p"."Title"
        FROM "Blogs" AS "b"
        LEFT JOIN "Posts" AS "p" ON "b"."Id" = "p"."BlogId"
        WHERE "b"."Name" = 'x'
        ORDER BY "b"."Id"
        """)]
    [InlineData("Join", """
        SELECT "b"."Name", "p"."Title"
        FROM "Blogs" AS "b"
        INNER JOIN "Posts" AS "p" ON "b"."Id" = "p"."BlogId"
        """)]
    [InlineData("SelectMany", """
        SELECT "b"."Name", "p"."Title"
        FROM "Blogs" AS "b"
        INNER JOIN "Posts" AS "p" ON "b"."Id" = "p"."BlogId"
        """)]
    [InlineData("GroupBy", """
        SELECT "p"."BlogId" AS "Key", COUNT(*) AS "Count"
        FROM "Posts" AS "p"
        GROUP BY "p"."BlogId"
        """)]
    [InlineData("Aggregates", """
        SELECT "p"."BlogId" AS "Key", COALESCE(SUM("p"."Id"), 0) AS "Total", MIN("p"."Id") AS "Smallest", MAX("p"."Id") AS "Largest"
        FROM "Posts" AS "p"
        GROUP BY "p"."BlogId"
        """)]
    [InlineData("SubqueryCount", """
        SELECT "b"."Id", "b"."Name"
        FROM "Blogs" AS "b"
        WHERE (
            SELECT COUNT(*)
            FROM "Posts" AS "p"
            WHERE "b"."Id" = "p"."BlogId") > 2
        """)]
    [InlineData("Any", """
        SELECT "b"."Id", "b"."Name"
        FROM "Blogs" AS "b"
        WHERE EXISTS (
            SELECT 1
            FROM "Posts" AS "p"
            WHERE "b"."Id" = "p"."BlogId" AND "p"."Title" = 'x')
        """)]
    // Dameng's grammar has a ROW_LIMIT clause, `[OFFSET n ROW] [FETCH NEXT n ROW ONLY]`, so the
    // ANSI shape the relational layer emits is the documented one.
    [InlineData("SkipTake", """
        -- :p='10'
        -- :p1='5'
        SELECT "b"."Id", "b"."Name"
        FROM "Blogs" AS "b"
        ORDER BY "b"."Id"
        OFFSET :p ROWS FETCH NEXT :p1 ROWS ONLY
        """)]
    [InlineData("OrderByDescending", """
        SELECT "b"."Id", "b"."Name"
        FROM "Blogs" AS "b"
        ORDER BY "b"."Id" DESC
        """)]
    [InlineData("Take", """
        -- :p='5'
        SELECT "b"."Id", "b"."Name"
        FROM "Blogs" AS "b"
        ORDER BY "b"."Id"
        FETCH FIRST :p ROWS ONLY
        """)]
    [InlineData("Skip", """
        -- :p='10'
        SELECT "b"."Id", "b"."Name"
        FROM "Blogs" AS "b"
        ORDER BY "b"."Id"
        OFFSET :p ROWS
        """)]
    [InlineData("LeftJoin", """
        SELECT "b"."Name", "p"."Title"
        FROM "Blogs" AS "b"
        LEFT JOIN "Posts" AS "p" ON "b"."Id" = "p"."BlogId"
        """)]
    [InlineData("RightJoin", """
        SELECT "b"."Name", "p"."Title"
        FROM "Blogs" AS "b"
        RIGHT JOIN "Posts" AS "p" ON "b"."Id" = "p"."BlogId"
        """)]
    [InlineData("Union", """
        SELECT "b"."Id"
        FROM "Blogs" AS "b"
        UNION
        SELECT "p"."Id"
        FROM "Posts" AS "p"
        """)]
    public void Pipeline_ComposesValidStatement(string name, string expected)
    {
        using var context = new ProbeContext();

        Assert.Equal(
            expected.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd(),
            Statements[name](context).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
    }

    /// <summary>
    /// A split query returns only the first statement, so the assertion is that the shape is still
    /// the root query and that the provider did not fall back to a single statement.
    /// </summary>
    [Fact]
    public void SplitQuery_KeepsTheRootStatementSeparate()
    {
        using var context = new ProbeContext();

        var sql = context.Blogs.Include(b => b.Posts).AsSplitQuery().ToQueryString();

        Assert.StartsWith(
            """
            SELECT "b"."Id", "b"."Name"
            FROM "Blogs" AS "b"
            ORDER BY "b"."Id"
            """,
            sql.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Contains("split-query mode", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// A collection joined in the query is rendered by the framework as a row source of values, which
    /// Dameng spells as a <c>SELECT</c> from <c>DUAL</c> per row because it has no <c>VALUES</c> row
    /// source: measured on 8.1.4.80, both <c>(VALUES ...) AS t(x)</c> and a bare <c>VALUES</c> in a set
    /// operation answered <c>语法分析出错</c>.
    /// </summary>
    [Fact]
    public void JoinOverACollection_BecomesSelectsFromDual()
    {
        using var context = new ProbeContext();

        var ids = new[] { 1, 2 };

        Assert.Equal(
            """
            -- :p1='1'
            -- :p2='2'
            SELECT "b"."Name"
            FROM "Blogs" AS "b"
            INNER JOIN (SELECT :p1 AS "Value" FROM DUAL UNION ALL SELECT :p2 FROM DUAL) AS "p" ON "b"."Id" = "p"."Value"
            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            context.Blogs
                .Join(ids, b => b.Id, id => id, (b, id) => b.Name)
                .ToQueryString()
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .TrimEnd());
    }

    /// <summary>
    /// A join whose key is a tautology is simplified by the framework to a boolean constant, and the
    /// server rejects a value in a join predicate the same way it rejects one in a where clause.
    /// </summary>
    [Fact]
    public void JoinWithATautologyPredicate_ComparesWithOne()
    {
        using var context = new ProbeContext();

        var query = from b in context.Blogs
                    join p in context.Posts on (b.Name != null) equals true into grouping
                    from p in grouping.DefaultIfEmpty()
                    select new { b.Name, p!.Title };

        Assert.Equal(
            """
            SELECT "b"."Name", "p"."Title"
            FROM "Blogs" AS "b"
            LEFT JOIN "Posts" AS "p" ON 1 = 1
            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            query.ToQueryString().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
    }

    private sealed class ProbeContext : DbContext
    {
        public DbSet<Blog> Blogs => Set<Blog>();

        public DbSet<Post> Posts => Set<Post>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");
    }

    private sealed class Blog
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<Post> Posts { get; set; } = [];
    }

    private sealed class Post
    {
        public int Id { get; set; }

        public int BlogId { get; set; }

        public string Title { get; set; } = string.Empty;

        public Blog Blog { get; set; } = null!;
    }
}
