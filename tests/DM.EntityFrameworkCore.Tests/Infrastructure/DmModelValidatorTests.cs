using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// T1 tests for the model validation. They build the model and nothing else, so they pin the rules
/// the provider refuses to send to the server.
/// </summary>
/// <remarks>
/// Each scenario has its own context type, because EF Core caches the model per context type.
/// </remarks>
public class DmModelValidatorTests
{
    [Fact]
    public void TwoIdentityColumnsInOneTable_AreRejected()
    {
        using var context = new TwoIdentityContext(new FakeDmConnection { RowsAffected = 1 });

        var exception = Assert.ThrowsAny<Exception>(() => context.Model);

        Assert.Contains("one identity column per table", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The check is per table, not per entity, so two entity types that share a table are covered by
    /// the same rule.
    /// </summary>

    /// <summary>
    /// Measured on 8.1.4.80: `ROWVERSION` is not a type (`非法的基类名[ROWVERSION]`), `ORA_ROWSCN` does not
    /// exist, and `ROWID` does not change when a row is updated. A property the model marks as generated on
    /// every write therefore describes a token the store never changes, which would leave the concurrency
    /// check passing on a stale row, so the model is refused with the shape that does work.
    /// </summary>
    [Fact]
    public void RowVersionColumn_IsRejected()
    {
        using var context = new RowVersionContext(new FakeDmConnection { RowsAffected = 1 });

        var exception = Assert.ThrowsAny<Exception>(() => context.Model);

        Assert.Contains("no column that generates a new value on every write", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A concurrency token the application maintains is the shape that works, and it stays accepted.
    /// </summary>
    [Fact]
    public void ApplicationMaintainedConcurrencyToken_IsAccepted()
    {
        using var context = new ApplicationTokenContext(new FakeDmConnection { RowsAffected = 1 });

        var version = context.Model.FindEntityType(typeof(TokenProbe))!.FindProperty(nameof(TokenProbe.Version))!;

        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(0, (int)version.ValueGenerated);
    }

    [Fact]
    public void IdentityColumnsInDifferentTables_AreAccepted()
    {
        using var context = new SeparateIdentityContext(new FakeDmConnection { RowsAffected = 1 });

        Assert.NotNull(context.Model.FindEntityType(typeof(TypedProbe))!.FindProperty(nameof(TypedProbe.Id)));
    }

    [Fact]
    public void IdentityColumnOnATextColumn_IsRejected()
    {
        using var context = new TextIdentityContext(new FakeDmConnection { RowsAffected = 1 });

        var exception = Assert.ThrowsAny<Exception>(() => context.Model);

        Assert.Contains("an INT or BIGINT column only", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IdentityColumnWithADefault_IsRejected()
    {
        using var context = new DefaultAndIdentityContext(new FakeDmConnection { RowsAffected = 1 });

        var exception = Assert.ThrowsAny<Exception>(() => context.Model);

        Assert.Contains("generates its value two ways", exception.Message, StringComparison.Ordinal);
    }

    private abstract class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        protected ProbeContext(FakeDmConnection connection) => _connection = connection;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);
    }

    private sealed class TwoIdentityContext : ProbeContext
    {
        public TwoIdentityContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TypedProbe>(
                e =>
                {
                    e.Property(p => p.Id).UseIdentityColumn();
                    e.Property(p => p.Other).UseIdentityColumn();
                });
    }

    private sealed class SeparateIdentityContext : ProbeContext
    {
        public SeparateIdentityContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TypedProbe>(e => e.Property(p => p.Id).UseIdentityColumn())
                .Entity<OtherProbe>(e => e.Property(p => p.Id).UseIdentityColumn());
    }

    private sealed class TextIdentityContext : ProbeContext
    {
        public TextIdentityContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TextProbe>(
                e =>
                {
                    e.Property(p => p.Id).HasMaxLength(20);
                    e.Property(p => p.Id).UseIdentityColumn();
                });
    }

    private sealed class DefaultAndIdentityContext : ProbeContext
    {
        public DefaultAndIdentityContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TypedProbe>(
                e =>
                {
                    e.Property(p => p.Id).HasDefaultValueSql("1");
                    e.Property(p => p.Id).UseIdentityColumn();
                });
    }

    private sealed class RowVersionContext : ProbeContext
    {
        public RowVersionContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        public DbSet<VersionedProbe> Probes => Set<VersionedProbe>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<VersionedProbe>(e => e.Property(p => p.Version).IsRowVersion());
    }

    private sealed class ApplicationTokenContext : ProbeContext
    {
        public ApplicationTokenContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        public DbSet<TokenProbe> Probes => Set<TokenProbe>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TokenProbe>(e => e.Property(p => p.Version).IsConcurrencyToken());
    }

    private sealed class VersionedProbe
    {
        public int Id { get; set; }

        public byte[] Version { get; set; } = [];
    }

    private sealed class TokenProbe
    {
        public int Id { get; set; }

        public int Version { get; set; }
    }

    private sealed class TypedProbe
    {
        public int Id { get; set; }

        public int Other { get; set; }
    }

    private sealed class OtherProbe
    {
        public int Id { get; set; }
    }

    private sealed class TextProbe
    {
        public string Id { get; set; } = string.Empty;
    }
}