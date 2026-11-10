using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
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
/// The lock is a table lock, taken in exclusive mode. It is released by the transaction, so a
/// crashed migration cannot leave a stale lock row behind — unlike a lock table, which has to be
/// cleared by hand after a failure.
/// </para>
/// </remarks>
internal sealed class DmHistoryRepository : HistoryRepository
{
    public DmHistoryRepository(HistoryRepositoryDependencies dependencies)
        : base(dependencies)
    {
    }

    public override LockReleaseBehavior LockReleaseBehavior => LockReleaseBehavior.Transaction;

    /// <summary>
    /// Dameng keeps its catalog in <c>SYS.SYSOBJECTS</c>, which is the only place that knows
    /// whether an object exists regardless of the caller's privileges on it.
    /// </summary>
    protected override string ExistsSql => "SELECT ID FROM SYS.SYSOBJECTS WHERE " + ObjectPredicate + ";";

    protected override bool InterpretExistsResult(object? value) => value is not null;

    public override string GetCreateIfNotExistsScript()
        => new StringBuilder()
            .AppendLine("BEGIN")
            .Append("    IF (SELECT COUNT(*) FROM SYS.SYSOBJECTS WHERE ")
            .Append(ObjectPredicate)
            .AppendLine(") = 0 THEN")
            .Append("        EXECUTE IMMEDIATE '")
            .Append(CreateStatement().Replace("'", "''", StringComparison.Ordinal))
            .AppendLine("';")
            .AppendLine("    END IF;")
            .AppendLine("END;")
            .ToString();

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
        Dependencies.RawSqlCommandBuilder.Build(LockSql).ExecuteNonQuery(CreateParameterObject());

        return new DmMigrationDatabaseLock(this);
    }

    public override async Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RelationalLoggerExtensions.AcquiringMigrationLock(Dependencies.MigrationsLogger);

        await Dependencies.RawSqlCommandBuilder
            .Build(LockSql)
            .ExecuteNonQueryAsync(CreateParameterObject(), cancellationToken)
            .ConfigureAwait(false);

        return new DmMigrationDatabaseLock(this);
    }

    private string LockSql
        => "LOCK TABLE "
            + SqlGenerationHelper.DelimitIdentifier(TableName, TableSchema)
            + " IN EXCLUSIVE MODE"
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
