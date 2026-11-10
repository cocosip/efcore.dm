using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Storage;

/// <summary>
/// T1 model tests for type mapping facets. They assert the store type a property resolves to,
/// which is what DDL generation and every later SQL assertion depend on.
/// </summary>
public class DmTypeMappingTests
{
    [Theory]
    [InlineData(nameof(Facets.DefaultString), "VARCHAR")]
    [InlineData(nameof(Facets.SizedString), "VARCHAR(50)")]
    [InlineData(nameof(Facets.UnicodeString), "NVARCHAR(50)")]
    [InlineData(nameof(Facets.FixedString), "CHAR(50)")]
    [InlineData(nameof(Facets.UnicodeFixedString), "NCHAR(50)")]
    [InlineData(nameof(Facets.SizedBinary), "VARBINARY(64)")]
    [InlineData(nameof(Facets.UnsizedBinary), "BLOB")]
    [InlineData(nameof(Facets.DefaultDecimal), "DECIMAL(29,4)")]
    [InlineData(nameof(Facets.ScaledDecimal), "DECIMAL(18,2)")]
    [InlineData(nameof(Facets.DefaultDateTime), "TIMESTAMP")]
    [InlineData(nameof(Facets.PreciseDateTime), "TIMESTAMP(3)")]
    [InlineData(nameof(Facets.DateOnly), "DATE")]
    [InlineData(nameof(Facets.TimeOnly), "TIME")]
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

    private sealed class FacetsContext : DbContext
    {
        public DbSet<Facets> Facets => Set<Facets>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Facets>(entity =>
            {
                entity.Property(e => e.SizedString).HasMaxLength(50);
                entity.Property(e => e.UnicodeString).HasMaxLength(50).IsUnicode();
                entity.Property(e => e.FixedString).HasMaxLength(50).IsFixedLength();
                entity.Property(e => e.UnicodeFixedString).HasMaxLength(50).IsUnicode().IsFixedLength();
                entity.Property(e => e.SizedBinary).HasMaxLength(64);
                entity.Property(e => e.ScaledDecimal).HasPrecision(18, 2);
                entity.Property(e => e.PreciseDateTime).HasPrecision(3);
            });
    }

    private sealed class Facets
    {
        public int Id { get; set; }

        public string DefaultString { get; set; } = string.Empty;

        public string SizedString { get; set; } = string.Empty;

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

        public Guid Guid { get; set; }

        public bool Flag { get; set; }

        public long Count { get; set; }

        public short SmallCount { get; set; }

        public TimeSpan Span { get; set; }
    }
}
