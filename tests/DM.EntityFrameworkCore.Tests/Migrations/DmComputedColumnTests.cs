using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace DM.EntityFrameworkCore.Tests.Migrations;

/// <summary>
/// T2 tests for a computed column, which the base generator leaves unwritten: without an override a model
/// that declares one could not produce a script at all.
/// </summary>
public class DmComputedColumnTests
{
    [Fact]
    public void CreateScript_WritesAVirtualColumn()
    {
        using var context = new ComputedContext();

        Assert.Contains(
            "\"Total\" GENERATED ALWAYS AS (\"Amount\" * 2)",
            context.Database.GenerateCreateScript(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A column added by a migration is written the same way, because it is the clause rather than the
    /// statement that the server spells differently.
    /// </summary>
    [Fact]
    public void AddColumn_WritesAVirtualColumn()
    {
        using var context = new ComputedContext();

        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(
            [
                new AddColumnOperation
                {
                    Table = "Probes",
                    Name = "Total",
                    ClrType = typeof(int),
                    ColumnType = "INT",
                    ComputedColumnSql = "\"Amount\" * 2"
                }
            ],
            context.Model);

        Assert.Equal(
            """
            ALTER TABLE "Probes" ADD "Total" GENERATED ALWAYS AS ("Amount" * 2);
            """,
            commands.Single().CommandText.ReplaceLineEndings("\n").TrimEnd());
    }

    /// <summary>
    /// Measured on 8.1.4.80: `STORED` after the clause is a syntax error (`语法分析出错`), so a stored column is
    /// refused with a message that names the limitation rather than written as a statement the server rejects.
    /// </summary>
    [Fact]
    public void StoredComputedColumn_IsRefusedWhileTheScriptIsBuilt()
    {
        using var context = new ComputedContext();

        var exception = Assert.Throws<NotSupportedException>(
            () => context.GetService<IMigrationsSqlGenerator>().Generate(
                [
                    new CreateTableOperation
                    {
                        Name = "Probes",
                        Columns =
                        {
                            new AddColumnOperation
                            {
                                Table = "Probes",
                                Name = "Total",
                                ClrType = typeof(int),
                                ColumnType = "INT",
                                ComputedColumnSql = "\"Amount\" * 2",
                                IsStored = true
                            }
                        }
                    }
                ],
                context.Model));

        Assert.Contains("stored", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ComputedContext : DbContext
    {
        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Total).HasComputedColumnSql("\"Amount\" * 2");
            });
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public int Amount { get; set; }

        public int Total { get; set; }
    }
}

