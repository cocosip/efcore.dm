using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Database lifecycle operations for Dameng.
/// </summary>
/// <remarks>
/// Dameng creates a database with the <c>dminit</c> tool, not with SQL, so this class does not
/// try to create or drop one. What it does provide is the catalog queries the relational layer
/// needs once a connection exists.
/// </remarks>
internal sealed class DmDatabaseCreator : RelationalDatabaseCreator
{
    /// <summary>
    /// Counts user tables in the current schema. Recycle bin entries are excluded by name
    /// prefix, and system objects are excluded by their catalog type, so a dropped table that is
    /// still recoverable does not make the database look populated.
    /// </summary>
    private const string HasTablesSql =
        "SELECT CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END FROM SYS.SYSOBJECTS"
        + " WHERE SCHID = CURRENT_SCHID()"
        + " AND TYPE$ = 'SCHOBJ' AND SUBTYPE$ = 'UTAB'"
        + " AND NAME NOT LIKE 'BIN$%'";

    private readonly IRawSqlCommandBuilder _commandBuilder;

    public DmDatabaseCreator(
        RelationalDatabaseCreatorDependencies dependencies,
        IRawSqlCommandBuilder commandBuilder)
        : base(dependencies)
        => _commandBuilder = commandBuilder;

    public override bool Exists()
    {
        try
        {
            Dependencies.Connection.Open();

            return true;
        }
        catch (global::Dm.DmException)
        {
            return false;
        }
        finally
        {
            Dependencies.Connection.Close();
        }
    }

    public override async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Dependencies.Connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (global::Dm.DmException)
        {
            return false;
        }
        finally
        {
            await Dependencies.Connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public override bool HasTables()
        => Dependencies.ExecutionStrategy.Execute(
            Dependencies.Connection,
            (_, connection) => Convert.ToInt64(Scalar(connection), CultureInfo.InvariantCulture) != 0,
            verifySucceeded: null);

    public override Task<bool> HasTablesAsync(CancellationToken cancellationToken = default)
        => Dependencies.ExecutionStrategy.ExecuteAsync(
            Dependencies.Connection,
            async (_, connection, token) =>
                Convert.ToInt64(await ScalarAsync(connection, token).ConfigureAwait(false), CultureInfo.InvariantCulture) != 0,
            verifySucceeded: null,
            cancellationToken);

    public override void Create()
        => throw new NotSupportedException(
            "Dameng creates a database with the dminit tool before a connection string can reach it. "
            + "Create the database there, then run the migrations.");

    public override void Delete()
        => throw new NotSupportedException(
            "Dameng drops a database by removing its files while no session is connected to it. "
            + "A connected session cannot issue that statement.");

    private object? Scalar(IRelationalConnection connection)
        => _commandBuilder
            .Build(HasTablesSql)
            .ExecuteScalar(CreateParameterObject(connection));

    private Task<object?> ScalarAsync(IRelationalConnection connection, CancellationToken cancellationToken)
        => _commandBuilder
            .Build(HasTablesSql)
            .ExecuteScalarAsync(CreateParameterObject(connection), cancellationToken);

    private RelationalCommandParameterObject CreateParameterObject(IRelationalConnection connection)
        => new(
            Dependencies.Connection,
            parameterValues: null,
            readerColumns: null,
            Dependencies.CurrentContext.Context,
            Dependencies.CommandLogger);
}
