using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// Generates DDL for migrations.
/// </summary>
/// <remarks>
/// The relational base implementation emits standard DDL through <see cref="ISqlGenerationHelper" />
/// and the type mapping source, both of which the provider supplies. The operations overridden
/// here are the ones the base class either leaves unimplemented or spells in a dialect Dameng
/// does not accept.
/// </remarks>
internal sealed class DmMigrationsSqlGenerator : MigrationsSqlGenerator
{
    public DmMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }

    private ISqlGenerationHelper Helper => Dependencies.SqlGenerationHelper;

    /// <summary>
    /// Dameng declares an identity column with <c>IDENTITY(seed, increment)</c> between the type
    /// and the nullability, so this cannot reuse the base method and append afterwards.
    /// </summary>
    protected override void ColumnDefinition(
        string? schema,
        string table,
        string name,
        ColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        if (operation.ComputedColumnSql is not null)
        {
            base.ColumnDefinition(schema, table, name, operation, model, builder);

            return;
        }

        var columnType = operation.ColumnType ?? GetColumnType(schema, table, name, operation, model);

        builder
            .Append(Helper.DelimitIdentifier(name))
            .Append(" ")
            .Append(columnType);

        if (operation.Collation is not null)
        {
            builder
                .Append(" COLLATE ")
                .Append(operation.Collation);
        }

        if (FindIdentityProperty(schema, table, name, model) is { } identity)
        {
            builder
                .Append(" IDENTITY(")
                .Append(identity.GetDmIdentitySeed().ToString(CultureInfo.InvariantCulture))
                .Append(",")
                .Append(identity.GetDmIdentityIncrement().ToString(CultureInfo.InvariantCulture))
                .Append(")");
        }

        builder.Append(operation.IsNullable ? " NULL" : " NOT NULL");

        DefaultValue(operation.DefaultValue, operation.DefaultValueSql, columnType, builder);
    }

    /// <summary>
    /// Reads the strategy from the model rather than from the operation, because the operation
    /// carries no provider annotation: the differ copies the model, not the conventions.
    /// </summary>
    private static IProperty? FindIdentityProperty(string? schema, string table, string name, IModel? model)
    {
        var property = model?.GetRelationalModel()
            .FindTable(table, schema)
            ?.FindColumn(name)
            ?.PropertyMappings.FirstOrDefault()
            ?.Property;

        return property?.GetDmValueGenerationStrategy() is DmValueGenerationStrategy.IdentityColumn ? property : null;
    }

    /// <summary>
    /// Dameng alters a column with <c>MODIFY</c>. The base class does not implement this
    /// operation at all, so there is no dialect to correct.
    /// </summary>
    protected override void Generate(
        AlterColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .Append("ALTER TABLE ")
            .Append(Helper.DelimitIdentifier(operation.Table, operation.Schema))
            .Append(" MODIFY (")
            .Append(Helper.DelimitIdentifier(operation.Name))
            .Append(" ")
            .Append(operation.ColumnType ?? DefaultStoreType(operation.ClrType));

        if (!operation.IsNullable)
        {
            builder.Append(" NOT NULL");
        }

        var defaultLiteral = DefaultLiteral(operation);

        if (defaultLiteral is not null)
        {
            builder.Append(" DEFAULT ").Append(defaultLiteral);
        }

        builder.Append(")");

        EndStatement(builder);
    }

    protected override void Generate(
        RenameColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .Append("ALTER TABLE ")
            .Append(Helper.DelimitIdentifier(operation.Table, operation.Schema))
            .Append(" RENAME COLUMN ")
            .Append(Helper.DelimitIdentifier(operation.Name))
            .Append(" TO ")
            .Append(Helper.DelimitIdentifier(operation.NewName));

        EndStatement(builder);
    }

    protected override void Generate(
        RenameTableOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .Append("ALTER TABLE ")
            .Append(Helper.DelimitIdentifier(operation.Name, operation.Schema))
            .Append(" RENAME TO ")
            .Append(Helper.DelimitIdentifier(operation.NewName ?? operation.Name));

        EndStatement(builder);
    }

    protected override void Generate(
        RenameIndexOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .Append("ALTER INDEX ")
            .Append(Helper.DelimitIdentifier(operation.Name, operation.Schema))
            .Append(" RENAME TO ")
            .Append(Helper.DelimitIdentifier(operation.NewName ?? operation.Name));

        EndStatement(builder);
    }

    protected override void Generate(
        DropIndexOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        // The table is not named: in Dameng the index belongs to the schema, not to the table.
        builder
            .Append("DROP INDEX ")
            .Append(Helper.DelimitIdentifier(operation.Name, operation.Schema));

        if (terminate)
        {
            EndStatement(builder);
        }
    }

    /// <summary>
    /// A Dameng sequence takes no type clause and spells its boundary and cycle options as one word,
    /// so the statement the base class builds is not valid here.
    /// </summary>
    protected override void Generate(
        CreateSequenceOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .Append("CREATE SEQUENCE ")
            .Append(Helper.DelimitIdentifier(operation.Name, operation.Schema))
            .Append(" START WITH ")
            .Append(Number(operation.StartValue));

        AppendSequenceOptions(operation, builder);

        EndStatement(builder);
    }

    /// <summary>
    /// An alteration repeats every option, so that changing one of them does not silently keep the
    /// previous value.
    /// </summary>
    protected override void SequenceOptions(
        AlterSequenceOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        AppendSequenceOptions(operation, builder);
    }

    private static void AppendSequenceOptions(SequenceOperation operation, MigrationCommandListBuilder builder)
    {
        builder.Append(" INCREMENT BY ").Append(Number(operation.IncrementBy));

        builder.Append(
            operation.MinValue.HasValue
                ? " MINVALUE " + Number(operation.MinValue.Value)
                : " NOMINVALUE");

        builder.Append(
            operation.MaxValue.HasValue
                ? " MAXVALUE " + Number(operation.MaxValue.Value)
                : " NOMAXVALUE");

        builder.Append(operation.IsCyclic ? " CYCLE" : " NOCYCLE");
    }

    /// <summary>
    /// Dameng creates a schema with <c>CREATE SCHEMA</c>, and the statement fails when the schema is
    /// already there, which is the case when a database administrator created it. The block asks the
    /// catalog first, so a migration is also safe against a schema the user made.
    /// </summary>
    /// <remarks>
    /// The catalog query is the one the Dameng provider uses; it is confirmed against a server with
    /// the rest of the migration DDL.
    /// </remarks>
    protected override void Generate(
        EnsureSchemaOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        var name = operation.Name;

        builder
            .AppendLine("DECLARE")
            .AppendLine("    declared INT;")
            .AppendLine("BEGIN")
            .Append("    SELECT COUNT(*) INTO declared FROM SYS.SYSOBJECTS WHERE TYPE$ = 'SCH' AND NAME = ")
            .Append(Quote(name))
            .AppendLine(";")
            .AppendLine("    IF declared = 0 THEN")
            .Append("        EXECUTE IMMEDIATE ")
            .Append(Quote("CREATE SCHEMA " + Helper.DelimitIdentifier(name)))
            .AppendLine(";")
            .AppendLine("    END IF;")
            .Append("END;");

        builder.EndCommand();
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Quotes a string literal for a statement body.</summary>
    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private void EndStatement(MigrationCommandListBuilder builder)
        => builder.AppendLine(Helper.StatementTerminator).EndCommand();

    private string DefaultStoreType(Type clrType)
        => Dependencies.TypeMappingSource.FindMapping(clrType)?.StoreType
            ?? throw new NotSupportedException(
                $"No store type is known for the CLR type '{clrType.Name}', so the column cannot be altered.");

    private string? DefaultLiteral(AlterColumnOperation operation)
    {
        if (operation.DefaultValueSql is not null)
        {
            return operation.DefaultValueSql;
        }

        return operation.DefaultValue is null
            ? null
            : Dependencies.TypeMappingSource.FindMapping(operation.ClrType)?.GenerateSqlLiteral(operation.DefaultValue);
    }
}
