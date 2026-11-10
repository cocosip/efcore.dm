using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Diagnostics;

/// <summary>
/// T2 tests for the cancellation bridge. <c>DM.DmProvider</c> inherits the asynchronous command members
/// of <see cref="System.Data.Common.DbCommand" />, and those do not stop a statement that is already
/// running, so a cancelled token only reaches the server if the provider calls
/// <see cref="System.Data.Common.DbCommand.Cancel" /> itself.
/// </summary>
public class DmCommandCancellationTests
{
    [Fact]
    public async Task CancelledToken_CancelsTheStatementThatIsRunning()
    {
        var connection = new FakeDmConnection { BlocksUntilCancelled = true };
        using var context = new ProbeContext(connection);
        using var cancellation = new CancellationTokenSource();

        var execution = context.Database.ExecuteSqlRawAsync("SELECT 1 FROM DUAL", cancellation.Token);
        await connection.Started;

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.Equal(1, connection.CancelCount);
    }

    /// <summary>
    /// A token that cannot be cancelled leaves nothing to bridge, so no callback is registered and the
    /// statement is unaffected.
    /// </summary>
    [Fact]
    public async Task UncancellableToken_LeavesTheStatementAlone()
    {
        var connection = new FakeDmConnection { RowsAffected = 3 };
        using var context = new ProbeContext(connection);

        var affected = await context.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM DUAL",
            CancellationToken.None);

        Assert.Equal(3, affected);
        Assert.Equal(0, connection.CancelCount);
    }

    /// <summary>
    /// The save path builds its command through the modification batch rather than the raw SQL helper,
    /// so it is a separate path to the same interceptor.
    /// </summary>
    [Fact]
    public async Task CancelledToken_DuringSaveChanges_CancelsTheStatement()
    {
        var connection = new FakeDmConnection { BlocksUntilCancelled = true };
        using var context = new ProbeContext(connection);
        using var cancellation = new CancellationTokenSource();

        context.Probes.Add(new Probe { Name = "a" });

        var save = context.SaveChangesAsync(cancellation.Token);
        await connection.Started;

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => save);
        Assert.Equal(1, connection.CancelCount);
    }

    private sealed class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public ProbeContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
