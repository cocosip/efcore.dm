using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Infrastructure;

public class DmProviderRegistrationTests
{
    [Fact]
    public void UseDm_ConfiguresTheDamengProvider()
    {
        using var context = new ProbeContext();

        Assert.Equal("DM.EntityFrameworkCore", context.Database.ProviderName);
    }

    [Fact]
    public void UseDm_ExposesConnectionString()
    {
        using var context = new ProbeContext();

        Assert.Equal(ProbeContext.ConnectionString, context.Database.GetConnectionString());
    }

    [Fact]
    public void UseDm_BuildsModelWithMappedTypes()
    {
        using var context = new ProbeContext();

        var entity = context.Model.FindEntityType(typeof(Probe))!;
        var id = entity.FindProperty(nameof(Probe.Id))!;
        var name = entity.FindProperty(nameof(Probe.Name))!;

        Assert.Equal("INT", id.GetColumnType());
        Assert.Equal("VARCHAR", name.GetColumnType());
    }

    private sealed class ProbeContext : DbContext
    {
        public const string ConnectionString = "Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;";

        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(ConnectionString);
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
