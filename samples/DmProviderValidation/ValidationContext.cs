using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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
    private readonly Action<DmDbContextOptionsBuilder>? _configure;

    public ValidationContext(string connectionString, Action<DmDbContextOptionsBuilder>? configure = null)
    {
        ConnectionString = connectionString;
        _configure = configure;
    }

    public string ConnectionString { get; }

    public DbSet<Record> Records => Set<Record>();

    public DbSet<Parent> Parents => Set<Parent>();

    public DbSet<Child> Children => Set<Child>();

    public DbSet<Computed> Computeds => Set<Computed>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDm(ConnectionString, _configure);

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
            // A string with no length becomes a character large object, which is right for a document
            // and wrong for a value a query compares, so the fields this run filters on name a length.
            entity.Property(record => record.Status).HasMaxLength(32);
            entity.Property(record => record.Optional).HasMaxLength(100);
            entity.Property(record => record.Version).IsConcurrencyToken();
            entity.OwnsOne(record => record.Document, owned => owned.ToJson());
        });

        modelBuilder.Entity<Parent>(entity =>
        {
            entity.ToTable("DM_VALIDATION_PARENT");
            entity.HasKey(parent => parent.Id);
            entity.Property(parent => parent.Name).HasMaxLength(100);
        });

        // A computed column is a virtual one the server derives, so the acceptance run proves the script and
        // the read back against the server rather than only the shape of the DDL.
        modelBuilder.Entity<Computed>(entity =>
        {
            entity.ToTable("DM_VALIDATION_COMPUTED");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Total).HasComputedColumnSql("\"Amount\" * 2");
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

    public Document Document { get; set; } = new();
}

/// <summary>
/// The entity a JSON column holds. The server has a <c>JSON</c> type, so the document is stored in one
/// column and read back through it.
/// </summary>
internal sealed class Document
{
    public string Title { get; set; } = string.Empty;

    public int Pages { get; set; }

    public bool Flag { get; set; }
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

/// <summary>A row whose total the server derives from its amount.</summary>
internal sealed class Computed
{
    public int Id { get; set; }

    public int Amount { get; set; }

    public int Total { get; set; }
}
