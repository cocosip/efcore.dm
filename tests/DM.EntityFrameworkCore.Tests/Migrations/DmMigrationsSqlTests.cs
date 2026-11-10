using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
                "Id" INT NOT NULL,
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
    public void CreateSequence_OmitsTheTypeAndSpellsCycle()
    {
        AssertSql(
            new CreateSequenceOperation { Name = "ProbeSeq", ClrType = typeof(int), StartValue = 5L, IncrementBy = 2 },
            """CREATE SEQUENCE "ProbeSeq" START WITH 5 INCREMENT BY 2 NOCYCLE;""");
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
