using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace DM.EntityFrameworkCore.Tests.Migrations;

/// <summary>
/// Pins the idempotent flavour of a migration script. Dameng has no <c>IF NOT EXISTS</c> for DDL, so the
/// provider asks the catalog in a PL/SQL block and runs the statement through <c>EXECUTE IMMEDIATE</c>.
/// Without this, <c>dotnet ef migrations script --idempotent</c> produces a script that fails on a
/// database that already holds part of the migrations.
/// </summary>
public class DmIdempotentScriptTests
{
    [Fact]
    public void CreateTable_IsGuardedByTheCatalog()
    {
        var sql = Generate(
            MigrationsSqlGenerationOptions.Idempotent,
            new CreateTableOperation
            {
                Name = "Probes",
                Columns =
                {
                    new AddColumnOperation
                    {
                        Name = "Id",
                        Table = "Probes",
                        ClrType = typeof(int),
                        ColumnType = "INT",
                        IsNullable = false
                    }
                }
            });

        Assert.Contains(
            "SELECT COUNT(*) INTO declared FROM ALL_TABLES WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND TABLE_NAME = 'Probes';",
            sql,
            StringComparison.Ordinal);

        Assert.Contains(
            """EXECUTE IMMEDIATE 'CREATE TABLE "Probes" (""",
            sql,
            StringComparison.Ordinal);

        Assert.Contains("IF declared = 0 THEN", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// A column a later migration adds is guarded against the column catalog, not the table one: the
    /// table is already there, so only the column name tells a re-run whether the statement ran.
    /// </summary>
    [Fact]
    public void AddedColumn_IsGuardedByTheColumnCatalog()
    {
        var sql = Generate(
            MigrationsSqlGenerationOptions.Idempotent,
            new AddColumnOperation
            {
                Table = "Probes",
                Name = "Note",
                ClrType = typeof(string),
                ColumnType = "VARCHAR(50)",
                IsNullable = true
            });

        Assert.Contains(
            "SELECT COUNT(*) INTO declared FROM ALL_TAB_COLUMNS WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AND COLUMN_NAME = 'Note' AND TABLE_NAME = 'Probes';",
            sql,
            StringComparison.Ordinal);

        Assert.Contains(
            """EXECUTE IMMEDIATE 'ALTER TABLE "Probes" ADD "Note" VARCHAR(50) NULL'""",
            sql,
            StringComparison.Ordinal);
    }
    [Fact]
    public void SequenceAndIndex_AreGuardedByTheirOwnViewAndSchema()
    {
        var sql = Generate(
            MigrationsSqlGenerationOptions.Idempotent,
            new CreateSequenceOperation
            {
                Name = "ProbeSeq",
                Schema = "APP",
                ClrType = typeof(int),
                StartValue = 1L
            },
            new CreateIndexOperation
            {
                Name = "IX_Probes_Name",
                Schema = "APP",
                Table = "Probes",
                Columns = ["Name"]
            });

        Assert.Contains(
            "SELECT COUNT(*) INTO declared FROM ALL_SEQUENCES WHERE SEQUENCE_OWNER = 'APP' AND SEQUENCE_NAME = 'ProbeSeq';",
            sql,
            StringComparison.Ordinal);

        Assert.Contains("EXECUTE IMMEDIATE 'CREATE SEQUENCE \"APP\"", sql, StringComparison.Ordinal);

        Assert.Contains(
            "SELECT COUNT(*) INTO declared FROM ALL_INDEXES WHERE OWNER = 'APP' AND INDEX_NAME = 'IX_Probes_Name';",
            sql,
            StringComparison.Ordinal);

        Assert.Contains("EXECUTE IMMEDIATE 'CREATE INDEX", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ForeignKey_IsGuardedAsAConstraint()
    {
        var sql = Generate(
            MigrationsSqlGenerationOptions.Idempotent,
            new AddForeignKeyOperation
            {
                Name = "FK_Lines_Probes_ProbeId",
                Schema = "APP",
                Table = "Lines",
                Columns = ["ProbeId"],
                PrincipalTable = "Probes",
                PrincipalSchema = "APP",
                PrincipalColumns = ["Id"]
            });

        Assert.Contains(
            "SELECT COUNT(*) INTO declared FROM ALL_CONSTRAINTS WHERE OWNER = 'APP' AND CONSTRAINT_NAME = 'FK_Lines_Probes_ProbeId';",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheDynamicStatement_CarriesNoTerminator()
    {
        // The block supplies the terminator; a semicolon inside the quoted statement would make the
        // dynamic execution fail.
        var sql = Generate(
            MigrationsSqlGenerationOptions.Idempotent,
            new CreateSequenceOperation { Name = "ProbeSeq", ClrType = typeof(int), StartValue = 1L });

        Assert.DoesNotContain(";;", sql, StringComparison.Ordinal);
        Assert.Contains("""EXECUTE IMMEDIATE 'CREATE SEQUENCE "ProbeSeq" START WITH 1 INCREMENT BY 1 NOMINVALUE NOMAXVALUE NOCYCLE';""", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefaultScript_LeavesTheStatementUnguarded()
    {
        var sql = Generate(
            MigrationsSqlGenerationOptions.Default,
            new CreateSequenceOperation { Name = "ProbeSeq", ClrType = typeof(int), StartValue = 1L });

        Assert.DoesNotContain("DECLARE", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("EXECUTE IMMEDIATE", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// DMSQL accepts DDL and session statements in a block only through dynamic SQL, so every command
    /// the script wraps in the history guard has to be <c>EXECUTE IMMEDIATE</c> or a block of its own.
    /// The operations below are the ones that do not have a catalog guard: comment, alter, rename,
    /// drop and raw SQL.
    /// </summary>
    [Fact]
    public void StatementsWithoutACatalogGuard_AreExecutedDynamically()
    {
        var sql = Generate(
            MigrationsSqlGenerationOptions.Idempotent,
            new AlterColumnOperation
            {
                Table = "Probes",
                Name = "Name",
                ClrType = typeof(string),
                ColumnType = "VARCHAR(120)",
                IsNullable = true
            },
            new RenameColumnOperation { Table = "Probes", Name = "Name", NewName = "Title" },
            new DropColumnOperation { Table = "Probes", Name = "Note" },
            new DropTableOperation { Name = "OldProbes" },
            new AddColumnOperation
            {
                Table = "Probes",
                Name = "Note",
                ClrType = typeof(string),
                ColumnType = "VARCHAR(20)",
                IsNullable = true,
                Comment = "a note"
            },
            new SqlOperation { Sql = "CREATE TABLE \"Raw\" (\"Id\" INT)" });

        Assert.Contains("""EXECUTE IMMEDIATE 'ALTER TABLE "Probes" MODIFY ("Name" VARCHAR(120))';""", sql, StringComparison.Ordinal);
        Assert.Contains("""EXECUTE IMMEDIATE 'ALTER TABLE "Probes" RENAME COLUMN "Name" TO "Title"';""", sql, StringComparison.Ordinal);
        Assert.Contains("""EXECUTE IMMEDIATE 'ALTER TABLE "Probes" DROP COLUMN "Note"';""", sql, StringComparison.Ordinal);
        Assert.Contains("""EXECUTE IMMEDIATE 'DROP TABLE "OldProbes"';""", sql, StringComparison.Ordinal);
        Assert.Contains("""EXECUTE IMMEDIATE 'COMMENT ON COLUMN "Probes"."Note" IS ''a note''';""", sql, StringComparison.Ordinal);
        Assert.Contains("""EXECUTE IMMEDIATE 'CREATE TABLE "Raw" ("Id" INT)';""", sql, StringComparison.Ordinal);

        // No statement of those forms sits in a guard body on its own.
        foreach (var line in sql.Split('\n').Select(line => line.Trim()))
        {
            Assert.False(
                line.StartsWith("COMMENT ON ", StringComparison.Ordinal)
                || line.StartsWith("ALTER TABLE ", StringComparison.Ordinal)
                || line.StartsWith("DROP TABLE ", StringComparison.Ordinal),
                $"The line is not dynamic SQL: {line}");
        }
    }

    /// <summary>
    /// A schema is created by a block of its own; inside the history guard it must not carry the
    /// batch terminator the top-level script uses, because that would close the outer block.
    /// </summary>
    [Fact]
    public void EnsureSchema_InTheIdempotentScript_DoesNotTerminateTheGuard()
    {
        var sql = Generate(
            MigrationsSqlGenerationOptions.Idempotent,
            new EnsureSchemaOperation { Name = "AUDIT" });

        Assert.Contains("""EXECUTE IMMEDIATE 'CREATE SCHEMA "AUDIT"'""", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\n/\n", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// A seeded identity column needs the session flag around the insert, and a block runs that flag
    /// only as dynamic SQL; the insert itself stays DML.
    /// </summary>
    [Fact]
    public void SeedData_KeepsTheInsertAndRunsTheIdentityFlagDynamically()
    {
        using var context = new SeedContext();

        var commands = context
            .GetService<IMigrationsSqlGenerator>()
            .Generate(
                [
                    new InsertDataOperation
                    {
                        Table = "Lines",
                        Columns = ["Id", "Note"],
                        Values = new object[,] { { 7, "a" } }
                    }
                ],
                context.Model,
                MigrationsSqlGenerationOptions.Idempotent);

        var sql = commands[0].CommandText.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.StartsWith("BEGIN", sql.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("""EXECUTE IMMEDIATE 'SET IDENTITY_INSERT "Lines" ON';""", sql, StringComparison.Ordinal);
        Assert.Contains("""INSERT INTO "Lines" ("Id", "Note")""", sql, StringComparison.Ordinal);
        Assert.Contains("""EXECUTE IMMEDIATE 'SET IDENTITY_INSERT "Lines" OFF';""", sql, StringComparison.Ordinal);
    }

    private static string Generate(MigrationsSqlGenerationOptions options, params MigrationOperation[] operations)
    {
        using var context = new ProbeContext();

        return string.Join(
            "\n",
            context
                .GetService<IMigrationsSqlGenerator>()
                .Generate(operations, context.Model, options)
                .Select(command => command.CommandText.Replace("\r\n", "\n", StringComparison.Ordinal)));
    }

    private sealed class ProbeContext : DbContext
    {
        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Name).HasMaxLength(50);
            });
    }

    private sealed class SeedContext : DbContext
    {
        public DbSet<Line> Lines => Set<Line>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Line>().ToTable("Lines");
    }

    private sealed class Line
    {
        public int Id { get; set; }

        public string Note { get; set; } = string.Empty;
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
