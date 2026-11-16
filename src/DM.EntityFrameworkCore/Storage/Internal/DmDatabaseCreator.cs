using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
    /// Counts user tables in the schema the model maps, which is the current schema when the model
    /// names none; a model in another schema would otherwise look empty and <c>EnsureCreated</c> would
    /// try to create objects that are already there. Recycle bin entries are excluded by name prefix,
    /// and system objects are excluded by their catalog type, so a dropped table that is still
    /// recoverable does not make the database look populated.
    /// </summary>
    private string HasTablesSql
    {
        get
        {
            var schemas = Dependencies.CurrentContext.Context.Model
                .GetRelationalModel()
                .Tables
                .Select(table => table.Schema)
                .Where(schema => !string.IsNullOrEmpty(schema))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (schemas.Count == 0)
            {
                return DefaultHasTablesSql;
            }

            var names = string.Join(
                ", ",
                schemas.Select(schema => "'" + schema!.Replace("'", "''", StringComparison.Ordinal) + "'"));

            return "SELECT CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END FROM SYS.SYSOBJECTS"
                + " WHERE SCHID IN (SELECT ID FROM SYS.SYSOBJECTS WHERE TYPE$ = 'SCH'"
                + " AND (NAME IN (" + names + ") OR NAME = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')))"
                + " AND TYPE$ = 'SCHOBJ' AND SUBTYPE$ = 'UTAB'"
                + " AND NAME NOT LIKE 'BIN$%'";
        }
    }

    private const string DefaultHasTablesSql =
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

    /// <summary>
    /// The parameter object names the connection the execution strategy handed over rather than the one
    /// the dependency holds: the two are the same while nothing retries, and a strategy that retries
    /// would hand the retry its own.
    /// </summary>
    private RelationalCommandParameterObject CreateParameterObject(IRelationalConnection connection)
        => new(
            connection,
            parameterValues: null,
            readerColumns: null,
            Dependencies.CurrentContext.Context,
            Dependencies.CommandLogger);
}
