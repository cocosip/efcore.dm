using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Query;

/// <summary>
/// T2 tests for raw SQL composition and for the bulk operations that bypass <c>SaveChanges</c>.
/// The bulk statements are captured through a recording connection, because they have no
/// <c>ToQueryString</c>.
/// </summary>
/// <remarks>
/// Dameng's grammar declares a table alias as <c>[AS] &lt;alias&gt;</c>, so the separator the
/// relational layer emits is valid here. See the DM SQL manual, "数据的插入、删除和修改".
/// </remarks>
public class DmDmlQueryTests
{
    [Fact]
    public void FromSql_PassesTheStatementThrough()
    {
        using var context = new ProbeContext(new FakeDmConnection());

        Assert.Equal(
            Sql("SELECT * FROM \"Blogs\""),
            Sql(context.Blogs.FromSql($"SELECT * FROM \"Blogs\"").ToQueryString()));
    }

    [Fact]
    public void FromSql_ComposesAsADerivedTable()
    {
        using var context = new ProbeContext(new FakeDmConnection());

        Assert.Equal(
            """
            SELECT "e"."Id", "e"."Name"
            FROM (
                SELECT * FROM "Blogs"
            ) AS "e"
            WHERE "e"."Name" = 'x'
            """,
            Sql(context.Blogs
                .FromSql($"SELECT * FROM \"Blogs\"")
                .Where(b => b.Name == "x")
                .ToQueryString()));
    }

    [Fact]
    public void ExecuteUpdate_GeneratesUpdateWithoutSelect()
    {
        var connection = new FakeDmConnection { RowsAffected = 3 };
        using var context = new ProbeContext(connection);

        var affected = context.Blogs
            .Where(b => b.Name == "x")
            .ExecuteUpdate(setters => setters.SetProperty(b => b.Name, "y"));

        Assert.Equal(3, affected);
        Assert.Equal(
            """
            UPDATE "Blogs" AS "b"
            SET "Name" = :p
            WHERE "b"."Name" = 'x'
            """,
            Sql(Assert.Single(connection.Commands)));
    }

    [Fact]
    public void ExecuteDelete_GeneratesDeleteWithoutSelect()
    {
        var connection = new FakeDmConnection { RowsAffected = 3 };
        using var context = new ProbeContext(connection);

        var affected = context.Blogs
            .Where(b => b.Name == "x")
            .ExecuteDelete();

        Assert.Equal(3, affected);
        Assert.Equal(
            """
            DELETE FROM "Blogs" AS "b"
            WHERE "b"."Name" = 'x'
            """,
            Sql(Assert.Single(connection.Commands)));
    }

    private static string Sql(string sql)
        => sql.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private sealed class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public ProbeContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<Blog> Blogs => Set<Blog>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Blog>(entity =>
            {
                entity.Property(b => b.Id).ValueGeneratedNever();
                entity.Property(b => b.Name).HasMaxLength(50);
            });
    }

    private sealed class Blog
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
