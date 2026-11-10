using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// T2 tests for the extensions that answer a question about the provider rather than change a model.
/// </summary>
public class DmDatabaseFacadeTests
{
    [Fact]
    public void IsDm_RecognisesTheProvider()
    {
        using var context = new ProbeContext(new FakeDmConnection());

        Assert.True(context.Database.IsDm());
    }

    private sealed class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public ProbeContext(FakeDmConnection connection) => _connection = connection;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);
    }
}
