using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// T2 tests for the options a consumer sets on <see cref="DM.EntityFrameworkCore.Infrastructure.DmDbContextOptionsBuilder" />.
/// The builder derives from the relational one, so the relational settings have to reach the context
/// rather than merely compile.
/// </summary>
public class DmDbContextOptionsBuilderTests
{
    [Fact]
    public void CommandTimeout_SetOnTheBuilder_ReachesTheCommand()
    {
        var connection = new FakeDmConnection { RowsAffected = 1 };

        using var context = new ProbeContext(connection);
        context.Add(new Probe { Id = 1, Name = "a" });

        context.SaveChanges();

        Assert.Equal(7, connection.LastCommandTimeout);
    }

    private sealed class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public ProbeContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection, options => options.CommandTimeout(7));
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
