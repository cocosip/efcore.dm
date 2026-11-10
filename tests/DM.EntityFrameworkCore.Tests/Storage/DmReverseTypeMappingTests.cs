using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Tests.Storage;

/// <summary>
/// T1 tests for the reverse direction of the type mapping source. Reverse engineering reads a
/// column type from the catalog and has to recover both the CLR type and the facets written in
/// that name, so a round trip that loses either would silently change the model.
/// </summary>
public class DmReverseTypeMappingTests
{
    [Theory]
    [InlineData("BIT", typeof(bool))]
    [InlineData("TINYINT", typeof(sbyte))]
    [InlineData("SMALLINT", typeof(short))]
    [InlineData("INT", typeof(int))]
    [InlineData("INTEGER", typeof(int))]
    [InlineData("BIGINT", typeof(long))]
    [InlineData("REAL", typeof(float))]
    // Dameng FLOAT is FLOAT(53), which is double precision, so a column that declares it reads back
    // as double. REAL is the single-precision type.
    [InlineData("FLOAT", typeof(double))]
    [InlineData("FLOAT(53)", typeof(double))]
    [InlineData("DOUBLE", typeof(double))]
    [InlineData("DOUBLE PRECISION", typeof(double))]
    [InlineData("DECIMAL(18,2)", typeof(decimal))]
    [InlineData("NUMBER", typeof(decimal))]
    [InlineData("NUMERIC", typeof(decimal))]
    [InlineData("VARCHAR(50)", typeof(string))]
    [InlineData("VARCHAR2(50)", typeof(string))]
    [InlineData("NVARCHAR(50)", typeof(string))]
    [InlineData("CLOB", typeof(string))]
    [InlineData("NCLOB", typeof(string))]
    [InlineData("BLOB", typeof(byte[]))]
    [InlineData("VARBINARY(64)", typeof(byte[]))]
    [InlineData("DATE", typeof(DateTime))]
    [InlineData("TIMESTAMP(3)", typeof(DateTime))]
    [InlineData("DATETIME", typeof(DateTime))]
    [InlineData("TIME", typeof(TimeOnly))]
    [InlineData("TIME(6)", typeof(TimeOnly))]
    [InlineData("TIME(3)", typeof(TimeOnly))]
    [InlineData("TIMESTAMP WITH TIME ZONE", typeof(DateTimeOffset))]
    [InlineData("DATETIME WITH TIME ZONE", typeof(DateTimeOffset))]
    [InlineData("CHAR(10)", typeof(string))]
    [InlineData("INTERVAL DAY TO SECOND", typeof(TimeSpan))]
    [InlineData("CHAR(36)", typeof(Guid))]
    public void StoreType_ResolvesToClrType(string storeType, Type expected)
    {
        using var context = new ProbeContext();

        Assert.Equal(expected, Find(storeType)?.ClrType);
    }

    [Theory]
    [InlineData("VARCHAR(50)", 50)]
    [InlineData("NVARCHAR(120)", 120)]
    [InlineData("VARBINARY(64)", 64)]
    public void StoreType_RecoversTheSizeFacet(string storeType, int size)
    {
        using var context = new ProbeContext();

        Assert.Equal(size, Find(storeType)?.Size);
    }

    [Fact]
    public void StoreType_RecoversThePrecisionAndScaleFacets()
    {
        using var context = new ProbeContext();

        var mapping = Find("DECIMAL(18,2)");

        Assert.Equal(18, mapping?.Precision);
        Assert.Equal(2, mapping?.Scale);
        Assert.Equal("DECIMAL(18,2)", mapping?.StoreType);
    }

    [Fact]
    public void RoundTrip_KeepsTheStoreTypeTheModelDeclared()
    {
        using var context = new ProbeContext();

        var mapping = Find(context.Model.FindEntityType(typeof(Probe))!.FindProperty(nameof(Probe.Name))!.GetColumnType());

        Assert.Equal(typeof(string), mapping?.ClrType);
        Assert.Equal(50, mapping?.Size);
    }

    /// <summary>
    /// The width is in the name rather than a facet, because a Guid has no size of its own, so the
    /// name itself has to survive the round trip.
    /// </summary>
    [Fact]
    public void GuidStoreType_RoundTripsAsATypeThatReadsBack()
    {
        var mapping = Find("CHAR(36)");

        Assert.Equal(typeof(Guid), mapping?.ClrType);
        Assert.Equal("CHAR(36)", mapping?.StoreType);
    }

    [Fact]
    public void UnmappedStoreType_ResolvesToNothing()
    {
        using var context = new ProbeContext();

        Assert.Null(Find("SOMETYPE(1)"));
    }

    private static RelationalTypeMapping? Find(string storeType)
    {
        using var context = new ProbeContext();

        return context.GetService<IRelationalTypeMappingSource>().FindMapping(storeType);
    }

    private sealed class ProbeContext : DbContext
    {
        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Name).HasMaxLength(50);
            });
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
