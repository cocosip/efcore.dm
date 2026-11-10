using System.Globalization;
using DM.EntityFrameworkCore.Metadata;
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

        if (FindIdentity(schema, table, name, operation, model) is var (seed, increment))
        {
            builder
                .Append(" IDENTITY(")
                .Append(seed.ToString(CultureInfo.InvariantCulture))
                .Append(",")
                .Append(increment.ToString(CultureInfo.InvariantCulture))
                .Append(")");
        }

        builder.Append(operation.IsNullable ? " NULL" : " NOT NULL");

        DefaultValue(operation.DefaultValue, operation.DefaultValueSql, columnType, builder);
    }

    /// <summary>
    /// The identity a column generates, or null. A migration the differ produced carries the attribute
    /// as an annotation, which is what the generated migration code shows and what a replay reads; a
    /// hand written migration carries nothing, so the model decides there.
    /// </summary>
    private static (int Seed, int Increment)? FindIdentity(
        string? schema,
        string table,
        string name,
        ColumnOperation operation,
        IModel? model)
    {
        if (operation[DmPropertyExtensions.ValueGenerationStrategyAnnotation]
            is DmValueGenerationStrategy.IdentityColumn)
        {
            return (
                operation[DmAnnotationNames.IdentitySeed] as int? ?? 1,
                operation[DmAnnotationNames.IdentityIncrement] as int? ?? 1);
        }

        return FindIdentityProperty(schema, table, name, model) is { } property
            ? (property.GetDmIdentitySeed(), property.GetDmIdentityIncrement())
            : null;
    }

    /// <summary>
    /// The property the model maps to a column, when that column generates its value. The rule lives
    /// with the annotation provider, so a migration the differ produced and a hand written one agree.
    /// </summary>
    private static IProperty? FindIdentityProperty(string? schema, string table, string name, IModel? model)
        => model?.GetRelationalModel()
            .FindTable(table, schema)
            ?.FindColumn(name) is { } column
            ? DmAnnotationProvider.IdentityProperty(column)
            : null;

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

    /// <summary>
    /// The base class refuses an operation it does not know, and every operation below is this
    /// provider's own, so they are dispatched here before it can.
    /// </summary>
    protected override void Generate(
        MigrationOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        switch (operation)
        {
            case DmCreateUserOperation createUser:
                Generate(createUser, builder);
                break;
            case DmDropUserOperation dropUser:
                Generate(dropUser, builder);
                break;
            case DmCreateSchemaOperation createSchema:
                Generate(createSchema, builder);
                break;
            case DmDropSchemaOperation dropSchema:
                Generate(dropSchema, builder);
                break;
            default:
                base.Generate(operation, model, builder);
                break;
        }
    }

    /// <summary>
    /// A schema belongs to a user, so the schema is created in a statement of its own once the user
    /// exists. Each statement is its own command, because the driver runs one statement per round
    /// trip.
    /// </summary>
    private void Generate(DmCreateUserOperation operation, MigrationCommandListBuilder builder)
    {
        builder
            .Append("CREATE USER ")
            .Append(Helper.DelimitIdentifier(operation.UserName))
            .Append(" IDENTIFIED BY ")
            .Append(Quote(operation.Password));

        EndStatement(builder);

        if (!string.IsNullOrEmpty(operation.Schema))
        {
            builder
                .Append("CREATE SCHEMA ")
                .Append(Helper.DelimitIdentifier(operation.Schema))
                .Append(" AUTHORIZATION ")
                .Append(Helper.DelimitIdentifier(operation.UserName));

            EndStatement(builder);
        }
    }

    private void Generate(DmDropUserOperation operation, MigrationCommandListBuilder builder)
    {
        builder
            .Append("DROP USER ")
            .Append(Helper.DelimitIdentifier(operation.UserName))
            .Append(" CASCADE");

        EndStatement(builder);
    }

    private void Generate(DmCreateSchemaOperation operation, MigrationCommandListBuilder builder)
    {
        builder
            .Append("CREATE SCHEMA ")
            .Append(Helper.DelimitIdentifier(operation.Name))
            .Append(" AUTHORIZATION ")
            .Append(Helper.DelimitIdentifier(operation.UserName));

        EndStatement(builder);
    }

    private void Generate(DmDropSchemaOperation operation, MigrationCommandListBuilder builder)
    {
        builder
            .Append("DROP SCHEMA ")
            .Append(Helper.DelimitIdentifier(operation.Name))
            .Append(" CASCADE");

        EndStatement(builder);
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
