using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DM.EntityFrameworkCore.Tests.Metadata;

/// <summary>
/// T1 tests for the value generation strategy. They read the built model, so they cover the
/// convention that chooses the strategy without needing a database or a migration.
/// </summary>
/// <remarks>
/// Each scenario has its own context type. EF Core caches the model per context type, so a shared
/// type would hand the second test the model the first one built.
/// </remarks>
public class DmValueGenerationStrategyTests
{
    [Fact]
    public void ConventionIntegerKey_BecomesIdentityColumn()
    {
        using var context = new ProbeContext();

        Assert.Equal(DmValueGenerationStrategy.IdentityColumn, Key(context, typeof(IdentityProbe)).GetDmValueGenerationStrategy());
    }

    [Fact]
    public void ExplicitOnAddIntegerKey_BecomesIdentityColumn()
    {
        using var context = new ExplicitOnAddContext();

        Assert.Equal(DmValueGenerationStrategy.IdentityColumn, Key(context, typeof(IdentityProbe)).GetDmValueGenerationStrategy());
    }

    [Fact]
    public void ExplicitNone_IsRespected()
    {
        using var context = new ExplicitNoneContext();

        Assert.Equal(DmValueGenerationStrategy.None, Key(context, typeof(IdentityProbe)).GetDmValueGenerationStrategy());
    }

    /// <summary>
    /// The strategy has to reach the DDL as well as the model, or an insert would target a column
    /// the database does not fill.
    /// </summary>
    [Fact]
    public void ExplicitNone_DoesNotEmitIdentity()
    {
        using var context = new ExplicitNoneContext();

        Assert.DoesNotContain("IDENTITY", context.Database.GenerateCreateScript(), StringComparison.Ordinal);
    }

    [Fact]
    public void ConventionIntegerKey_EmitsIdentity()
    {
        using var context = new ProbeContext();

        Assert.Contains("""INT IDENTITY(1,1) NOT NULL""", context.Database.GenerateCreateScript(), StringComparison.Ordinal);
    }

    [Fact]
    public void NonIntegerKey_HasNoStrategy()
    {
        using var context = new ProbeContext();

        Assert.Null(Key(context, typeof(GuidKeyProbe)).GetDmValueGenerationStrategy());
    }

    [Fact]
    public void NonKeyProperty_HasNoStrategy()
    {
        using var context = new ProbeContext();

        Assert.Null(
            context.Model.FindEntityType(typeof(IdentityProbe))!
                .FindProperty(nameof(IdentityProbe.Name))!
                .GetDmValueGenerationStrategy());
    }

    /// <summary>
    /// A key that already has a default expression is not an identity column: the store supplies
    /// the value from that expression, so the strategy records where the value comes from.
    /// </summary>
    [Fact]
    public void KeyWithDefaultExpression_HasTheSequenceStrategy()
    {
        using var context = new DefaultSqlContext();

        Assert.Equal(DmValueGenerationStrategy.Sequence, Key(context, typeof(IdentityProbe)).GetDmValueGenerationStrategy());
    }

    /// <summary>
    /// A key whose store type is not an integer cannot be an identity column: measured on 8.1.4.80,
    /// `IDENTITY` on `TINYINT` and `SMALLINT` answered `非法IDENTITY列类型`, while `INT`, `BIGINT`,
    /// `INTEGER`, `NUMBER` and `DECIMAL` were accepted.
    /// </summary>
    [Fact]
    public void ShortKey_HasNoStrategy()
    {
        using var context = new ProbeContext();

        Assert.Null(Key(context, typeof(ShortKeyProbe)).GetDmValueGenerationStrategy());
    }

    /// <summary>
    /// The column type decides, not the CLR type: an integer key converted to a string is a character
    /// column, and an identity on one answered `非法IDENTITY列类型`, measured on 8.1.4.80 through the store
    /// generated cases of the specification suite. The model is refused rather than sent to fail.
    /// </summary>
    [Fact]
    public void ConvertedIntegerKey_IsRefused()
    {
        using var context = new ConvertedKeyContext();

        var exception = Assert.Throws<InvalidOperationException>(() => context.Model);

        Assert.Contains("NVARCHAR2(64)", exception.Message, StringComparison.Ordinal);
    }

    private static IProperty Key(DbContext context, Type entityType)
        => context.Model.FindEntityType(entityType)!.FindPrimaryKey()!.Properties[0];

    private class ProbeContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IdentityProbe>(e => e.Property(p => p.Name).HasMaxLength(50));
            modelBuilder.Entity<GuidKeyProbe>(e => e.Property(p => p.Name).HasMaxLength(50));
            modelBuilder.Entity<ShortKeyProbe>(e => e.Property(p => p.Name).HasMaxLength(50));
        }
    }

    private sealed class ExplicitOnAddContext : ProbeContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<IdentityProbe>(e => e.Property(p => p.Id).ValueGeneratedOnAdd());
        }
    }

    private sealed class ExplicitNoneContext : ProbeContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<IdentityProbe>(
                e => e.Property(p => p.Id).Metadata.SetDmValueGenerationStrategy(DmValueGenerationStrategy.None));
        }
    }

    private sealed class ConvertedKeyContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConvertedKeyProbe>(
                e => e.Property(p => p.Id).HasMaxLength(64).HasConversion<string>());
    }

    private sealed class DefaultSqlContext : ProbeContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<IdentityProbe>(e => e.Property(p => p.Id).HasDefaultValueSql("1"));
        }
    }

    private sealed class IdentityProbe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class GuidKeyProbe
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class ShortKeyProbe
    {
        public short Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class ConvertedKeyProbe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
