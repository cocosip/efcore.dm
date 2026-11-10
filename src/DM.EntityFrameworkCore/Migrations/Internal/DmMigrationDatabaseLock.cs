using Microsoft.EntityFrameworkCore.Migrations;

namespace DM.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// Represents the exclusive table lock taken while a migration runs.
/// </summary>
/// <remarks>
/// The lock needs no explicit release: Dameng ends it with the transaction that took it, which is
/// what <see cref="LockReleaseBehavior.Transaction" /> tells EF Core to expect. Nothing is stored
/// in the database, so a failed migration cannot leave a lock behind for the next run to trip on.
/// </remarks>
internal sealed class DmMigrationDatabaseLock : IMigrationsDatabaseLock
{
    private readonly IHistoryRepository _historyRepository;

    public DmMigrationDatabaseLock(IHistoryRepository historyRepository)
        => _historyRepository = historyRepository;

    IHistoryRepository IMigrationsDatabaseLock.HistoryRepository => _historyRepository;

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Re-takes the lock once the connection or the transaction underneath it has been restarted,
    /// because Dameng released the original lock with them.
    /// </summary>
    public Task<IMigrationsDatabaseLock> ReacquireIfNeededAsync(
        bool connectionReopened,
        bool? transactionRestarted,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return connectionReopened || transactionRestarted is not false
            ? _historyRepository.AcquireDatabaseLockAsync(cancellationToken)
            : Task.FromResult<IMigrationsDatabaseLock>(this);
    }
}
