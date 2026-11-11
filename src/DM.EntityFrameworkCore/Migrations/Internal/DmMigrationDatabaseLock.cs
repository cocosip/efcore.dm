using Microsoft.EntityFrameworkCore.Migrations;

namespace DM.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// The named lock taken while a migration runs.
/// </summary>
/// <remarks>
/// The lock is a <c>DBMS_LOCK</c> named lock owned by the session, so it is released when it is asked for,
/// when the connection closes, or when the process dies; nothing is stored in the database, so a failed
/// migration cannot leave a lock behind for the next run to trip on.
/// <para>
/// A table lock would not do: measured on 8.1.4.80, an exclusive table lock taken inside a transaction was
/// released by the first DDL statement of the migration, because the server commits around DDL. The named
/// lock survived that same DDL and still blocked the second session, which is the property a migration lock
/// needs.
/// </para>
/// </remarks>
internal sealed class DmMigrationDatabaseLock : IMigrationsDatabaseLock
{
    private readonly IHistoryRepository _historyRepository;

    private readonly DmHistoryRepository? _owner;

    private readonly int _id;

    /// <param name="historyRepository">The repository the lock was taken through.</param>
    /// <param name="owner">
    /// The repository that owns the lock, or null when the session already held it, in which case the lock
    /// is left for the holder to release.
    /// </param>
    /// <param name="id">The named lock the session holds.</param>
    public DmMigrationDatabaseLock(IHistoryRepository historyRepository, DmHistoryRepository? owner, int id)
    {
        _historyRepository = historyRepository;
        _owner = owner;
        _id = id;
    }

    IHistoryRepository IMigrationsDatabaseLock.HistoryRepository => _historyRepository;

    public void Dispose() => _owner?.ReleaseNamedLock(_id);

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Re-takes the lock once the connection underneath it has been restarted, because the server dropped
    /// the original lock with the session that held it.
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
