using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using DM.EntityFrameworkCore.Dialect;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// Stores the applied migrations in the history table.
/// </summary>
/// <remarks>
/// Dameng has no DDL that creates a table only when it is missing, so the idempotent script hooks
/// emit an anonymous block that checks the catalog and then runs the statement through
/// <c>EXECUTE IMMEDIATE</c>.
/// <para>
/// The lock is a <c>DBMS_LOCK</c> named lock held by the session. Measured on 8.1.4.80, an exclusive table
/// lock inside a transaction was released by the first DDL statement the migration ran, so it could not
/// guard anything; the named lock survived the same DDL. A lock table would need clearing by hand after a
/// failure, while this one ends with the session.
/// </para>
/// </remarks>
internal sealed class DmHistoryRepository : HistoryRepository
{
    public DmHistoryRepository(HistoryRepositoryDependencies dependencies)
        : base(dependencies)
    {
    }

    /// <summary>
    /// The lock is owned by the session rather than by a transaction, because the server commits around DDL
    /// and a transaction scoped lock would end with the first statement of the migration.
    /// </summary>
    public override LockReleaseBehavior LockReleaseBehavior => LockReleaseBehavior.Connection;

    /// <summary>
    /// Dameng keeps its catalog in <c>SYS.SYSOBJECTS</c>, which is the only place that knows
    /// whether an object exists regardless of the caller's privileges on it.
    /// </summary>
    protected override string ExistsSql => "SELECT ID FROM SYS.SYSOBJECTS WHERE " + ObjectPredicate + ";";

    protected override bool InterpretExistsResult(object? value) => value is not null;

    /// <summary>
    /// A create that two deployments can run at the same time, which is what a rolling deployment does when
    /// its instances come up together.
    /// </summary>
    /// <remarks>
    /// The catalog check alone is not enough: measured on 8.1.4.80, four sessions that started a migration
    /// together all read an empty catalog and three of them then failed with
    /// <c>数据库对象[__EFMigrationsHistory]已存在</c>. The create therefore runs in a block that swallows the
    /// failure only when the object is there afterwards, so the session that lost the race carries on to the
    /// lock, where the work is serialised, and a create that fails for any other reason still raises.
    /// </remarks>
    public override string GetCreateIfNotExistsScript()
    {
        // The statement goes inside a single-quoted literal in a block, so it is written on one line: the
        // statement the generator builds spans several, and a literal line break inside the block is what
        // the parser rejected when the handler was added.
        var create = Regex
            .Replace(CreateStatement(), @"\s+", " ")
            .Replace("'", "''", StringComparison.Ordinal);

        return new StringBuilder()
            .AppendLine("BEGIN")
            .Append("    IF (SELECT COUNT(*) FROM SYS.SYSOBJECTS WHERE ")
            .Append(ObjectPredicate)
            .AppendLine(") = 0 THEN")
            .AppendLine("        BEGIN")
            .Append("            EXECUTE IMMEDIATE '")
            .Append(create)
            .AppendLine("';")
            .AppendLine("        EXCEPTION")
            .AppendLine("            WHEN OTHERS THEN")
            .Append("                IF (SELECT COUNT(*) FROM SYS.SYSOBJECTS WHERE ")
            .Append(ObjectPredicate)
            .AppendLine(") = 0 THEN")
            .AppendLine("                    RAISE;")
            .AppendLine("                END IF;")
            .AppendLine("        END;")
            .AppendLine("    END IF;")
            .AppendLine("END;")
            .ToString();
    }

    public override string GetBeginIfNotExistsScript(string migrationId)
        => new StringBuilder()
            .AppendLine("DECLARE CNT INT;")
            .AppendLine("BEGIN")
            .Append("    SELECT COUNT(*) INTO CNT FROM ")
            .Append(SqlGenerationHelper.DelimitIdentifier(TableName, TableSchema))
            .Append(" WHERE ")
            .Append(SqlGenerationHelper.DelimitIdentifier(MigrationIdColumnName))
            .Append(" = ")
            .Append(MigrationLiteral(migrationId))
            .AppendLine(";")
            .AppendLine("    IF CNT = 0 THEN")
            .ToString();

    public override string GetBeginIfExistsScript(string migrationId)
        => new StringBuilder()
            .AppendLine("DECLARE CNT INT;")
            .AppendLine("BEGIN")
            .Append("    SELECT COUNT(*) INTO CNT FROM ")
            .Append(SqlGenerationHelper.DelimitIdentifier(TableName, TableSchema))
            .Append(" WHERE ")
            .Append(SqlGenerationHelper.DelimitIdentifier(MigrationIdColumnName))
            .Append(" = ")
            .Append(MigrationLiteral(migrationId))
            .AppendLine(";")
            .AppendLine("    IF CNT = 1 THEN")
            .ToString();

    public override string GetEndIfScript()
        => new StringBuilder()
            .AppendLine("    END IF;")
            .AppendLine("END;")
            .AppendLine(SqlGenerationHelper.BatchTerminator)
            .ToString();

    public override IMigrationsDatabaseLock AcquireDatabaseLock()
    {
        RelationalLoggerExtensions.AcquiringMigrationLock(Dependencies.MigrationsLogger);

        var id = LockId;
        var owner = GrantNamedLock(id)
            ? this
            : null;

        return new DmMigrationDatabaseLock(this, owner, id);
    }

    public override async Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RelationalLoggerExtensions.AcquiringMigrationLock(Dependencies.MigrationsLogger);

        var id = LockId;
        var owner = await GrantNamedLockAsync(id, cancellationToken).ConfigureAwait(false)
            ? this
            : null;

        return new DmMigrationDatabaseLock(this, owner, id);
    }

    /// <summary>
    /// Releases the named lock, which the lock object does when the migration is done with it.
    /// </summary>
    /// <remarks>
    /// A failure here is not reported: the lock belongs to the session, so it is gone with the connection
    /// anyway, and a release that cannot run means the connection is already past using.
    /// </remarks>
    internal void ReleaseNamedLock(int id)
    {
        try
        {
            Dependencies.RawSqlCommandBuilder
                .Build(NamedLock("RELEASE", id))
                .ExecuteNonQuery(CreateParameterObject());
        }
        catch (DbException)
        {
        }
    }

    /// <summary>
    /// Waits for the named lock and answers whether this call took it, as opposed to finding it already held
    /// by this session.
    /// </summary>
    private bool GrantNamedLock(int id)
    {
        var outcome = Convert.ToInt32(
            Dependencies.RawSqlCommandBuilder
                .Build(NamedLock("REQUEST", id))
                .ExecuteScalar(CreateParameterObject()),
            CultureInfo.InvariantCulture);

        return Interpret(outcome, id);
    }

    private async Task<bool> GrantNamedLockAsync(int id, CancellationToken cancellationToken)
    {
        var outcome = Convert.ToInt32(
            await Dependencies.RawSqlCommandBuilder
                .Build(NamedLock("REQUEST", id))
                .ExecuteScalarAsync(CreateParameterObject(), cancellationToken)
                .ConfigureAwait(false),
            CultureInfo.InvariantCulture);

        return Interpret(outcome, id);
    }

    /// <summary>
    /// Reads the answer of <c>DBMS_LOCK.REQUEST</c>, which is <c>0</c> for a lock it granted, <c>4</c> for one
    /// the session already holds and <c>1</c> for a wait that ran out; anything else is a failure the server
    /// names. Measured on 8.1.4.80, a second session answered <c>1</c> while the first held the lock and
    /// <c>0</c> once it was released.
    /// </summary>
    private bool Interpret(int outcome, int id)
        => outcome switch
        {
            0 => true,
            4 => false,
            1 => throw new TimeoutException(
                $"Another deployment held the Dameng migration lock {id} for longer than {LockTimeout} second(s). "
                + "Raise the command timeout or wait for the other deployment to finish."),
            _ => throw new InvalidOperationException(
                $"DBMS_LOCK.REQUEST answered {outcome} for the Dameng migration lock {id}.")
        };

    /// <summary>
    /// The lock the migration takes, named after the database so that two deployments of the same database
    /// contend and deployments of different databases do not.
    /// </summary>
    private int LockId
        => DmNamedLock.Id(
            Dependencies.Connection.DbConnection.Database is { Length: > 0 } database
                ? database
                : "DM.EntityFrameworkCore");

    /// <summary>
    /// How long a deployment waits for another one, taken from the command timeout so that a deployment that
    /// waits too long reports it instead of hanging.
    /// </summary>
    private int LockTimeout => Dependencies.Connection.CommandTimeout ?? 30;

    /// <summary>
    /// The named lock call. <c>6</c> is <c>DBMS_LOCK.X_MODE</c>, the exclusive mode, and the request does not
    /// release on commit: measured on 8.1.4.80, the server commits around DDL, so a lock released at commit
    /// would be gone before the migration it guards had finished.
    /// </summary>
    private string NamedLock(string call, int id)
        => "SELECT DBMS_LOCK."
            + call
            + "("
            + id.ToString(CultureInfo.InvariantCulture)
            + (call == "REQUEST"
                ? ", 6, " + LockTimeout.ToString(CultureInfo.InvariantCulture) + ", FALSE"
                : string.Empty)
            + ") FROM DUAL"
            + SqlGenerationHelper.StatementTerminator;

    private RelationalCommandParameterObject CreateParameterObject()
        => new(
            Dependencies.Connection,
            parameterValues: null,
            readerColumns: null,
            Dependencies.CurrentContext.Context,
            Dependencies.CommandLogger,
            CommandSource.Migrations);

    private string CreateStatement()
        => GetCreateCommands() is [var command]
            ? command.CommandText.TrimEnd('\r', '\n', ';', ' ', '/')
            : throw new NotSupportedException(
                "The history table must be created by a single statement to generate an idempotent script.");

    private string MigrationLiteral(string migrationId)
        => Dependencies.TypeMappingSource.FindMapping(typeof(string))!.GenerateSqlLiteral(migrationId);

    /// <summary>
    /// Matches the history table by name within its schema. Without an explicit schema the
    /// caller's current schema is used, which is what an unqualified table name resolves to.
    /// </summary>
    private string ObjectPredicate
    {
        get
        {
            var schema = TableSchema is null
                ? " (SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA'))"
                : " " + MigrationLiteral(TableSchema);

            return "NAME = " + MigrationLiteral(TableName)
                + " AND SCHID = (SELECT ID FROM SYS.SYSOBJECTS WHERE TYPE$ = 'SCH' AND NAME ="
                + schema
                + ")";
        }
    }
}
