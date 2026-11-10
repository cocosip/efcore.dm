using System.Text;
using Microsoft.EntityFrameworkCore.Update;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Generates INSERT, UPDATE and DELETE statements.
/// </summary>
/// <remarks>
/// The relational base implementation appends a PostgreSQL-style <c>RETURNING</c> clause to
/// every statement, including a bare <c>RETURNING 1</c> that exists only to report the affected
/// row count. Dameng requires <c>RETURNING ... INTO &lt;bind&gt;</c>, so the clause is omitted
/// here and the affected row count is taken from the driver's return value instead.
/// <para>
/// Consequence: reading store-generated values back is not supported yet, so a statement that
/// needs it is rejected rather than executed with a silently stale value.
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
        RejectReadOperations(command);

        AppendInsertCommandHeader(commandStringBuilder, command.TableName, command.Schema, writeOperations);
        AppendValuesHeader(commandStringBuilder, writeOperations);
        AppendValues(commandStringBuilder, command.TableName, command.Schema, writeOperations);
        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        requiresTransaction = false;

        return ResultSetMapping.NoResults;
    }

    public override ResultSetMapping AppendUpdateOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
    {
        var writeOperations = command.ColumnModifications.Where(c => c.IsWrite).ToList();
        var conditionOperations = command.ColumnModifications.Where(c => c.IsCondition).ToList();
        RejectReadOperations(command);

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
        RejectReadOperations(command);

        AppendDeleteCommandHeader(commandStringBuilder, command.TableName, command.Schema);
        AppendWhereClause(commandStringBuilder, conditionOperations);
        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        requiresTransaction = false;

        return ResultSetMapping.NoResults;
    }

    private static void RejectReadOperations(IReadOnlyModificationCommand command)
    {
        var readOperation = command.ColumnModifications.FirstOrDefault(c => c.IsRead);

        if (readOperation is not null)
        {
            throw new NotSupportedException(
                $"Reading the store generated value of '{command.TableName}.{readOperation.ColumnName}' is not "
                + "implemented. Dameng reads values back through RETURNING ... INTO, which this provider does not "
                + "generate yet, so the operation is rejected instead of leaving the property stale.");
        }
    }
}
