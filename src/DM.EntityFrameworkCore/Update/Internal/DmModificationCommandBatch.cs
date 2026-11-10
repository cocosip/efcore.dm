using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace DM.EntityFrameworkCore.Update.Internal;

/// <summary>
/// Executes one modification statement per round trip.
/// </summary>
/// <remarks>
/// The relational base batch reads the affected row count from a result set produced by a
/// <c>RETURNING</c> clause. Dameng cannot produce that result set for a plain statement, so the
/// batch uses the driver's non-query return value instead, which is why the batch size is one:
/// with more than one statement in a round trip the return value no longer maps to a single
/// entity, and the concurrency check could not attribute it.
/// </remarks>
internal sealed class DmModificationCommandBatch : AffectedCountModificationCommandBatch
{
    public DmModificationCommandBatch(ModificationCommandBatchFactoryDependencies dependencies, int maxBatchSize)
        : base(dependencies, maxBatchSize)
    {
    }

    public override void Execute(IRelationalConnection connection)
        => CheckRowsAffected(ExecuteCommand(connection));

    public override async Task ExecuteAsync(
        IRelationalConnection connection,
        CancellationToken cancellationToken = default)
        => CheckRowsAffected(await ExecuteCommandAsync(connection, cancellationToken).ConfigureAwait(false));

    private int ExecuteCommand(IRelationalConnection connection)
    {
        var command = StoreCommand
            ?? throw new InvalidOperationException("The modification command batch is not complete.");

        return command.RelationalCommand.ExecuteNonQuery(
            new RelationalCommandParameterObject(
                connection,
                command.ParameterValues,
                null,
                Dependencies.CurrentContext.Context,
                Dependencies.Logger,
                CommandSource.SaveChanges));
    }

    private Task<int> ExecuteCommandAsync(IRelationalConnection connection, CancellationToken cancellationToken)
    {
        var command = StoreCommand
            ?? throw new InvalidOperationException("The modification command batch is not complete.");

        return command.RelationalCommand.ExecuteNonQueryAsync(
            new RelationalCommandParameterObject(
                connection,
                command.ParameterValues,
                null,
                Dependencies.CurrentContext.Context,
                Dependencies.Logger,
                CommandSource.SaveChanges),
            cancellationToken);
    }

    /// <summary>
    /// Rejects a statement that did not affect its one expected row, which is the optimistic
    /// concurrency check. Without it a lost update would be reported as success.
    /// </summary>
    private void CheckRowsAffected(int rowsAffected)
    {
        if (rowsAffected == 1)
        {
            return;
        }

        throw new DbUpdateConcurrencyException(
            $"The database operation was expected to affect 1 row(s), but actually affected {rowsAffected} row(s); "
            + "data may have been modified or deleted since entities were loaded.",
            ModificationCommands.SelectMany(c => c.Entries).ToList());
    }
}
