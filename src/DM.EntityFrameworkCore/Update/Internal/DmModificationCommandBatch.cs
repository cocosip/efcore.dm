using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace DM.EntityFrameworkCore.Update.Internal;

/// <summary>
/// Executes one modification statement per round trip.
/// </summary>
/// <remarks>
/// A statement that returns no result set is executed as a non-query, and the driver's return
/// value is the row count the concurrency check uses. A statement that has to read a
/// store-generated value back runs as two statements and produces one result set, so it takes the
/// relational reader path, which consumes that result set and fills the properties.
/// <para>
/// The batch size is one for the same reason: with more than one statement in a round trip the
/// non-query return value no longer maps to a single entity, and the concurrency check could not
/// attribute it.
/// </para>
/// </remarks>
internal sealed class DmModificationCommandBatch : AffectedCountModificationCommandBatch
{
    public DmModificationCommandBatch(ModificationCommandBatchFactoryDependencies dependencies, int maxBatchSize)
        : base(dependencies, maxBatchSize)
    {
    }

    private bool RequiresResultSet
        => ModificationCommands.Any(
            command => command.ColumnModifications.Any(modification => modification.IsRead));

    public override void Execute(IRelationalConnection connection)
    {
        if (RequiresResultSet)
        {
            base.Execute(connection);

            return;
        }

        CheckRowsAffected(ExecuteCommand(connection));
    }

    public override async Task ExecuteAsync(
        IRelationalConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (RequiresResultSet)
        {
            await base.ExecuteAsync(connection, cancellationToken).ConfigureAwait(false);

            return;
        }

        CheckRowsAffected(await ExecuteCommandAsync(connection, cancellationToken).ConfigureAwait(false));
    }

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
