using DM.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace DM.EntityFrameworkCore.Tests.Migrations;

/// <summary>
/// T2 tests for migration DDL. The operations are generated directly, so the assertion covers
/// the dialect shapes Dameng spells differently without needing a migration project or a server.
/// </summary>
public class DmMigrationsSqlTests
{
    [Fact]
    public void CreateScript_GeneratesTablesIndexesAndBatchSeparators()
    {
        using var context = new ProbeContext();

        Assert.Equal(
            """
            CREATE TABLE "Probes" (
                "Id" INT NOT NULL,
                "Name" VARCHAR(50) NOT NULL,
                "Amount" DECIMAL(18,2) NOT NULL,
                CONSTRAINT "PK_Probes" PRIMARY KEY ("Id")
            );
            /
            CREATE TABLE "Lines" (
                "Id" INT IDENTITY(1,1) NOT NULL,
                "ProbeId" INT NOT NULL,
                CONSTRAINT "PK_Lines" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Lines_Probes_ProbeId" FOREIGN KEY ("ProbeId") REFERENCES "Probes" ("Id") ON DELETE CASCADE
            );
            /
            CREATE INDEX "IX_Lines_ProbeId" ON "Lines" ("ProbeId");
            /
            CREATE UNIQUE INDEX "IX_Probes_Name" ON "Probes" ("Name");
            /
            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            Separators(context.Database.GenerateCreateScript()));
    }

    [Fact]
    public void AlterColumn_UsesModify()
    {
        AssertSql(
            new AlterColumnOperation
            {
                Table = "Probes",
                Name = "Name",
                ClrType = typeof(string),
                ColumnType = "VARCHAR(120)",
                IsNullable = true
            },
            """ALTER TABLE "Probes" MODIFY ("Name" VARCHAR(120));""");
    }

    [Fact]
    public void AlterColumn_NotNullWithDefault_AppendsBoth()
    {
        AssertSql(
            new AlterColumnOperation
            {
                Table = "Probes",
                Name = "Name",
                ClrType = typeof(string),
                ColumnType = "VARCHAR(120)",
                IsNullable = false,
                DefaultValue = "x"
            },
            """ALTER TABLE "Probes" MODIFY ("Name" VARCHAR(120) NOT NULL DEFAULT 'x');""");
    }

    [Fact]
    public void RenameColumn_UsesRenameColumn()
    {
        AssertSql(
            new RenameColumnOperation { Table = "Probes", Name = "Name", NewName = "Title" },
            """ALTER TABLE "Probes" RENAME COLUMN "Name" TO "Title";""");
    }

    [Fact]
    public void RenameTable_UsesRenameTo()
    {
        AssertSql(
            new RenameTableOperation { Name = "Probes", NewName = "Surveys" },
            """ALTER TABLE "Probes" RENAME TO "Surveys";""");
    }

    [Fact]
    public void RenameIndex_UsesAlterIndex()
    {
        AssertSql(
            new RenameIndexOperation { Name = "IX_Probes_Name", NewName = "IX_Probes_Title" },
            """ALTER INDEX "IX_Probes_Name" RENAME TO "IX_Probes_Title";""");
    }

    [Fact]
    public void DropIndex_DoesNotNameTheTable()
    {
        AssertSql(
            new DropIndexOperation { Name = "IX_Probes_Name" },
            """DROP INDEX "IX_Probes_Name";""");
    }

    [Fact]
    public void CreateSequence_SpellsTheOptionsAsOneWord()
    {
        AssertSql(
            new CreateSequenceOperation { Name = "ProbeSeq", ClrType = typeof(int), StartValue = 5L, IncrementBy = 2 },
            """CREATE SEQUENCE "ProbeSeq" START WITH 5 INCREMENT BY 2 NOMINVALUE NOMAXVALUE NOCYCLE;""");
    }

    [Fact]
    public void CreateSequence_WithBounds_EmitsThem()
    {
        AssertSql(
            new CreateSequenceOperation
            {
                Name = "ProbeSeq",
                ClrType = typeof(int),
                StartValue = 1L,
                IncrementBy = 1,
                MinValue = 1L,
                MaxValue = 100L,
                IsCyclic = true
            },
            """CREATE SEQUENCE "ProbeSeq" START WITH 1 INCREMENT BY 1 MINVALUE 1 MAXVALUE 100 CYCLE;""");
    }

    /// <summary>
    /// An alteration repeats every option, so a sequence that stops cycling also loses a bound that
    /// was set before rather than keeping it.
    /// </summary>
    [Fact]
    public void AlterSequence_RepeatsEveryOption()
    {
        AssertSql(
            new AlterSequenceOperation { Name = "ProbeSeq", IncrementBy = 5 },
            """ALTER SEQUENCE "ProbeSeq" INCREMENT BY 5 NOMINVALUE NOMAXVALUE NOCYCLE;""");
    }

    /// <summary>
    /// A schema a database administrator already created must not fail the migration, so the
    /// statement asks the catalog before creating anything.
    /// </summary>
    [Fact]
    public void EnsureSchema_AsksTheCatalogBeforeCreating()
    {
        AssertSql(
            new EnsureSchemaOperation { Name = "APP" },
            """
            DECLARE
                declared INT;
            BEGIN
                SELECT COUNT(*) INTO declared FROM SYS.SYSOBJECTS WHERE TYPE$ = 'SCH' AND NAME = 'APP';
                IF declared = 0 THEN
                    EXECUTE IMMEDIATE 'CREATE SCHEMA "APP"';
                END IF;
            END;
            """);
    }
    /// <summary>
    /// A schema belongs to a user, so creating the user creates both. The privileges the account
    /// needs are granted by the application, not by the migration.
    /// </summary>
    [Fact]
    public void CreateUser_CreatesTheAccountAndTheSchemaItOwns()
    {
        AssertSql(
            new DmCreateUserOperation { UserName = "APP", Password = "secret", Schema = "APP" },
            """
            CREATE USER "APP" IDENTIFIED BY 'secret';
            CREATE SCHEMA "APP" AUTHORIZATION "APP";
            """);
    }

    [Fact]
    public void CreateUser_WithoutASchema_CreatesTheAccountOnly()
    {
        AssertSql(
            new DmCreateUserOperation { UserName = "APP", Password = "secret" },
            """CREATE USER "APP" IDENTIFIED BY 'secret';""");
    }

    /// <summary>
    /// A password is a string literal, so a quote in it has to be doubled rather than ending the
    /// literal early.
    /// </summary>
    [Fact]
    public void CreateUser_EscapesAQuoteInThePassword()
    {
        AssertSql(
            new DmCreateUserOperation { UserName = "APP", Password = "se'cret" },
            """CREATE USER "APP" IDENTIFIED BY 'se''cret';""");
    }

    [Fact]
    public void DropUser_Cascades()
    {
        AssertSql(
            new DmDropUserOperation { UserName = "APP" },
            """DROP USER "APP" CASCADE;""");
    }

    [Fact]
    public void CreateSchema_NamesTheOwner()
    {
        AssertSql(
            new DmCreateSchemaOperation { Name = "REPORTING", UserName = "APP" },
            """CREATE SCHEMA "REPORTING" AUTHORIZATION "APP";""");
    }

    [Fact]
    public void DropSchema_Cascades()
    {
        AssertSql(
            new DmDropSchemaOperation { Name = "REPORTING" },
            """DROP SCHEMA "REPORTING" CASCADE;""");
    }

    [Fact]
    public void IsDm_ComparesAgainstTheProviderAssembly()
    {
        var providerName = typeof(DmMigrationBuilderExtensions).Assembly.GetName().Name;

        Assert.True(new MigrationBuilder(providerName).IsDm());
        Assert.False(new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer").IsDm());
    }
    [Fact]
    public void CreateTable_WithComments_WritesACommentStatementForEach()
    {
        AssertSql(
            new CreateTableOperation
            {
                Name = "Probes",
                Comment = "the probes",
                Columns =
                {
                    new AddColumnOperation { Name = "Id", ClrType = typeof(int), ColumnType = "INT", IsNullable = false },
                    new AddColumnOperation
                    {
                        Name = "Name",
                        ClrType = typeof(string),
                        ColumnType = "VARCHAR(50)",
                        Comment = "the owner's name",
                        IsNullable = false
                    }
                }
            },
            """
            CREATE TABLE "Probes" (
                "Id" INT NOT NULL,
                "Name" VARCHAR(50) NOT NULL
            );
            COMMENT ON TABLE "Probes" IS 'the probes';
            COMMENT ON COLUMN "Probes"."Name" IS 'the owner''s name';
            """);
    }

    [Fact]
    public void AddColumn_WithComment_WritesTheCommentAfterTheColumn()
    {
        AssertSql(
            new AddColumnOperation
            {
                Table = "Probes",
                Name = "Note",
                ClrType = typeof(string),
                ColumnType = "VARCHAR(50)",
                Comment = "a note",
                IsNullable = true
            },
            """
            ALTER TABLE "Probes" ADD "Note" VARCHAR(50) NULL;
            COMMENT ON COLUMN "Probes"."Note" IS 'a note';
            """);
    }

    [Fact]
    public void AlterColumn_WithTheCommentRemoved_WritesAnEmptyComment()
    {
        AssertSql(
            new AlterColumnOperation
            {
                Table = "Probes",
                Name = "Note",
                ClrType = typeof(string),
                ColumnType = "VARCHAR(50)",
                IsNullable = true,
                OldColumn = new AddColumnOperation { Comment = "a note" }
            },
            """
            ALTER TABLE "Probes" MODIFY ("Note" VARCHAR(50));
            COMMENT ON COLUMN "Probes"."Note" IS '';
            """);
    }

    /// <summary>
    /// Drops the blank line the command list emits before each batch separator, so that the
    /// assertion covers the statements and the separator rather than EF Core's spacing.
    /// </summary>
    private static string Separators(string script)
        => script
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n\n/", "\n/", StringComparison.Ordinal)
            .TrimEnd('\n');

    /// <summary>
    /// The operations the generated migrations use, by name. Each shape is what Dameng spells, read
    /// from the statement the generator produces rather than borrowed from another provider.
    /// </summary>
    private static readonly Dictionary<string, MigrationOperation> Operations = new()
    {
        ["DropTable"] = new DropTableOperation { Name = "Probes", Schema = "APP" },
        ["DropColumn"] = new DropColumnOperation { Table = "Probes", Name = "Name", Schema = "APP" },
        ["AddPrimaryKey"] = new AddPrimaryKeyOperation { Table = "Probes", Name = "PK_Probes", Columns = ["Id"] },
        ["DropPrimaryKey"] = new DropPrimaryKeyOperation { Table = "Probes", Name = "PK_Probes" },
        ["AddUnique"] = new AddUniqueConstraintOperation { Table = "Probes", Name = "UQ_Probes_Name", Columns = ["Name"] },
        ["DropUnique"] = new DropUniqueConstraintOperation { Table = "Probes", Name = "UQ_Probes_Name" },
        ["AddForeignKey"] = new AddForeignKeyOperation
        {
            Table = "Lines",
            Name = "FK_Lines_Probes",
            Columns = ["ProbeId"],
            PrincipalTable = "Probes",
            PrincipalColumns = ["Id"],
            OnDelete = ReferentialAction.Cascade
        },
        ["DropForeignKey"] = new DropForeignKeyOperation { Table = "Lines", Name = "FK_Lines_Probes" },
        ["RestartSequence"] = new RestartSequenceOperation { Name = "ProbeSeq", StartValue = 100L },
        ["DropSequence"] = new DropSequenceOperation { Name = "ProbeSeq" },
        ["DropSchema"] = new DropSchemaOperation { Name = "REPORTING" },
        ["Sql"] = new SqlOperation { Sql = "SELECT 1 FROM DUAL" },
        ["InsertData"] = new InsertDataOperation
        {
            Table = "Probes",
            Columns = ["Id", "Name"],
            Values = new object[,] { { 1, "a" }, { 2, "b" } }
        },
        ["UpdateData"] = new UpdateDataOperation
        {
            Table = "Probes",
            KeyColumns = ["Id"],
            KeyValues = new object[,] { { 1 } },
            Columns = ["Name"],
            Values = new object[,] { { "c" } }
        },
        ["DeleteData"] = new DeleteDataOperation
        {
            Table = "Probes",
            KeyColumns = ["Id"],
            KeyValues = new object[,] { { 1 } }
        }
    };

    [Theory]
    [InlineData("DropTable", """DROP TABLE "APP"."Probes";""")]
    [InlineData("DropColumn", """ALTER TABLE "APP"."Probes" DROP COLUMN "Name";""")]
    [InlineData("AddPrimaryKey", """ALTER TABLE "Probes" ADD CONSTRAINT "PK_Probes" PRIMARY KEY ("Id");""")]
    [InlineData("DropPrimaryKey", """ALTER TABLE "Probes" DROP CONSTRAINT "PK_Probes";""")]
    [InlineData("AddUnique", """ALTER TABLE "Probes" ADD CONSTRAINT "UQ_Probes_Name" UNIQUE ("Name");""")]
    [InlineData("DropUnique", """ALTER TABLE "Probes" DROP CONSTRAINT "UQ_Probes_Name";""")]
    [InlineData("AddForeignKey", """
        ALTER TABLE "Lines" ADD CONSTRAINT "FK_Lines_Probes" FOREIGN KEY ("ProbeId") REFERENCES "Probes" ("Id") ON DELETE CASCADE;
        """)]
    [InlineData("DropForeignKey", """ALTER TABLE "Lines" DROP CONSTRAINT "FK_Lines_Probes";""")]
    // The sequence restart is the shape the row limiting clause uses; whether the server accepts it on
    // every supported version is unverified.
    [InlineData("RestartSequence", """ALTER SEQUENCE "ProbeSeq" RESTART WITH 100;""")]
    [InlineData("DropSequence", """DROP SEQUENCE "ProbeSeq";""")]
    [InlineData("DropSchema", """DROP SCHEMA "REPORTING";""")]
    [InlineData("Sql", """SELECT 1 FROM DUAL""")]
    [InlineData("InsertData", """
        INSERT INTO "Probes" ("Id", "Name")
        VALUES (1, 'a');
        INSERT INTO "Probes" ("Id", "Name")
        VALUES (2, 'b');
        """)]
    [InlineData("UpdateData", """
        UPDATE "Probes" SET "Name" = 'c'
        WHERE "Id" = 1;
        """)]
    [InlineData("DeleteData", """
        DELETE FROM "Probes"
        WHERE "Id" = 1;
        """)]
    public void Operation_GeneratesTheStatement(string name, string expected)
        => AssertSql(Operations[name], expected);

    /// <summary>
    /// A column that stops generating its value has to lose the attribute before the type is modified,
    /// which the provider writes as a statement of its own in front of the modification. The table is
    /// the one this model does not map, because the model in a migration states the state after it and
    /// this one maps its own key as an identity column.
    /// </summary>
    [Fact]
    public void AlterColumn_FromIdentity_DropsTheAttributeFirst()
        => AssertSql(
            new AlterColumnOperation
            {
                Table = "History",
                Name = "Id",
                ClrType = typeof(int),
                ColumnType = "INT",
                OldColumn = IdentityColumn()
            },
            """
            ALTER TABLE "History" DROP IDENTITY;
            ALTER TABLE "History" MODIFY ("Id" INT NOT NULL);
            """);

    /// <summary>
    /// Changing anything else about an identity column keeps the attribute, so no drop is written.
    /// </summary>
    [Fact]
    public void AlterColumn_IdentityStays_KeepsTheAttribute()
        => AssertSql(
            new AlterColumnOperation
            {
                Table = "Lines",
                Name = "Id",
                ClrType = typeof(long),
                ColumnType = "BIGINT",
                [DmPropertyExtensions.ValueGenerationStrategyAnnotation] = DmValueGenerationStrategy.IdentityColumn,
                OldColumn = IdentityColumn()
            },
            """ALTER TABLE "Lines" MODIFY ("Id" BIGINT NOT NULL);""");

    /// <summary>
    /// The other direction is rejected rather than left undone: no statement that turns an existing
    /// column into an identity column has been verified, and a migration that silently does nothing
    /// leaves a column the model treats as generated without the attribute that generates it.
    /// </summary>
    [Fact]
    public void AlterColumn_ToIdentity_IsRejected()
    {
        using var context = new ProbeContext();

        var operation = new AlterColumnOperation
        {
            Table = "Lines",
            Name = "Id",
            ClrType = typeof(int),
            ColumnType = "INT",
            [DmPropertyExtensions.ValueGenerationStrategyAnnotation] = DmValueGenerationStrategy.IdentityColumn,
            OldColumn = new AddColumnOperation { ClrType = typeof(int), ColumnType = "INT", IsNullable = false }
        };

        Assert.Throws<NotSupportedException>(
            () => context.GetService<IMigrationsSqlGenerator>().Generate([operation], context.Model));
    }

    /// <summary>
    /// A snapshot the previous provider wrote marks an identity column with its own annotation rather
    /// than with the strategy. Reading that name keeps such a migration from being read as a column
    /// without identity, which would both skip the drop and reject the change as an unsupported gain.
    /// </summary>
    [Fact]
    public void AlterColumn_FromIdentityWrittenByThePreviousProvider_DropsTheAttribute()
    {
        var oldColumn = new AddColumnOperation { ClrType = typeof(int), ColumnType = "INT", IsNullable = false };
        oldColumn[DmAnnotationNames.Identity] = true;

        AssertSql(
            new AlterColumnOperation
            {
                Table = "History",
                Name = "Id",
                ClrType = typeof(int),
                ColumnType = "INT",
                OldColumn = oldColumn
            },
            """
            ALTER TABLE "History" DROP IDENTITY;
            ALTER TABLE "History" MODIFY ("Id" INT NOT NULL);
            """);
    }
    /// <summary>
    /// The end to end shape, through the differ rather than a hand written operation: a model that
    /// stops generating its key produces the drop, which proves the annotation the differ puts on the
    /// old column is the one the generator reads.
    /// </summary>
    [Fact]
    public void MigrationsDiff_WhenAColumnStopsGenerating_DropsTheIdentityAttribute()
    {
        using var before = new IdentityContext();
        using var after = new PlainContext();

        var operations = before.GetService<IMigrationsModelDiffer>().GetDifferences(
            before.GetService<IDesignTimeModel>().Model.GetRelationalModel(),
            after.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Equal(
            """
            ALTER TABLE "Items" DROP IDENTITY;
            ALTER TABLE "Items" MODIFY ("Id" INT NOT NULL);
            """,
            string.Join(
                "\n",
                before.GetService<IMigrationsSqlGenerator>()
                    .Generate(operations, after.GetService<IDesignTimeModel>().Model)
                    .Select(command => command.CommandText.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n'))));
    }

    /// <summary>
    /// The same reverse direction, reached through the differ instead of a hand written operation.
    /// </summary>
    [Fact]
    public void MigrationsDiff_WhenAColumnStartsGenerating_IsRejected()
    {
        using var before = new PlainContext();
        using var after = new IdentityContext();

        var operations = before.GetService<IMigrationsModelDiffer>().GetDifferences(
            before.GetService<IDesignTimeModel>().Model.GetRelationalModel(),
            after.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Throws<NotSupportedException>(
            () => before.GetService<IMigrationsSqlGenerator>().Generate(operations, after.GetService<IDesignTimeModel>().Model));
    }

    private abstract class DiffContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");
    }

    private sealed class IdentityContext : DiffContext
    {
        public DbSet<Item> Items => Set<Item>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Item>(entity =>
            {
                entity.ToTable("Items");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
            });
    }

    private sealed class PlainContext : DiffContext
    {
        public DbSet<Item> Items => Set<Item>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Item>(entity =>
            {
                entity.ToTable("Items");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedNever();
            });
    }

    private sealed class Item
    {
        public int Id { get; set; }
    }
    private static AddColumnOperation IdentityColumn()
    {
        var column = new AddColumnOperation
        {
            ClrType = typeof(int),
            ColumnType = "INT",
            IsNullable = false
        };

        column[DmPropertyExtensions.ValueGenerationStrategyAnnotation] = DmValueGenerationStrategy.IdentityColumn;

        return column;
    }
    private static void AssertSql(MigrationOperation operation, string expected)
    {
        using var context = new ProbeContext();

        var commands = context.GetService<IMigrationsSqlGenerator>().Generate([operation], context.Model);

        Assert.Equal(
            expected,
            string.Join("\n", commands.Select(c => c.CommandText.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n'))));
    }

    private sealed class ProbeContext : DbContext
    {
        public DbSet<Probe> Probes => Set<Probe>();

        public DbSet<Line> Lines => Set<Line>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasMany(e => e.Lines).WithOne().HasForeignKey(l => l.ProbeId);
            });
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public List<Line> Lines { get; set; } = [];
    }

    private sealed class Line
    {
        public int Id { get; set; }

        public int ProbeId { get; set; }
    }
}
