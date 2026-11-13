using System.Text.RegularExpressions;
using System.Text;
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
/// </remarks>
internal sealed class DmHistoryRepository : HistoryRepository
{
    public DmHistoryRepository(HistoryRepositoryDependencies dependencies)
        : base(dependencies)
    {
    }

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
    /// failure only when the object is there afterwards, so the session that lost the race carries on, and a
    /// create that fails for any other reason still raises.
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

    /// <summary>
    /// The single statement that creates the history table. EF Core 8 publishes the create script rather
    /// than its commands, so the script is split on the batch terminator and a script of more than one
    /// statement is refused: the idempotent wrapper runs one statement inside a block.
    /// </summary>
    private string CreateStatement()
    {
        var statements = GetCreateScript()
            .Split(SqlGenerationHelper.BatchTerminator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return statements.Length == 1
            ? statements[0].TrimEnd(';', ' ', '\r', '\n', '/')
            : throw new NotSupportedException(
                "The history table must be created by a single statement to generate an idempotent script.");
    }

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
