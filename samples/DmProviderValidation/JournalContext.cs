using Microsoft.EntityFrameworkCore;

namespace DmProviderValidation;

/// <summary>
/// The context the migration steps use, kept apart from <see cref="ValidationContext" /> on purpose: the
/// acceptance run creates the validation tables with the create script, and a migration cannot create a
/// table that is already there. Its own table and its own history row let the migration path be exercised
/// in the same database without the two getting in each other's way.
/// </summary>
internal sealed class JournalContext : DbContext
{
    public JournalContext(string connectionString) => ConnectionString = connectionString;

    public string ConnectionString { get; }

    public DbSet<Journal> Journal => Set<Journal>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDm(ConnectionString);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Journal>(entity =>
        {
            entity.ToTable("DM_VALIDATION_JOURNAL");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Note).HasMaxLength(100).IsRequired();
            entity.HasIndex(entry => entry.Note);
        });
}

/// <summary>
/// One row a migration step writes, so that the table a migration created is read back through the model
/// the migration was generated from.
/// </summary>
internal sealed class Journal
{
    public int Id { get; set; }

    public string Note { get; set; } = string.Empty;
}
