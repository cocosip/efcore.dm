using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DM.EntityFrameworkCore.Tests.Migrations;

/// <summary>
/// The schema a deployment gets has two sources: the create script the relational database creator
/// builds from the model, which the acceptance console and <c>EnsureCreated</c> use, and the migration
/// statements a differ produces, which <c>dotnet ef</c> uses. They read the same model, so they have to
/// say the same thing; a shape one path spells and the other does not is a difference a developer would
/// only meet on the database.
/// </summary>
public class DmSchemaConsistencyTests
{
    [Fact]
    public void TheSchemaAMigrationCreatesMatchesTheCreateScript()
    {
        using var context = new ModelContext();

        var model = context.GetService<IDesignTimeModel>().Model;

        var migrationStatements = context.GetService<IMigrationsSqlGenerator>()
            .Generate(
                context.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel()),
                model)
            .Select(command => command.CommandText);

        Assert.Equal(
            Statements(context.Database.GenerateCreateScript()),
            Statements(string.Join("\n", migrationStatements)));
    }

    /// <summary>
    /// Splits a script into its statements, so that the batch separators the create script carries and
    /// the migration statements do not are not part of the comparison.
    /// </summary>
    private static List<string> Statements(string script)
        => script
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Aggregate(
                new List<string>(),
                (statements, line) =>
                {
                    if (line == "/" || line.Length == 0)
                    {
                        return statements;
                    }

                    if (statements.Count > 0 && char.IsWhiteSpace(line[0]))
                    {
                        statements[^1] += "\n" + line;

                        return statements;
                    }

                    statements.Add(line);

                    return statements;
                });

    private sealed class ModelContext : DbContext
    {
        public DbSet<Order> Orders => Set<Order>();

        public DbSet<Line> Lines => Set<Line>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=127.0.0.1:5236;User Id=SYSDBA;PWD=SYSDBA;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasSequence<int>("OrderNumbers").StartsAt(1000).IncrementsBy(1);

            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("ORDERS", table => table.HasComment("Customer orders"));
                entity.HasKey(order => order.Id);
                entity.Property(order => order.Id).ValueGeneratedOnAdd().HasComment("Identifier");
                entity.Property(order => order.Code).HasMaxLength(20).IsRequired();
                entity.Property(order => order.Amount).HasPrecision(18, 2).HasDefaultValue(0m);
                entity.Property(order => order.Placed).HasColumnType("TIMESTAMP(3)");
                entity.Property(order => order.Reference).HasMaxLength(36);
                entity.Property(order => order.Payload).HasMaxLength(64);
                entity.Property(order => order.PlacedOffset).IsRequired();
                entity.Property(order => order.Day).IsRequired();
                entity.Property(order => order.Span).IsRequired();
                entity.Property(order => order.Code49).HasMaxLength(49).IsFixedLength();
                entity.HasIndex(order => order.Code).IsUnique();
                entity.HasMany(order => order.Lines).WithOne().HasForeignKey(line => line.OrderId);
            });

            modelBuilder.Entity<Line>(entity =>
            {
                entity.ToTable("LINES");
                entity.HasKey(line => line.Id);
                entity.Property(line => line.Id).UseHiLo("OrderNumbers");
            });
        }
    }

    private sealed class Order
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime Placed { get; set; }

        public Guid Reference { get; set; }

        public byte[] Payload { get; set; } = [];

        public DateTimeOffset PlacedOffset { get; set; }

        public DateOnly Day { get; set; }

        public TimeSpan Span { get; set; }

        public string Code49 { get; set; } = string.Empty;

        public List<Line> Lines { get; set; } = [];
    }

    private sealed class Line
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
    }
}
