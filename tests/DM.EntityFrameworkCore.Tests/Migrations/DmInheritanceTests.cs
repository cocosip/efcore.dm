using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Migrations;

/// <summary>
/// T2 tests for the inheritance mapping strategies. Each one maps a hierarchy to tables in its own
/// way, so the generated DDL and the generated query are asserted together.
/// </summary>
/// <remarks>
/// Each strategy has its own context type, because EF Core caches the model per context type.
/// </remarks>
public class DmInheritanceTests
{
    /// <summary>
    /// A single table holds every type, told apart by a discriminator column, so only the root table
    /// generates the key.
    /// </summary>
    [Fact]
    public void TablePerHierarchy_CreatesOneTableWithADiscriminator()
    {
        using var context = new TphContext(new FakeDmConnection { RowsAffected = 1 });

        var script = Normalize(context.Database.GenerateCreateScript());

        Assert.Contains("\"Id\" INT IDENTITY(1,1) NOT NULL", script, StringComparison.Ordinal);
        Assert.Contains("\"Discriminator\" NVARCHAR2(8) NOT NULL", script, StringComparison.Ordinal);
        Assert.Equal(1, Counted(context, "CREATE TABLE"));
        Assert.Contains("WHERE \"a\".\"Discriminator\" = 'Cat'", context.Animals.OfType<Cat>().ToQueryString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Each type has its own table, and the derived tables carry a key that repeats the value of the
    /// base row. Only the base table generates it, because the derived insert writes the value the
    /// base insert produced.
    /// </summary>
    [Fact]
    public void TablePerType_GeneratesTheKeyInTheBaseTableOnly()
    {
        using var context = new TptContext(new FakeDmConnection { RowsAffected = 1 });

        var script = Normalize(context.Database.GenerateCreateScript());

        Assert.Contains(
            """
            CREATE TABLE "Animals" (
                "Id" INT IDENTITY(1,1) NOT NULL,
            """,
            script,
            StringComparison.Ordinal);

        Assert.Contains(
            """
            CREATE TABLE "Cats" (
                "Id" INT NOT NULL,
            """,
            script,
            StringComparison.Ordinal);

        Assert.Contains(
            """
            CREATE TABLE "Dogs" (
                "Id" INT NOT NULL,
            """,
            script,
            StringComparison.Ordinal);

        Assert.Equal(3, Counted(context, "CREATE TABLE"));
    }

    /// <summary>
    /// The insert of a derived row is two statements: the base row generates the key, and the derived
    /// row writes the value that was read back from it.
    /// </summary>
    [Fact]
    public void TablePerType_InsertWritesTheGeneratedKeyToTheDerivedTable()
    {
        var connection = new FakeDmConnection { RowsAffected = 1, ReadBackColumns = ["Id"], ReadBackRow = [7] };

        using var context = new TptContext(connection);
        var cat = new Cat { Name = "a", Lives = 9 };
        context.Add(cat);

        context.SaveChanges();

        Assert.Equal(7, cat.Id);
        Assert.Equal(2, connection.Commands.Count);
        Assert.Contains("INSERT INTO \"Animals\"", connection.Commands[0], StringComparison.Ordinal);
        Assert.Contains("INSERT INTO \"Cats\" (\"Id\", \"Lives\")", connection.Commands[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// A query over a derived type joins the tables of the hierarchy.
    /// </summary>
    [Fact]
    public void TablePerType_QueryJoinsTheTables()
    {
        using var context = new TptContext(new FakeDmConnection { RowsAffected = 1 });

        var sql = context.Animals.OfType<Cat>().ToQueryString();

        Assert.Contains("LEFT JOIN \"Cats\" AS \"c\" ON \"a\".\"Id\" = \"c\".\"Id\"", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Each concrete type has a table of its own with its key column, and no table generates the key
    /// because a hierarchy mapped this way has no single place to do it. The application assigns the
    /// key, or the model configures a generator for it.
    /// </summary>
    [Fact]
    public void TablePerConcreteType_CreatesATablePerTypeWithoutAGeneratedKey()
    {
        using var context = new TpcContext(new FakeDmConnection { RowsAffected = 1 });

        var script = Normalize(context.Database.GenerateCreateScript());

        Assert.Equal(2, Counted(context, "CREATE TABLE"));
        Assert.DoesNotContain("IDENTITY", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Discriminator", script, StringComparison.Ordinal);
        Assert.Equal("Never", context.Model.FindEntityType(typeof(Cat))!.FindProperty(nameof(Animal.Id))!.ValueGenerated.ToString());
    }

    private static string Normalize(string script) => script.ReplaceLineEndings("\n");

    private static int Counted(DbContext context, string fragment)
        => context.Database.GenerateCreateScript().Split(fragment, StringSplitOptions.None).Length - 1;

    private abstract class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        protected ProbeContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<Animal> Animals => Set<Animal>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Animal>(e => e.Property(p => p.Name).HasMaxLength(50));
            modelBuilder.Entity<Cat>();
            modelBuilder.Entity<Dog>();
        }
    }

    private sealed class TphContext : ProbeContext
    {
        public TphContext(FakeDmConnection connection)
            : base(connection)
        {
        }
    }

    private sealed class TptContext : ProbeContext
    {
        public TptContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Animal>().UseTptMappingStrategy();
            modelBuilder.Entity<Cat>().ToTable("Cats");
            modelBuilder.Entity<Dog>().ToTable("Dogs");
        }
    }

    private sealed class TpcContext : ProbeContext
    {
        public TpcContext(FakeDmConnection connection)
            : base(connection)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Animal>().UseTpcMappingStrategy();
            modelBuilder.Entity<Cat>().ToTable("Cats");
            modelBuilder.Entity<Dog>().ToTable("Dogs");
        }
    }

    private abstract class Animal
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class Cat : Animal
    {
        public int Lives { get; set; }
    }

    private sealed class Dog : Animal
    {
        public bool Barks { get; set; }
    }
}
