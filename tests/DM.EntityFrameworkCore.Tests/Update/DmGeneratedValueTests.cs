using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Update;

/// <summary>
/// T2 tests for reading a store-generated value back after an insert. They run against a recording
/// connection, so they pin the statement the provider issues and the value EF Core ends up with,
/// without a server.
/// </summary>
public class DmGeneratedValueTests
{
    [Fact]
    public void Insert_ReadsTheGeneratedKeyBack()
    {
        var connection = new FakeDmConnection { ReadBackColumns = ["Id"], ReadBackRow = [42] };

        using var context = new ProbeContext(connection);
        var probe = new Probe { Name = "a" };
        context.Add(probe);

        context.SaveChanges();

        Assert.Equal(42, probe.Id);
        Assert.Equal(
            Sql(
                """
                INSERT INTO "Probes" ("Name")
                VALUES (:p0);
                SELECT "Id"
                FROM "Probes"
                WHERE sql%ROWCOUNT = 1 AND "Id" = scope_identity();
                """),
            Normalize(connection.Commands.Single()));
    }

    /// <summary>
    /// A second key column that is known before the insert identifies the row by its parameter, so
    /// the row does not have to be found through the identity function.
    /// </summary>
    [Fact]
    public void Insert_WithAKeyKnownBeforeTheInsert_MatchesItByParameter()
    {
        var connection = new FakeDmConnection { ReadBackColumns = ["Id"], ReadBackRow = [7] };

        using var context = new CompositeKeyContext(connection);
        var probe = new CompositeKeyProbe { Tenant = "t" };
        context.Add(probe);

        context.SaveChanges();

        Assert.Equal(7, probe.Id);
        Assert.Contains("AND \"Tenant\" = :", connection.Commands.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InsertAsync_ReadsTheGeneratedKeyBack()
    {
        var connection = new FakeDmConnection { ReadBackColumns = ["Id"], ReadBackRow = [42] };

        await using var context = new ProbeContext(connection);
        var probe = new Probe { Name = "a" };
        context.Add(probe);

        await context.SaveChangesAsync();

        Assert.Equal(42, probe.Id);
    }

    /// <summary>
    /// When the insert affects no row the row cannot be read, and EF Core reports that as a
    /// concurrency failure rather than leaving the key at zero.
    /// </summary>
    [Fact]
    public void Insert_ThatAffectsNoRow_ReportsAConcurrencyFailure()
    {
        var connection = new FakeDmConnection { RowsAffected = 0, ReadBackColumns = ["Id"], ReadBackRow = null };

        using var context = new ProbeContext(connection);
        context.Add(new Probe { Name = "a" });

        Assert.Throws<DbUpdateConcurrencyException>(() => context.SaveChanges());
    }

    /// <summary>
    /// A key the store fills from a sequence default is found through that sequence's current value,
    /// which is the default expression with NEXTVAL replaced, so the row read back is the one this
    /// session just inserted.
    /// </summary>
    [Fact]
    public void Insert_WithASequenceDefaultKey_ReadsItBackThroughCurrval()
    {
        var connection = new FakeDmConnection { ReadBackColumns = ["Id"], ReadBackRow = [9] };

        using var context = new SequenceKeyContext(connection);
        var probe = new SequenceKeyProbe { Name = "a" };
        context.Add(probe);

        context.SaveChanges();

        Assert.Equal(9, probe.Id);
        Assert.Equal(
            Sql(
                """
                INSERT INTO "Probes" ("Name")
                VALUES (:p0);
                SELECT "Id"
                FROM "Probes"
                WHERE sql%ROWCOUNT = 1 AND "Id" = seq_probe.CURRVAL;
                """),
            Normalize(connection.Commands.Single()));
    }

    /// <summary>
    /// The column carries the default expression instead of an identity attribute: Dameng allows one
    /// identity column per table, and the key already has a source.
    /// </summary>
    [Fact]
    public void SequenceDefaultKey_ProducesADefaultAndNoIdentity()
    {
        using var context = new SequenceKeyContext(new FakeDmConnection { RowsAffected = 1 });

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("DEFAULT (seq_probe.NEXTVAL)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("IDENTITY", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// A default expression that does not advance a sequence leaves no way to identify the row, so
    /// the insert is rejected rather than matching it with the identity function.
    /// </summary>
    [Fact]
    public void Insert_WithADefaultExpressionThatIdentifiesNoSequence_IsRejected()
    {
        var connection = new FakeDmConnection { RowsAffected = 1 };

        using var context = new ConstantKeyContext(connection);
        context.Add(new SequenceKeyProbe { Name = "a" });

        var exception = Assert.Throws<NotSupportedException>(() => context.SaveChanges());

        Assert.Contains("cannot read back", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Insert_WithoutAGeneratedValue_IssuesOneStatement()
    {
        var connection = new FakeDmConnection { RowsAffected = 1 };

        using var context = new AssignedKeyContext(connection);
        context.Add(new AssignedKeyProbe { Id = 3, Name = "a" });

        context.SaveChanges();

        Assert.DoesNotContain("SELECT", connection.Commands.Single(), StringComparison.Ordinal);
    }

    private static string Sql(string sql) => Normalize(sql);

    private static string Normalize(string sql) => sql.ReplaceLineEndings("\n").TrimEnd();

    private class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public ProbeContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(e => e.Property(p => p.Name).HasMaxLength(50));
    }

    private sealed class CompositeKeyContext : ProbeContext
    {
        public CompositeKeyContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompositeKeyProbe>(
                e =>
                {
                    e.HasKey(p => new { p.Tenant, p.Id });
                    e.Property(p => p.Tenant).HasMaxLength(10);
                    e.Property(p => p.Id).ValueGeneratedOnAdd();
                });
    }

    private sealed class AssignedKeyContext : ProbeContext
    {
        public AssignedKeyContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AssignedKeyProbe>(
                e =>
                {
                    e.Property(p => p.Id).ValueGeneratedNever();
                    e.Property(p => p.Name).HasMaxLength(50);
                });
    }

    private sealed class SequenceKeyContext : SequenceKeyProbeContext
    {
        public SequenceKeyContext(FakeDmConnection connection)
            : base(connection, "seq_probe.NEXTVAL")
        {
        }
    }

    private sealed class ConstantKeyContext : SequenceKeyProbeContext
    {
        public ConstantKeyContext(FakeDmConnection connection)
            : base(connection, "42")
        {
        }
    }

    private abstract class SequenceKeyProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;
        private readonly string _defaultSql;

        protected SequenceKeyProbeContext(FakeDmConnection connection, string defaultSql)
        {
            _connection = connection;
            _defaultSql = defaultSql;
        }

        public DbSet<SequenceKeyProbe> Probes => Set<SequenceKeyProbe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SequenceKeyProbe>(
                e =>
                {
                    e.Property(p => p.Id).HasDefaultValueSql(_defaultSql);
                    e.Property(p => p.Name).HasMaxLength(50);
                });
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class CompositeKeyProbe
    {
        public string Tenant { get; set; } = string.Empty;

        public int Id { get; set; }
    }

    private sealed class SequenceKeyProbe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class AssignedKeyProbe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
