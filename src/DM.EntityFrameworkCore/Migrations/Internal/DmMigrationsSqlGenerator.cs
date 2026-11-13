using System.Globalization;
using DM.EntityFrameworkCore.Metadata;
using DM.EntityFrameworkCore.Dialect;
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
        // On a column the model did not type, the framework would have refused the model already.
        builder
            .Append(Helper.DelimitIdentifier(name))
            .Append(" ")
            .Append(columnType!);

        if (operation.Collation is not null)
        {
            builder
                .Append(" COLLATE ")
                .Append(Helper.DelimitIdentifier(operation.Collation));
        }

        // The identity is written only on a column type that accepts one, which is the server's own rule:
        // measured on 8.1.4.80, `IDENTITY` on `TINYINT` and `SMALLINT` answered `非法IDENTITY列类型` while
        // `INT`, `BIGINT`, `INTEGER`, `NUMBER` and `DECIMAL` were accepted. A converted key reaches this
        // point with its store type, so the attribute is what the column can carry rather than what the
        // CLR type suggested.
        if (DmIdentityTypes.CanCarryIdentity(columnType)
            && FindIdentity(schema, table, name, operation, model) is var (seed, increment))
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
    /// A computed column is a virtual column the server derives from the others.
    /// </summary>
    /// <remarks>
    /// The base class leaves this unwritten, so a model with a computed column could not produce a script at
    /// all. Measured on 8.1.4.80: `GENERATED ALWAYS AS (expression)` was accepted and answered 42 for a column
    /// derived from 41, as were the same clause with `VIRTUAL` and the shorter `AS (expression)`, while
    /// `STORED` was a syntax error (`语法分析出错`), so a stored column is refused rather than miswritten.
    /// </remarks>
    protected override void ComputedColumnDefinition(
        string? schema,
        string table,
        string name,
        ColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        if (operation.IsStored == true)
        {
            throw new NotSupportedException(
                $"The computed column '{name}' on '{table}' is stored, and Dameng has no stored computed column: "
                + "measured on 8.1.4.80, `GENERATED ALWAYS AS (...) STORED` answered `语法分析出错`. "
                + "Declare the column as a virtual one.");
        }

        // The server refuses a data type on a virtual column: measured on 8.1.4.80, the definition with
        // one answered `不支持为虚拟列指定的数据类型`, while the definition without it was accepted.
        builder
            .Append(Helper.DelimitIdentifier(name))
            .Append(" GENERATED ALWAYS AS (")
            .Append(operation.ComputedColumnSql!)
            .Append(")");
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
        if (operation[DmAnnotationNames.Identity] is not null
            || operation[DmPropertyExtensions.ValueGenerationStrategyAnnotation]
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

        // A comment is a statement of its own on this server, and the wrapper emits it after this method.
        // Repeating the whole column definition for a comment would fail on an identity column, which the
        // server refuses to modify at all.
        if (OnlyCommentChanged(operation))
        {
            return;
        }

        DropIdentityIfLost(operation, model, builder);

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

        if (!string.Equals(operation.Schema, operation.NewSchema, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"The table '{operation.Name}' is moved to the schema '{operation.NewSchema}', and Dameng has no "
                + "statement for it: measured on 8.1.4.80, `ALTER TABLE ... RENAME TO schema.name`, "
                + "`SET SCHEMA` and `MOVE SCHEMA` all answered `语法分析出错`. Drop and create the table in the new "
                + "schema instead.");
        }

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

        if (_idempotent)
        {
            GenerateIfMissing(
                builder,
                Exists("ALL_SEQUENCES", "SEQUENCE_OWNER", "SEQUENCE_NAME", operation.Schema, operation.Name),
                inner => GenerateSequence(operation, inner));

            return;
        }

        GenerateSequence(operation, builder);
    }

    private void GenerateSequence(CreateSequenceOperation operation, MigrationCommandListBuilder builder)
    {
        builder
            .Append("CREATE SEQUENCE ")
            .Append(Helper.DelimitIdentifier(operation.Name, operation.Schema))
            .Append(" START WITH ")
            .Append(Number(operation.StartValue));

        AppendSequenceOptions(operation, builder);

        EndStatement(builder);
    }

    /// <summary>
    /// Dameng renames a sequence inside its own schema; a move to another schema is not a form the server has.
    /// </summary>
    /// <remarks>
    /// Measured on 8.1.4.80: `ALTER SEQUENCE s RENAME TO t` was accepted, while the same statement with a
    /// schema-qualified new name was rejected with `语法分析出错`.
    /// </remarks>
    protected override void Generate(
        RenameSequenceOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        if (!string.Equals(operation.Schema, operation.NewSchema, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"The sequence '{operation.Name}' is moved to the schema '{operation.NewSchema}', and Dameng "
                + "renames a sequence only inside its own schema: measured on 8.1.4.80, "
                + "`ALTER SEQUENCE ... RENAME TO schema.name` answered `语法分析出错`. Drop and create the "
                + "sequence in the new schema instead.");
        }

        builder.Append("ALTER SEQUENCE ");
        AppendName(builder, operation.Schema, operation.Name);
        builder.Append(" RENAME TO ")
            .Append(Helper.DelimitIdentifier(operation.NewName ?? operation.Name));

        EndStatement(builder);
    }

    /// <summary>
    /// Dameng has no statement that sets the current value of a sequence.
    /// </summary>
    /// <remarks>
    /// Measured on 8.1.4.80: both `ALTER SEQUENCE ... RESTART` and `ALTER SEQUENCE ... RESTART WITH n`
    /// answered `未指定 ALTER SEQUENCE 的选项`.
    /// </remarks>
    protected override void Generate(
        RestartSequenceOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
        => throw new NotSupportedException(
            $"The sequence '{operation.Name}' is restarted, and Dameng has no statement for it: measured on "
            + "8.1.4.80, `ALTER SEQUENCE ... RESTART [WITH n]` answered `未指定 ALTER SEQUENCE 的选项`. "
            + "Drop and create the sequence instead.");

    private void AppendName(MigrationCommandListBuilder builder, string? schema, string name)
    {
        if (!string.IsNullOrEmpty(schema))
        {
            builder
                .Append(Helper.DelimitIdentifier(schema))
                .Append(".");
        }

        builder.Append(Helper.DelimitIdentifier(name));
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
    /// Records the requested script flavour, so that an operation can wrap itself when the script has
    /// to be safe to run against a database that already holds part of it.
    /// </summary>
    public override IReadOnlyList<MigrationCommand> Generate(
        IReadOnlyList<MigrationOperation> operations,
        IModel? model = null,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        _idempotent = options.HasFlag(MigrationsSqlGenerationOptions.Idempotent);

        try
        {
            return base.Generate(DeferSelfReferencingForeignKeys(operations), model, options);
        }
        finally
        {
            _idempotent = false;
        }
    }

    /// <summary>
    /// A self-referencing foreign key is added after the indexes of the script, because the server
    /// indexes the referencing columns when the constraint is created and then refuses an index on
    /// the same columns.
    /// </summary>
    /// <remarks>
    /// Measured on 8.1.4.80: a table created with a foreign key to itself answered `此列列表已索引`
    /// for a later `CREATE INDEX` on the referencing column, while the same constraint added with
    /// `ALTER TABLE` after the index was accepted. The constraint keeps its name, the model's index
    /// keeps its name, and the server keeps the index it made for the constraint.
    /// </remarks>
    private static IReadOnlyList<MigrationOperation> DeferSelfReferencingForeignKeys(
        IReadOnlyList<MigrationOperation> operations)
    {
        var deferred = new List<AddForeignKeyOperation>();
        var reordered = new List<MigrationOperation>(operations.Count);

        foreach (var operation in operations)
        {
            if (operation is CreateTableOperation createTable
                && createTable.ForeignKeys.Any(key => IsSelfReferencing(createTable.Schema, createTable.Name, key)))
            {
                var kept = new List<AddForeignKeyOperation>();

                foreach (var key in createTable.ForeignKeys)
                {
                    if (IsSelfReferencing(createTable.Schema, createTable.Name, key))
                    {
                        deferred.Add(key);
                    }
                    else
                    {
                        kept.Add(key);
                    }
                }

                reordered.Add(WithoutForeignKeys(createTable, kept));

                continue;
            }

            if (operation is AddForeignKeyOperation addForeignKey
                && IsSelfReferencing(addForeignKey.Schema, addForeignKey.Table, addForeignKey))
            {
                deferred.Add(addForeignKey);

                continue;
            }

            reordered.Add(operation);
        }

        reordered.AddRange(deferred);

        return reordered;
    }

    private static bool IsSelfReferencing(string? schema, string table, AddForeignKeyOperation key)
        => key.PrincipalTable == table
            && (key.PrincipalSchema ?? schema) == schema;

    private static CreateTableOperation WithoutForeignKeys(
        CreateTableOperation operation,
        List<AddForeignKeyOperation> foreignKeys)
    {
        var copy = new CreateTableOperation
        {
            Name = operation.Name,
            Schema = operation.Schema,
            Comment = operation.Comment,
            PrimaryKey = operation.PrimaryKey
        };

        copy.Columns.AddRange(operation.Columns);
        copy.UniqueConstraints.AddRange(operation.UniqueConstraints);
        copy.CheckConstraints.AddRange(operation.CheckConstraints);
        copy.ForeignKeys.AddRange(foreignKeys);

        foreach (var annotation in operation.GetAnnotations())
        {
            copy.AddAnnotation(annotation.Name, annotation.Value);
        }

        return copy;
    }

    /// <summary>
    /// Dameng has no <c>IF NOT EXISTS</c> for DDL, so an idempotent script asks the catalog first and
    /// runs the statement through <c>EXECUTE IMMEDIATE</c>, the same shape the schema guard uses.
    /// </summary>
    protected override void Generate(
        CreateTableOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        if (!_idempotent)
        {
            base.Generate(operation, model, builder, terminate);

            return;
        }

        GenerateIfMissing(
            builder,
            Exists("ALL_TABLES", "OWNER", "TABLE_NAME", operation.Schema, operation.Name),
            inner => base.Generate(operation, model, inner, terminate));
    }

    protected override void Generate(
        CreateIndexOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        if (operation.Filter is not null)
        {
            throw new NotSupportedException(
                $"The index '{operation.Name}' carries a filter, but Dameng has no filtered index: measured on "
                + "8.1.4.80, `CREATE INDEX ... WHERE` answered `语法分析出错`. Remove the filter from the migration.");
        }

        if (!_idempotent)
        {
            base.Generate(operation, model, builder, terminate);

            return;
        }

        GenerateIfMissing(
            builder,
            Exists("ALL_INDEXES", "OWNER", "INDEX_NAME", operation.Schema, operation.Name),
            inner => base.Generate(operation, model, inner, terminate));
    }

    /// <summary>
    /// A column a later migration adds needs its own guard: the table is there, so a table check says
    /// nothing about the column. The predicate names the table as well as the column, because a column
    /// name is unique within a table rather than within a schema.
    /// </summary>
    protected override void Generate(
        AddColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        if (!_idempotent)
        {
            base.Generate(operation, model, builder, terminate);

            return;
        }

        GenerateIfMissing(
            builder,
            Exists("ALL_TAB_COLUMNS", "OWNER", "COLUMN_NAME", operation.Schema, operation.Name)
                + " AND TABLE_NAME = "
                + Quote(operation.Table),
            inner => base.Generate(operation, model, inner, terminate));
    }

    protected override void Generate(
        AddPrimaryKeyOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        if (!_idempotent)
        {
            base.Generate(operation, model, builder, terminate);

            return;
        }

        GenerateIfMissing(
            builder,
            Exists("ALL_CONSTRAINTS", "OWNER", "CONSTRAINT_NAME", operation.Schema, operation.Name!),
            inner => base.Generate(operation, model, inner, terminate));
    }

    protected override void Generate(
        AddForeignKeyOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        if (!_idempotent)
        {
            base.Generate(operation, model, builder, terminate);

            return;
        }

        GenerateIfMissing(
            builder,
            Exists("ALL_CONSTRAINTS", "OWNER", "CONSTRAINT_NAME", operation.Schema, operation.Name!),
            inner => base.Generate(operation, model, inner, terminate));
    }

    protected override void Generate(
        AddUniqueConstraintOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(builder);

        if (!_idempotent)
        {
            base.Generate(operation, model, builder);

            return;
        }

        GenerateIfMissing(
            builder,
            Exists("ALL_CONSTRAINTS", "OWNER", "CONSTRAINT_NAME", operation.Schema, operation.Name!),
            inner => base.Generate(operation, model, inner));
    }

    /// <summary>
    /// Emits a block that runs the statements only when the catalog holds nothing under the name.
    /// </summary>
    private void GenerateIfMissing(
        MigrationCommandListBuilder builder,
        string query,
        Action<MigrationCommandListBuilder> generate)
    {
        var inner = new MigrationCommandListBuilder(Dependencies);
        generate(inner);

        builder
            .AppendLine("DECLARE")
            .AppendLine("    declared INT;")
            .AppendLine("BEGIN")
            .Append("    SELECT COUNT(*) INTO declared FROM ")
            .Append(query)
            .AppendLine(";")
            .AppendLine("    IF declared = 0 THEN");

        foreach (var command in inner.GetCommandList())
        {
            builder
                .Append("        EXECUTE IMMEDIATE ")
                .Append(Quote(WithoutTerminator(command.CommandText)))
                .AppendLine(";");
        }

        builder
            .AppendLine("    END IF;")
            .Append("END;");

        builder.EndCommand();
    }

    /// <summary>
    /// The row count test behind a guarded statement. A migration that names no schema creates the object
    /// in the session's current schema, so the guard compares the owner with that schema.
    /// </summary>
    /// <remarks>
    /// <c>USER</c> is the account, not the schema: measured on 8.1.4.80 with a connection string that sets
    /// <c>schema=</c>, <c>USER</c> answered `SYSDBA` while the object was created under `HIDOS` and
    /// <c>SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')</c> answered `HIDOS`. A guard comparing the owner with
    /// `USER` therefore answered `0` for an object that was there, and the block ran its statement again on
    /// the next run of the script.
    /// </remarks>
    private static string Exists(string view, string ownerColumn, string nameColumn, string? schema, string name)
        => view
            + " WHERE "
            + ownerColumn
            + " = "
            + (schema is null ? "SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')" : Quote(schema))
            + " AND "
            + nameColumn
            + " = "
            + Quote(name);

    /// <summary>
    /// A statement as it goes inside <c>EXECUTE IMMEDIATE</c>: no line breaks and no trailing statement
    /// terminator, which the block supplies.
    /// </summary>
    private string WithoutTerminator(string commandText)
    {
        var text = commandText.Trim();

        if (text.EndsWith(Helper.StatementTerminator, StringComparison.Ordinal))
        {
            text = text[..^Helper.StatementTerminator.Length].TrimEnd();
        }

        return text.ReplaceLineEndings(" ");
    }

    private bool _idempotent;

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
                GenerateComment(operation, builder);
                break;
        }
    }

    /// <summary>
    /// A comment change is the only change when every other attribute of the column is the same, in which
    /// case the statement that rewrites the column is not written at all.
    /// </summary>
    private static bool OnlyCommentChanged(AlterColumnOperation operation)
    {
        var old = operation.OldColumn;

        return old is not null
            && operation.Comment != old.Comment
            && operation.ColumnType == old.ColumnType
            && operation.IsNullable == old.IsNullable
            && operation.ClrType == old.ClrType
            && Equals(operation.DefaultValue, old.DefaultValue)
            && operation.DefaultValueSql == old.DefaultValueSql
            && operation.ComputedColumnSql == old.ComputedColumnSql
            && operation.IsStored == old.IsStored
            && operation.Collation == old.Collation
            && operation.IsRowVersion == old.IsRowVersion
            && operation.IsUnicode == old.IsUnicode
            && operation.MaxLength == old.MaxLength
            && operation.Precision == old.Precision
            && operation.Scale == old.Scale
            && operation.IsFixedLength == old.IsFixedLength;
    }

    /// <summary>
    /// Dameng keeps a table or column comment in a statement of its own, so a migration that carries no
    /// comment statement loses it. A comment is written next to the object it describes, after the
    /// statement that creates or alters it.
    /// </summary>
    private void GenerateComment(MigrationOperation operation, MigrationCommandListBuilder builder)
    {
        switch (operation)
        {
            case CreateTableOperation table:
                var owner = Helper.DelimitIdentifier(table.Name, table.Schema);

                Comment("TABLE", owner, table.Comment, builder);

                foreach (var column in table.Columns)
                {
                    Comment("COLUMN", ColumnOf(owner, column.Name), column.Comment, builder);
                }

                break;

            case AddColumnOperation column:
                Comment("COLUMN", ColumnOf(column.Table, column.Schema, column.Name), column.Comment, builder);
                break;

            case AlterColumnOperation column when column.Comment != column.OldColumn.Comment:
                // An empty string is how Dameng removes a comment.
                Comment("COLUMN", ColumnOf(column.Table, column.Schema, column.Name), column.Comment ?? string.Empty, builder);
                break;

            case AlterTableOperation table when table.Comment != table.OldTable.Comment:
                Comment("TABLE", Helper.DelimitIdentifier(table.Name, table.Schema), table.Comment ?? string.Empty, builder);
                break;
        }
    }

    private void Comment(string target, string name, string? text, MigrationCommandListBuilder builder)
    {
        if (text is null)
        {
            return;
        }

        builder
            .Append("COMMENT ON ")
            .Append(target)
            .Append(" ")
            .Append(name)
            .Append(" IS ")
            .Append(Quote(text));

        EndStatement(builder);
    }

    private string ColumnOf(string table, string? schema, string column)
        => ColumnOf(Helper.DelimitIdentifier(table, schema), column);

    private string ColumnOf(string owner, string column)
        => owner + "." + Helper.DelimitIdentifier(column);

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

    /// <summary>
    /// A column that stops generating its value keeps the attribute until it is dropped, and Dameng
    /// spells that <c>DROP IDENTITY</c>, which the reference provider also emits. The operation carries
    /// the state the column had, so the drop is written only when the column had one; the other
    /// direction is rejected rather than silently left undone, because neither provider knows a
    /// statement that turns an existing column into an identity column.
    /// </summary>
    private void DropIdentityIfLost(
        AlterColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        if (operation.OldColumn is not { } oldColumn)
        {
            return;
        }

        var before = FindIdentity(operation.Schema, operation.Table, operation.Name, oldColumn, model: null);
        var after = FindIdentity(operation.Schema, operation.Table, operation.Name, operation, model);

        if (before is not null && after is null)
        {
            builder
                .Append("ALTER TABLE ")
                .Append(Helper.DelimitIdentifier(operation.Table, operation.Schema))
                .Append(" DROP IDENTITY");

            EndStatement(builder);
        }
        else if (before is null && after is not null)
        {
            throw new NotSupportedException(
                $"The column '{operation.Name}' becomes an identity column, which this provider does not "
                + "alter in place: the statement for it is not verified against a server. Drop and add "
                + "the column again, or generate the value from a sequence.");
        }
    }

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
