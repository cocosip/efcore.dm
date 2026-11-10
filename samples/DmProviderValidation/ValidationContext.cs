using Microsoft.EntityFrameworkCore;

namespace DmProviderValidation;

/// <summary>
/// The model the acceptance steps run against: one row type that carries every supported type, and
/// a pair of tables for the joins.
/// </summary>
/// <remarks>
/// Every table is prefixed and the program drops them at the end, so a run cleans up only what it
/// created. A run identifier on each row isolates the data of one run from another.
/// </remarks>
internal sealed class ValidationContext : DbContext
{
    public ValidationContext(string connectionString) => ConnectionString = connectionString;

    public string ConnectionString { get; }

    public DbSet<Record> Records => Set<Record>();

    public DbSet<Parent> Parents => Set<Parent>();

    public DbSet<Child> Children => Set<Child>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDm(ConnectionString);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>("DM_VALIDATION_SEQ").StartsAt(1000);

        modelBuilder.Entity<Record>(entity =>
        {
            entity.ToTable("DM_VALIDATION_RECORD");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Text).HasMaxLength(100);
            entity.Property(record => record.FixedText).HasMaxLength(20).IsFixedLength();
            entity.Property(record => record.Money).HasPrecision(18, 2);
            entity.Property(record => record.Payload).HasMaxLength(64);
            entity.Property(record => record.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<Parent>(entity =>
        {
            entity.ToTable("DM_VALIDATION_PARENT");
            entity.HasKey(parent => parent.Id);
            entity.Property(parent => parent.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Child>(entity =>
        {
            entity.ToTable("DM_VALIDATION_CHILD");
            entity.HasKey(child => child.Id);
            entity.Property(child => child.Name).HasMaxLength(100);
            entity.HasOne<Parent>().WithMany().HasForeignKey(child => child.ParentId);
        });
    }
}

internal sealed class Record
{
    public int Id { get; set; }

    public Guid RunId { get; set; }

    public string Text { get; set; } = string.Empty;

    public string FixedText { get; set; } = string.Empty;

    public string? Optional { get; set; }

    public string Status { get; set; } = "new";

    public int Number { get; set; }

    public long BigNumber { get; set; }

    public short SmallNumber { get; set; }

    public decimal Money { get; set; }

    public double Ratio { get; set; }

    public float Ratio32 { get; set; }

    public bool Flag { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTimeOffset Offset { get; set; }

    public DateOnly Day { get; set; }

    public TimeOnly Time { get; set; }

    public TimeSpan Duration { get; set; }

    public Guid Key { get; set; }

    public byte[] Payload { get; set; } = [];

    public int Version { get; set; }
}

internal sealed class Parent
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class Child
{
    public int Id { get; set; }

    public int? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;
}
