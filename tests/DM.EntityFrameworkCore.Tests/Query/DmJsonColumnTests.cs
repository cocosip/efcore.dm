using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for a JSON column. Every member expression sits in a predicate or a projection, so
/// a client side fallback cannot make a test pass.
/// </summary>
/// <remarks>
/// Measured on 8.1.4.80: <c>JSON</c> is a type a table accepts, the driver reads and writes it as text,
/// <c>JSON_VALUE</c> answers a member as text, and the <c>RETURNING</c> form that would type it is rejected
/// with <c>返回值的数据类型无效</c> — which is why a member that is not text is cast from the text.
/// <c>JSON_QUERY</c> answers an object or an array only, so the generator does not use it.
/// </remarks>
public class DmJsonColumnTests
{
    [Fact]
    public void OwnedEntity_MapsToAJsonColumn()
    {
        using var context = new ProbeContext();

        Assert.Contains(
            "\"Details\" JSON NOT NULL",
            context.Database.GenerateCreateScript(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void StringMember_GeneratesJsonValue()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(probe => probe.Details.Name == "abc"),
            """
            SELECT "p"."Id", "p"."Details"
            FROM "Probes" AS "p"
            WHERE (JSON_VALUE("p"."Details", '$.Name')) = 'abc'
            """);
    }

    [Fact]
    public void TypedMember_IsCastFromTheText()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(probe => probe.Details.Count > 1),
            """
            SELECT "p"."Id", "p"."Details"
            FROM "Probes" AS "p"
            WHERE (CAST(JSON_VALUE("p"."Details", '$.Count') AS INT)) > 1
            """);
    }

    /// <summary>
    /// A boolean member is not cast: the server stores it as <c>true</c>, and casting that text to
    /// <c>BIT</c> answers <c>无效的数字</c>. It is compared with that text and answered as the condition
    /// shape the provider uses, which the base rewrite then compares with <c>1</c>.
    /// </summary>
    [Fact]
    public void BooleanMember_IsComparedAsText()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(probe => probe.Details.Flag),
            """
            SELECT "p"."Id", "p"."Details"
            FROM "Probes" AS "p"
            WHERE (CASE WHEN JSON_VALUE("p"."Details", '$.Flag') = 'true' THEN 1 ELSE 0 END) = 1
            """);
    }

    [Fact]
    public void MemberInAProjection_GeneratesJsonValue()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Select(probe => new { probe.Id, probe.Details.Name }),
            """
            SELECT "p"."Id", JSON_VALUE("p"."Details", '$.Name') AS "Name"
            FROM "Probes" AS "p"
            """);
    }

    /// <summary>
    /// The document itself is the column: it is selected as it is stored and the framework parses the
    /// text, so <c>JSON_VALUE(doc, '$')</c> would answer the document only to have it parsed twice.
    /// </summary>
    [Fact]
    public void DocumentInAProjection_SelectsTheColumn()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Select(probe => probe),
            """
            SELECT "p"."Id", "p"."Details"
            FROM "Probes" AS "p"
            """);
    }

    private static void AssertSql<T>(IQueryable<T> query, string expected)
        => Assert.Equal(expected, Statement(query.ToQueryString()));

    /// <summary>
    /// Drops the leading parameter comment lines, so the assertion covers the statement rather
    /// than EF Core's diagnostic formatting.
    /// </summary>
    private static string Statement(string toQueryString)
        => string.Join(
            "\n",
            toQueryString
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Where(line => !line.StartsWith("--", StringComparison.Ordinal)));

    private sealed class ProbeContext : DbContext
    {
        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(
                entity =>
                {
                    entity.HasKey(probe => probe.Id);
                    entity.OwnsOne(probe => probe.Details, owned => owned.ToJson());
                });
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public Details Details { get; set; } = new();
    }

    private sealed class Details
    {
        public string Name { get; set; } = string.Empty;

        public int Count { get; set; }

        public bool Flag { get; set; }
    }
}
