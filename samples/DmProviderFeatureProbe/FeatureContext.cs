using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DmProviderFeatureProbe;

/// <summary>
/// The feature model. Every table this context declares is prefixed, is created by the program and
/// is dropped by it, so the probe can run in a catalog that holds other applications' tables.
/// </summary>
internal sealed class FeatureContext(string connectionString) : DbContext
{
    public string ConnectionString { get; } = connectionString;

    public DbSet<AllTypes> AllTypeRows => Set<AllTypes>();

    public DbSet<KeyShape> KeyShapes => Set<KeyShape>();

    public DbSet<LongKeyRow> LongKeyRows => Set<LongKeyRow>();

    public DbSet<CompositeKeyRow> CompositeKeys => Set<CompositeKeyRow>();

    public DbSet<HiLoRow> HiLoRows => Set<HiLoRow>();

    public DbSet<AnnotatedRecord> Annotated => Set<AnnotatedRecord>();

    public DbSet<QuotedRecord> Quoted => Set<QuotedRecord>();

    public DbSet<Shadowed> Shadowed => Set<Shadowed>();

    public DbSet<ShadowChild> ShadowChildren => Set<ShadowChild>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Member> Members => Set<Member>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<Note> Notes => Set<Note>();

    public DbSet<Versioned> Versioned => Set<Versioned>();

    public DbSet<Animal> Animals => Set<Animal>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Tagged> Tagged => Set<Tagged>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDm(
            ConnectionString,
            options => options.CommandTimeout(180));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>(Schema.Sequence).StartsAt(1);

        modelBuilder.Entity<AllTypes>(entity =>
        {
            entity.ToTable("DM_FEATURE_ALL_TYPES");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Text).HasMaxLength(100);
            entity.Property(row => row.TextNull).HasMaxLength(100);
            entity.Property(row => row.FixedText).HasMaxLength(20).IsFixedLength();
            entity.Property(row => row.FixedTextNull).HasMaxLength(20).IsFixedLength();
            entity.Property(row => row.Binary).HasMaxLength(64);
            entity.Property(row => row.BinaryNull).HasMaxLength(64);
            entity.Property(row => row.Decimal).HasPrecision(18, 2);
            entity.Property(row => row.DecimalNull).HasPrecision(18, 2);
            entity.Property(row => row.Timestamp).HasPrecision(6);
            entity.Property(row => row.TimestampNull).HasPrecision(6);
        });

        modelBuilder.Entity<KeyShape>(entity =>
        {
            entity.ToTable("DM_FEATURE_KEY_SHAPES");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.StringKey).HasMaxLength(32);
            entity.HasIndex(row => row.StringKey).IsUnique();
            entity.HasIndex(row => new { row.StringKey, row.GuidKey });
            entity.HasIndex(row => row.GuidKey);
        });

        modelBuilder.Entity<LongKeyRow>(entity =>
        {
            entity.ToTable("DM_FEATURE_LONG_KEYS");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Name).HasMaxLength(32);
        });

        modelBuilder.Entity<CompositeKeyRow>(entity =>
        {
            entity.ToTable("DM_FEATURE_COMPOSITE_KEYS");
            entity.HasKey(row => new { row.PartA, row.PartB });
            entity.Property(row => row.PartB).HasMaxLength(32);
            entity.Property(row => row.Payload).HasMaxLength(64);
        });

        modelBuilder.Entity<HiLoRow>(entity =>
        {
            entity.ToTable("DM_FEATURE_HILO_ROWS");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).UseHiLo(Schema.Sequence);
            entity.Property(row => row.Name).HasMaxLength(32);
        });

        modelBuilder.Entity<AnnotatedRecord>(entity => entity.HasKey(row => row.Id));

        modelBuilder.Entity<QuotedRecord>(entity =>
        {
            entity.ToTable("DM_FEATURE_SELECT");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.User).HasColumnName("USER").HasMaxLength(32);
            entity.Property(row => row.Order).HasColumnName("ORDER").HasMaxLength(32);
        });

        modelBuilder.Entity<Shadowed>(entity =>
        {
            entity.ToTable("DM_FEATURE_SHADOWED");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Name).HasMaxLength(32);
            entity.Property(row => row.Note).HasMaxLength(64);
            entity.Property<int>("ShadowNumber");
        });

        modelBuilder.Entity<ShadowChild>(entity =>
        {
            entity.ToTable("DM_FEATURE_SHADOW_CHILDREN");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Name).HasMaxLength(32);
            entity.HasOne<Shadowed>()
                .WithMany(parent => parent.Children)
                .HasForeignKey("ParentId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("DM_FEATURE_USERS");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Name).HasMaxLength(50);
            entity.HasOne(user => user.Profile)
                .WithOne(profile => profile.User)
                .HasForeignKey<Profile>(profile => profile.Id);
        });

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("DM_FEATURE_PROFILES");
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Id).ValueGeneratedNever();
            entity.Property(profile => profile.Bio).HasMaxLength(200);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("DM_FEATURE_ORDERS");
            entity.HasKey(order => order.Id);
            entity.Property(order => order.Number).HasMaxLength(32);
            entity.HasMany(order => order.Lines)
                .WithOne(line => line.Order)
                .HasForeignKey(line => line.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(order => order.Invoice)
                .WithOne(invoice => invoice.Order)
                .HasForeignKey<Invoice>(invoice => invoice.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.OwnsOne(order => order.Shipping, owned =>
            {
                owned.ToJson();
                owned.Property(shipping => shipping.Street).HasMaxLength(100);
                owned.Property(shipping => shipping.City).HasMaxLength(50);
            });
        });

        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.ToTable("DM_FEATURE_ORDER_LINES");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Sku).HasMaxLength(32);
            entity.Property(line => line.Price).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("DM_FEATURE_INVOICES");
            entity.HasKey(invoice => invoice.Id);
            entity.Property(invoice => invoice.Number).HasMaxLength(32);
            entity.HasIndex(invoice => invoice.OrderId).IsUnique();
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("DM_FEATURE_STUDENTS");
            entity.HasKey(student => student.Id);
            entity.Property(student => student.Name).HasMaxLength(50);
            entity.HasMany(student => student.Courses)
                .WithMany(course => course.Students)
                .UsingEntity("DM_FEATURE_STUDENT_COURSE");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("DM_FEATURE_COURSES");
            entity.HasKey(course => course.Id);
            entity.Property(course => course.Title).HasMaxLength(100);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("DM_FEATURE_PROJECTS");
            entity.HasKey(project => project.Id);
            entity.Property(project => project.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.ToTable("DM_FEATURE_MEMBERS");
            entity.HasKey(member => member.Id);
            entity.Property(member => member.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("DM_FEATURE_MEMBERSHIPS");
            entity.HasKey(membership => new { membership.ProjectId, membership.MemberId });
            entity.Property(membership => membership.Role).HasMaxLength(32);
            entity.HasOne(membership => membership.Project)
                .WithMany(project => project.Memberships)
                .HasForeignKey(membership => membership.ProjectId);
            entity.HasOne(membership => membership.Member)
                .WithMany(member => member.Memberships)
                .HasForeignKey(membership => membership.MemberId);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("DM_FEATURE_CATEGORIES");
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).HasMaxLength(32);
            entity.HasOne(category => category.Parent)
                .WithMany(category => category.Children)
                .HasForeignKey(category => category.ParentId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("DM_FEATURE_TICKETS");
            entity.HasKey(ticket => ticket.Id);
            entity.Property(ticket => ticket.Subject).HasMaxLength(100);
            entity.HasOne(ticket => ticket.CreatedBy)
                .WithMany()
                .HasForeignKey(ticket => ticket.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ticket => ticket.AssignedTo)
                .WithMany()
                .HasForeignKey(ticket => ticket.AssignedToId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Note>(entity =>
        {
            entity.ToTable("DM_FEATURE_NOTES");
            entity.HasKey(note => note.Id);
            entity.Property(note => note.Text).HasMaxLength(100);
            entity.Property(note => note.Score).HasDefaultValue(7);
            entity.Property(note => note.Double).HasComputedColumnSql("\"Score\" * 2");
            entity.Property(note => note.Version).IsConcurrencyToken();
            entity.HasQueryFilter(note => !note.IsDeleted);
            entity.HasOne(note => note.User)
                .WithMany()
                .HasForeignKey(note => note.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Versioned>(entity =>
        {
            entity.ToTable("DM_FEATURE_VERSIONED");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Name).HasMaxLength(50);
            entity.Property(row => row.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<Animal>(entity =>
        {
            entity.ToTable("DM_FEATURE_ANIMALS");
            entity.HasKey(animal => animal.Id);
            entity.Property(animal => animal.Name).HasMaxLength(50);
            entity.HasDiscriminator<string>("kind")
                .HasValue<Dog>("dog")
                .HasValue<Cat>("cat");
            entity.Property("kind").HasMaxLength(8);
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.ToTable("DM_FEATURE_VEHICLES");
            entity.HasKey(vehicle => vehicle.Id);
            entity.Property(vehicle => vehicle.Name).HasMaxLength(50);
            entity.UseTptMappingStrategy();
        });

        modelBuilder.Entity<Car>(entity => entity.ToTable("DM_FEATURE_CARS"));

        modelBuilder.Entity<Truck>(entity =>
        {
            entity.ToTable("DM_FEATURE_TRUCKS");
            entity.Property(truck => truck.Payload).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.UseTpcMappingStrategy();
        });

        modelBuilder.Entity<CardPayment>(entity =>
        {
            entity.ToTable("DM_FEATURE_CARD_PAYMENTS");
            entity.Property(payment => payment.Last4).HasMaxLength(4);
        });

        modelBuilder.Entity<CashPayment>(entity =>
        {
            entity.ToTable("DM_FEATURE_CASH_PAYMENTS");
            entity.Property(payment => payment.Currency).HasMaxLength(3);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("DM_FEATURE_CUSTOMERS");
            entity.HasKey(customer => customer.Id);
            entity.Property(customer => customer.Name).HasMaxLength(50);
            entity.OwnsOne(customer => customer.Address, owned =>
            {
                owned.Property(address => address.Street).HasMaxLength(100);
                owned.Property(address => address.City).HasMaxLength(50);
            });
            entity.ComplexProperty(customer => customer.Balance, complex =>
            {
                complex.Property(money => money.Amount).HasPrecision(18, 2);
                complex.Property(money => money.Currency).HasMaxLength(3);
            });
            entity.Property(customer => customer.TierText)
                .HasConversion<string>()
                .HasMaxLength(32);
        });

        modelBuilder.Entity<Tagged>(entity =>
        {
            entity.ToTable("DM_FEATURE_TAGGED");
            entity.HasKey(tagged => tagged.Id);
            entity.Property(tagged => tagged.Name).HasMaxLength(32);
        });
    }
}
