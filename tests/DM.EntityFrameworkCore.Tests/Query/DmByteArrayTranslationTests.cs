using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 SQL shape tests for a byte array. Every expression sits in a predicate, so a client side fallback
/// cannot make the test pass.
/// </summary>
/// <remarks>
/// The function is `LENGTHB` rather than `LENGTH` because of what the server answered: measured on
/// 8.1.4.80, `LENGTH` of a `VARBINARY(64)` holding four bytes was 8, the length of its hexadecimal form,
/// and the byte count of a `BLOB` was 4, while `LENGTHB` answered 4 for both.
/// <para>
/// `Payload.Length > 0` and `Payload.Any()` reach a translator as `Enumerable.Any`, and
/// `Payload.Count()` as `Enumerable.Count`. A comparison against another count reaches it as
/// `ArrayLength`, which the translating visitor reads here, so every form of the count translates.
/// </para>
/// </remarks>
public class DmByteArrayTranslationTests
{
    [Fact]
    public void ArrayLength_GeneratesLengthB()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Payload.Length > 0),
            """
            SELECT "p"."Id", "p"."Payload"
            FROM "Probes" AS "p"
            WHERE LENGTHB("p"."Payload") > 0
            """);
    }

    [Fact]
    public void ArrayAny_GeneratesAByteCountComparison()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Payload.Any()),
            """
            SELECT "p"."Id", "p"."Payload"
            FROM "Probes" AS "p"
            WHERE LENGTHB("p"."Payload") > 0
            """);
    }

    [Fact]
    public void ArrayLengthComparison_GeneratesAByteCountComparison()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Payload.Length == 4),
            """
            SELECT "p"."Id", "p"."Payload"
            FROM "Probes" AS "p"
            WHERE LENGTHB("p"."Payload") = 4
            """);
    }

    [Fact]
    public void ArrayLengthInAProjection_GeneratesAByteCount()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Select(p => p.Payload.Length),
            """
            SELECT LENGTHB("p"."Payload")
            FROM "Probes" AS "p"
            """);
    }

    [Fact]
    public void ArrayCount_GeneratesAByteCount()
    {
        using var context = new ProbeContext();

        AssertSql(
            context.Probes.Where(p => p.Payload.Count() == 4),
            """
            SELECT "p"."Id", "p"."Payload"
            FROM "Probes" AS "p"
            WHERE LENGTHB("p"."Payload") = 4
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
        {
            modelBuilder.Entity<Probe>(entity => entity.Property(probe => probe.Payload).HasMaxLength(64));
        }
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public byte[] Payload { get; set; } = [];
    }
}
