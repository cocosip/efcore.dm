using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Storage;

/// <summary>
/// T1 model tests for type mapping facets. They assert the store type a property resolves to,
/// which is what DDL generation and every later SQL assertion depend on.
/// </summary>
public class DmTypeMappingTests
{
    [Theory]
    [InlineData(nameof(Facets.DefaultString), "NCLOB")]
    [InlineData(nameof(Facets.AnsiDefaultString), "CLOB")]
    [InlineData(nameof(Facets.UnicodeDefaultString), "NCLOB")]
    [InlineData(nameof(Facets.LongString), "NCLOB")]
    [InlineData(nameof(Facets.UnicodeLongString), "NCLOB")]
    [InlineData(nameof(Facets.LongestBoundedString), "NVARCHAR2(8188)")]
    [InlineData(nameof(Facets.SizedString), "NVARCHAR2(50)")]
    [InlineData(nameof(Facets.AnsiSizedString), "VARCHAR2(50 CHAR)")]
    [InlineData(nameof(Facets.UnicodeString), "NVARCHAR2(50)")]
    [InlineData(nameof(Facets.FixedString), "NCHAR(50)")]
    [InlineData(nameof(Facets.UnicodeFixedString), "NCHAR(50)")]
    [InlineData(nameof(Facets.SizedBinary), "VARBINARY(64)")]
    [InlineData(nameof(Facets.UnsizedBinary), "BLOB")]
    [InlineData(nameof(Facets.DefaultDecimal), "DECIMAL(29,4)")]
    [InlineData(nameof(Facets.ScaledDecimal), "DECIMAL(18,2)")]
    [InlineData(nameof(Facets.DefaultDateTime), "TIMESTAMP")]
    [InlineData(nameof(Facets.PreciseDateTime), "TIMESTAMP(3)")]
    [InlineData(nameof(Facets.DateOnly), "DATE")]
    [InlineData(nameof(Facets.TimeOnly), "TIME(6)")]
    [InlineData(nameof(Facets.PreciseTimeOnly), "TIME(3)")]
    [InlineData(nameof(Facets.Guid), "CHAR(36)")]
    [InlineData(nameof(Facets.Flag), "BIT")]
    [InlineData(nameof(Facets.Count), "BIGINT")]
    [InlineData(nameof(Facets.SmallCount), "SMALLINT")]
    public void Property_MapsToExpectedStoreType(string property, string expected)
    {
        using var context = new FacetsContext();

        var entity = context.Model.FindEntityType(typeof(Facets))!;

        Assert.Equal(expected, entity.FindProperty(property)!.GetColumnType());
    }

    /// <summary>
    /// A string the model leaves unbounded is a character large object, which Dameng does not index, so a
    /// key or an index names the bound the framework's own providers use for a key instead. Without it a
    /// table with a string key could not be created at all.
    /// </summary>
    [Theory]
    [InlineData("TextKey", "NVARCHAR2(450)")]
    [InlineData("AnsiKey", "VARCHAR2(900 CHAR)")]
    [InlineData("AnsiFixedKey", "CHAR(1 CHAR)")]
    [InlineData("UnicodeIndexedKey", "NVARCHAR2(450)")]
    [InlineData("IndexedKey", "NVARCHAR2(450)")]
    [InlineData("FixedKey", "NCHAR(1)")]
    public void AStringUsedAsAKeyOrIndex_NamesABoundedLength(string property, string expected)
    {
        using var context = new KeyedContext();

        var entity = context.Model.FindEntityType(typeof(Keyed))!;

        Assert.Equal(expected, entity.FindProperty(property)!.GetColumnType());
    }
    /// <summary>
    /// A byte array the model leaves unbounded is a large object, which Dameng does not index, so a key or
    /// an index names a bounded binary length instead. Without it a table with a binary key could not be
    /// created at all: the server refuses an index on a large object column.
    /// </summary>
    [Theory]
    [InlineData("BinaryKey", "VARBINARY(900)")]
    [InlineData("BinaryIndexed", "VARBINARY(900)")]
    [InlineData("SizedBinary", "VARBINARY(16)")]
    public void ABinaryUsedAsAKeyOrIndex_NamesABoundedLength(string property, string expected)
    {
        using var context = new BinaryKeyedContext();

        var entity = context.Model.FindEntityType(typeof(BinaryKeyed))!;

        Assert.Equal(expected, entity.FindProperty(property)!.GetColumnType());
    }

    /// <summary>
    /// A byte array is written as <c>HEXTORAW</c> rather than the base form, because Dameng rejects the
    /// base form for a large object column with <c>数据类型不匹配</c>.
    /// </summary>
    [Fact]
    public void ByteArray_Literal_IsHexToRaw()
    {
        using var context = new BinaryKeyedContext();

        var mapping = context.Model
            .FindEntityType(typeof(BinaryKeyed))!
            .FindProperty(nameof(BinaryKeyed.BinaryIndexed))!
            .GetRelationalTypeMapping();

        Assert.Equal("HEXTORAW('0414')", mapping.GenerateSqlLiteral(new byte[] { 0x04, 0x14 }));
    }

    /// <summary>
    /// An interval literal names its type, which a bare time string does not, and a negative span
    /// carries the sign in front of the value rather than on a field.
    /// </summary>
    [Theory]
    [InlineData("01:00:00", "INTERVAL '0 01:00:00' DAY TO SECOND")]
    [InlineData("1.02:03:04", "INTERVAL '1 02:03:04' DAY TO SECOND")]
    [InlineData("-00:30:00", "INTERVAL '-0 00:30:00' DAY TO SECOND")]
    [InlineData("00:00:01.500", "INTERVAL '0 00:00:01.500' DAY TO SECOND")]
    public void TimeSpan_Literal_NamesTheIntervalType(string span, string expected)
    {
        using var context = new FacetsContext();

        var mapping = context.Model
            .FindEntityType(typeof(Facets))!
            .FindProperty(nameof(Facets.Span))!
            .GetRelationalTypeMapping();

        Assert.Equal(expected, mapping.GenerateSqlLiteral(TimeSpan.Parse(span, System.Globalization.CultureInfo.InvariantCulture)));
    }

    private sealed class KeyedContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Keyed>(entity =>
            {
                entity.HasKey(entry => entry.TextKey);
                entity.Property(entry => entry.AnsiKey).IsUnicode(false);
                entity.HasIndex(entry => entry.AnsiKey);
                entity.Property(entry => entry.UnicodeIndexedKey).IsUnicode();
                entity.HasIndex(entry => entry.UnicodeIndexedKey);
                entity.Property(entry => entry.IndexedKey);
                entity.HasIndex(entry => entry.IndexedKey);
                entity.Property(entry => entry.FixedKey).IsFixedLength();
                entity.Property(entry => entry.AnsiFixedKey).IsUnicode(false).IsFixedLength();
                entity.HasIndex(entry => entry.AnsiFixedKey);
            });
    }

    private sealed class Keyed
    {
        public string TextKey { get; set; } = string.Empty;

        public string AnsiKey { get; set; } = string.Empty;

        public string UnicodeIndexedKey { get; set; } = string.Empty;

        public string IndexedKey { get; set; } = string.Empty;

        public string FixedKey { get; set; } = string.Empty;

        public string AnsiFixedKey { get; set; } = string.Empty;
    }

    private sealed class BinaryKeyedContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<BinaryKeyed>(entity =>
            {
                entity.HasKey(entry => entry.BinaryKey);
                entity.HasIndex(entry => entry.BinaryIndexed);
                entity.Property(entry => entry.SizedBinary).HasMaxLength(16);
                entity.HasIndex(entry => entry.SizedBinary);
            });
    }

    private sealed class BinaryKeyed
    {
        public byte[] BinaryKey { get; set; } = [];

        public byte[] BinaryIndexed { get; set; } = [];

        public byte[] SizedBinary { get; set; } = [];
    }

    private sealed class FacetsContext : DbContext
    {
        public DbSet<Facets> Facets => Set<Facets>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Facets>(entity =>
            {
                entity.Property(e => e.SizedString).HasMaxLength(50);
                entity.Property(e => e.AnsiSizedString).HasMaxLength(50).IsUnicode(false);
                entity.Property(e => e.AnsiDefaultString).IsUnicode(false);
                entity.Property(e => e.UnicodeDefaultString).IsUnicode();
                entity.Property(e => e.LongString).HasMaxLength(40000);
                entity.Property(e => e.UnicodeLongString).HasMaxLength(40000).IsUnicode();
                entity.Property(e => e.LongestBoundedString).HasMaxLength(8188);
                entity.Property(e => e.UnicodeString).HasMaxLength(50).IsUnicode();
                entity.Property(e => e.FixedString).HasMaxLength(50).IsFixedLength();
                entity.Property(e => e.UnicodeFixedString).HasMaxLength(50).IsUnicode().IsFixedLength();
                entity.Property(e => e.SizedBinary).HasMaxLength(64);
                entity.Property(e => e.ScaledDecimal).HasPrecision(18, 2);
                entity.Property(e => e.PreciseDateTime).HasPrecision(3);
                entity.Property(e => e.PreciseTimeOnly).HasPrecision(3);
            });
    }

    private sealed class Facets
    {
        public int Id { get; set; }

        public string DefaultString { get; set; } = string.Empty;

        public string SizedString { get; set; } = string.Empty;

        public string AnsiSizedString { get; set; } = string.Empty;

        public string AnsiDefaultString { get; set; } = string.Empty;

        public string UnicodeDefaultString { get; set; } = string.Empty;

        public string LongString { get; set; } = string.Empty;

        public string UnicodeLongString { get; set; } = string.Empty;

        public string LongestBoundedString { get; set; } = string.Empty;

        public string UnicodeString { get; set; } = string.Empty;

        public string FixedString { get; set; } = string.Empty;

        public string UnicodeFixedString { get; set; } = string.Empty;

        public byte[] SizedBinary { get; set; } = [];

        public byte[] UnsizedBinary { get; set; } = [];

        public decimal DefaultDecimal { get; set; }

        public decimal ScaledDecimal { get; set; }

        public DateTime DefaultDateTime { get; set; }

        public DateTime PreciseDateTime { get; set; }

        public DateOnly DateOnly { get; set; }

        public TimeOnly TimeOnly { get; set; }

        public TimeOnly PreciseTimeOnly { get; set; }

        public Guid Guid { get; set; }

        public bool Flag { get; set; }

        public long Count { get; set; }

        public short SmallCount { get; set; }

        public TimeSpan Span { get; set; }
    }
}
