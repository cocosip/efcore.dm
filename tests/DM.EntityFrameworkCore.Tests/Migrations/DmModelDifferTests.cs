using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace DM.EntityFrameworkCore.Tests.Migrations;

/// <summary>
/// Pins the migration operations the model differ produces for a model change and the SQL the
/// generator turns them into. The differ is the EF Core one; what these tests establish is that the
/// provider metadata survives the comparison and that the operations land as the right statement.
/// </summary>
public class DmModelDifferTests
{
    [Fact]
    public void GetDifferences_ReportsTheSchemaAndSequenceBehindANewHiLoProperty()
    {
        using var target = new HiLoContext();
        var operations = Diff(target, newIdentityModel: false);

        var schema = Assert.Single(operations.OfType<EnsureSchemaOperation>());
        Assert.Equal("app", schema.Name);

        var sequence = Assert.Single(operations.OfType<CreateSequenceOperation>());
        Assert.Equal("OrderSequence", sequence.Name);
        Assert.Equal("app", sequence.Schema);
        Assert.Equal(10, sequence.IncrementBy);
    }

    [Fact]
    public void Generate_AddsANewColumnInsteadOfModifyingIt()
    {
        using var target = new ColumnContext();
        var operations = Diff(target, newIdentityModel: false);

        var added = Assert.Single(operations.OfType<AddColumnOperation>());
        Assert.Equal("Code", added.Name);

        Assert.Contains(
            """ALTER TABLE "ORDERS" ADD "Code" NVARCHAR(10) NULL;""",
            Sql(target, operations));
    }

    [Fact]
    public void Generate_ModifiesAChangedColumn()
    {
        using var target = new ColumnContext();
        var operations = Diff(target, newIdentityModel: false);

        var altered = Assert.Single(operations.OfType<AlterColumnOperation>());
        Assert.Equal("Name", altered.Name);
        Assert.Equal(100, altered.MaxLength);

        Assert.Contains(
            """ALTER TABLE "ORDERS" MODIFY ("Name" NVARCHAR(100) NOT NULL);""",
            Sql(target, operations));
    }

    private static IReadOnlyList<MigrationOperation> Diff(DbContext target, bool newIdentityModel)
    {
        using var source = new BaselineContext();

        return target
            .GetService<IMigrationsModelDiffer>()
            .GetDifferences(
                source.GetService<IDesignTimeModel>().Model.GetRelationalModel(),
                target.GetService<IDesignTimeModel>().Model.GetRelationalModel());
    }

    private static string Sql(DbContext context, IReadOnlyList<MigrationOperation> operations)
        => string.Join(
            Environment.NewLine,
            context
                .GetService<IMigrationsSqlGenerator>()
                .Generate(operations, context.GetService<IDesignTimeModel>().Model)
                .Select(command => command.CommandText));

    /// <summary>
    /// The model before the change. <see cref="Order.Code" /> is ignored so a context that maps it
    /// produces an added column rather than a widened one.
    /// </summary>
    private sealed class BaselineContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(new FakeDmConnection());

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>(
                entity =>
                {
                    entity.ToTable("ORDERS");
                    entity.Property(order => order.Id).ValueGeneratedOnAdd();
                    entity.Property(order => order.Name).HasMaxLength(50);
                    entity.Ignore(order => order.Code);
                });
    }

    private sealed class HiLoContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(new FakeDmConnection());

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>(
                entity =>
                {
                    entity.ToTable("ORDERS");
                    entity.Property(order => order.Id).ValueGeneratedOnAdd().UseHiLo("OrderSequence", "app");
                    entity.Property(order => order.Name).HasMaxLength(50);
                    entity.Ignore(order => order.Code);
                });
    }

    private sealed class ColumnContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(new FakeDmConnection());

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>(
                entity =>
                {
                    entity.ToTable("ORDERS");
                    entity.Property(order => order.Id).ValueGeneratedOnAdd();
                    entity.Property(order => order.Name).HasMaxLength(100);
                    entity.Property(order => order.Code).HasMaxLength(10);
                });
    }

    private sealed class Order
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Code { get; set; }
    }
}
