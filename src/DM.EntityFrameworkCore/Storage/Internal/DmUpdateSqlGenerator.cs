using System.Text;
using Microsoft.EntityFrameworkCore;
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

        AppendInsertCommandHeader(commandStringBuilder, command.TableName, command.Schema, writeOperations);
        AppendValuesHeader(commandStringBuilder, writeOperations);
        AppendValues(commandStringBuilder, command.TableName, command.Schema, writeOperations);
        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        requiresTransaction = false;

        if (readOperations.Count == 0)
        {
            return ResultSetMapping.NoResults;
        }

        AppendReadBack(commandStringBuilder, command, readOperations);

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
        RejectReadOperations(command, "update");

        AppendUpdateCommandHeader(commandStringBuilder, command.TableName, command.Schema, writeOperations);
        AppendWhereClause(commandStringBuilder, conditionOperations);
        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        requiresTransaction = false;

        return ResultSetMapping.NoResults;
    }

    public override ResultSetMapping AppendDeleteOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
    {
        var conditionOperations = command.ColumnModifications.Where(c => c.IsCondition).ToList();
        RejectReadOperations(command, "delete");

        AppendDeleteCommandHeader(commandStringBuilder, command.TableName, command.Schema);
        AppendWhereClause(commandStringBuilder, conditionOperations);
        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        requiresTransaction = false;

        return ResultSetMapping.NoResults;
    }

    /// <summary>
    /// Reads the generated columns back from the row the insert created. A key column that is
    /// being read is the identity column, so it is matched with <c>scope_identity()</c>; the
    /// remaining key columns are matched on their parameters, which are still known here.
    /// </summary>
    private void AppendReadBack(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        IReadOnlyList<IColumnModification> readOperations)
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

        commandStringBuilder
            .AppendLine()
            .Append("WHERE sql%ROWCOUNT = 1");

        foreach (var key in ReadKeys(command))
        {
            commandStringBuilder.Append(" AND ");
            SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, key.ColumnName);
            commandStringBuilder.Append(" = ").Append(KeyCondition(command.TableName, key));
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

    private static IEnumerable<IColumnModification> ReadKeys(IReadOnlyModificationCommand command)
        => command.ColumnModifications.Where(c => c.IsKey);

    private static void RejectReadOperations(IReadOnlyModificationCommand command, string operation)
    {
        var readOperation = command.ColumnModifications.FirstOrDefault(c => c.IsRead);

        if (readOperation is not null)
        {
            throw new NotSupportedException(
                $"Reading the store generated value of '{command.TableName}.{readOperation.ColumnName}' after an "
                + $"{operation} is not implemented. This provider reads values back only after an insert, which is "
                + "where the generated key appears.");
        }
    }
}
