using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Generates INSERT, UPDATE and DELETE statements.
/// </summary>
/// <remarks>
/// The relational base implementation appends a PostgreSQL-style <c>RETURNING</c> clause to every
/// statement, including a bare <c>RETURNING 1</c> that exists only to report the affected row
/// count. Dameng requires <c>RETURNING ... INTO &lt;variable&gt;</c>, which binds to a PL/SQL
/// variable rather than producing a result set, so the clause is not usable here.
/// <para>
/// A store-generated value is therefore read by a second statement: the insert runs, then a
/// <c>SELECT</c> reads the row back. The <c>sql%ROWCOUNT = 1</c> predicate makes the select return
/// nothing when the insert did not affect exactly one row, so a lost update is reported as a
/// concurrency failure instead of a silent success, and <c>scope_identity()</c> identifies the row
/// the insert just created.
/// </para>
/// </remarks>
internal sealed class DmUpdateSqlGenerator : UpdateSqlGenerator
{
    public DmUpdateSqlGenerator(UpdateSqlGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }

    public override ResultSetMapping AppendInsertOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
    {
        var writeOperations = command.ColumnModifications.Where(c => c.IsWrite).ToList();
        var readOperations = command.ColumnModifications.Where(c => c.IsRead).ToList();
        var identityColumn = writeOperations.FirstOrDefault(IsIdentityWrite);

        if (identityColumn is not null)
        {
            AppendIdentityInsert(commandStringBuilder, command, on: true);
        }

        AppendInsertCommandHeader(commandStringBuilder, command.TableName, command.Schema, writeOperations);
        AppendValuesHeader(commandStringBuilder, writeOperations);
        AppendValues(commandStringBuilder, command.TableName, command.Schema, writeOperations);
        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        if (identityColumn is not null)
        {
            AppendIdentityInsert(commandStringBuilder, command, on: false);
        }

        requiresTransaction = false;

        if (readOperations.Count == 0)
        {
            return ResultSetMapping.NoResults;
        }

        AppendReadBack(commandStringBuilder, command, readOperations, inserted: true);

        return ResultSetMapping.LastInResultSet;
    }

    public override ResultSetMapping AppendUpdateOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
    {
        var writeOperations = command.ColumnModifications.Where(c => c.IsWrite).ToList();
        var conditionOperations = command.ColumnModifications.Where(c => c.IsCondition).ToList();
        var readOperations = command.ColumnModifications.Where(c => c.IsRead).ToList();

        AppendUpdateCommandHeader(commandStringBuilder, command.TableName, command.Schema, writeOperations);
        AppendWhereClause(commandStringBuilder, conditionOperations);
        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        requiresTransaction = false;

        if (readOperations.Count == 0)
        {
            return ResultSetMapping.NoResults;
        }

        AppendReadBack(commandStringBuilder, command, readOperations, inserted: false);

        return ResultSetMapping.LastInResultSet;
    }

    public override ResultSetMapping AppendDeleteOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
    {
        var conditionOperations = command.ColumnModifications.Where(c => c.IsCondition).ToList();

        AppendDeleteCommandHeader(commandStringBuilder, command.TableName, command.Schema);
        AppendWhereClause(commandStringBuilder, conditionOperations);
        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        requiresTransaction = false;

        return ResultSetMapping.NoResults;
    }

    /// <summary>
    /// Writes a member of a JSON document with <c>JSON_SET</c>, measured on 8.1.4.80: it answers
    /// <c>{"a":2}</c> for <c>JSON_SET('{"a":1}', '$.a', 2)</c>. The framework reports a change inside a
    /// document as a value for a path rather than as the whole document, so the base rendering would write
    /// that value into the column and replace the document with it, which is data loss rather than an
    /// error. Dameng has no <c>JSON_MODIFY</c> and no <c>JSON_PUT</c>.
    /// </summary>
    protected override void AppendUpdateColumnValue(
        ISqlGenerationHelper updateSqlGeneratorHelper,
        IColumnModification columnModification,
        StringBuilder stringBuilder,
        string name,
        string? schema)
    {
        // The root path is the document itself, and JSON_SET(doc, '$', '{"b":2}') answers the document
        // as a quoted string rather than as a document, measured on 8.1.4.80, so it is written plainly.
        if (columnModification.JsonPath is not { } jsonPath || jsonPath == "$")
        {
            base.AppendUpdateColumnValue(updateSqlGeneratorHelper, columnModification, stringBuilder, name, schema);

            return;
        }

        // Only the value is written here: the header writes the assignment. The value itself stays the
        // base generator's business, so its parameter and its shape are unchanged.
        stringBuilder
            .Append("JSON_SET(")
            .Append(updateSqlGeneratorHelper.DelimitIdentifier(columnModification.ColumnName))
            .Append(", '")
            .Append(jsonPath.Replace("'", "''", StringComparison.Ordinal))
            .Append("', ");

        base.AppendUpdateColumnValue(updateSqlGeneratorHelper, columnModification, stringBuilder, name, schema);

        stringBuilder.Append(')');
    }

    /// <summary>
    /// Dameng refuses to write a value into an identity column unless the session says so for that table,
    /// which is the rule SQL Server has as well: <c>仅当指定列列表，且SET IDENTITY_INSERT为ON时，才能对自增列赋值</c>.
    /// The flag is set before the insert and cleared in the same command, so a pooled connection is not left
    /// holding it for the next statement.
    /// </summary>
    private void AppendIdentityInsert(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        bool on)
    {
        commandStringBuilder.Append("SET IDENTITY_INSERT ");
        SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, command.TableName, command.Schema);
        commandStringBuilder.Append(on ? " ON" : " OFF").AppendLine(SqlGenerationHelper.StatementTerminator);
    }

    /// <summary>
    /// Whether the statement writes a value into an identity column. A generated key that the caller left
    /// unset is not written at all, so only an explicit value needs the flag.
    /// </summary>
    private static bool IsIdentityWrite(IColumnModification modification)
        => modification.IsWrite
            && modification.Property?.GetDmValueGenerationStrategy() == DmValueGenerationStrategy.IdentityColumn;

    /// <summary>
    /// Reads the generated columns back from the row the statement just wrote. An insert is matched
    /// by the affected row count and the key the insert produced; an update repeats the where clause
    /// of the statement it follows. Either way a statement that matched nothing reads nothing, so the
    /// caller reports a concurrency conflict rather than a stale value.
    /// </summary>
    private void AppendReadBack(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        IReadOnlyList<IColumnModification> readOperations,
        bool inserted)
    {
        commandStringBuilder.Append("SELECT ");

        for (var i = 0; i < readOperations.Count; i++)
        {
            if (i > 0)
            {
                commandStringBuilder.Append(", ");
            }

            SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, readOperations[i].ColumnName);
        }

        commandStringBuilder
            .AppendLine()
            .Append("FROM ");

        SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, command.TableName, command.Schema);

        var keys = command.ColumnModifications.Where(c => c.IsKey).ToList();

        if (inserted)
        {
            commandStringBuilder
                .AppendLine()
                .Append("WHERE sql%ROWCOUNT = 1");

            foreach (var key in keys)
            {
                commandStringBuilder.Append(" AND ");
                SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, key.ColumnName);
                commandStringBuilder.Append(" = ").Append(KeyCondition(command.TableName, key));
            }
        }
        else
        {
            // The update already matched the row, so the read repeats that where clause and binds to
            // the same parameters, which only the generator can name.
            AppendWhereClause(commandStringBuilder, keys);
        }

        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);
    }

    /// <summary>
    /// Identifies the row the insert created. An identity column is found through
    /// <c>scope_identity()</c>; a column the store fills from a sequence default is found through
    /// that sequence's current value, which is the same expression with <c>NEXTVAL</c> replaced, so
    /// the row is matched on the value this session just produced. A key whose value is known
    /// before the insert is matched on its parameter.
    /// </summary>
    private static string KeyCondition(string tableName, IColumnModification key)
    {
        if (!key.IsRead)
        {
            return ":" + key.ParameterName;
        }

        var defaultSql = key.Property?.GetDefaultValueSql();

        if (defaultSql is null)
        {
            return "scope_identity()";
        }

        var currentValueSql = defaultSql.Replace(".NEXTVAL", ".CURRVAL", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(currentValueSql, defaultSql, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"The key '{tableName}.{key.ColumnName}' is generated by '{defaultSql}', which this provider "
                + "cannot read back: the row is identified through the sequence current value, so the default "
                + "has to advance a sequence with NEXTVAL. Use an identity column for the key instead.");
        }

        return currentValueSql;
    }
}