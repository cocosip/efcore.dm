using System.Text.RegularExpressions;
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

        var sql = Sql(context.Blogs
            .FromSql($"SELECT * FROM \"Blogs\"")
            .Where(b => b.Name == "x")
            .ToQueryString());

        // The alias is generated from the context type, not from the query, so the assertion pins
        // that the same alias is used on every reference rather than a particular letter.
        var alias = Regex.Match(sql, "AS \"(?<alias>\\w+)\"").Groups["alias"].Value;

        Assert.Equal(
            $"""
            SELECT "{alias}"."Id", "{alias}"."Name"
            FROM (
                SELECT * FROM "Blogs"
            ) AS "{alias}"
            WHERE "{alias}"."Name" = 'x'
            """,
            sql);
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
            SET "Name" = 'y'
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

    [Fact]
    public void ExecuteUpdate_RewritesTheQueryFilterBoolean()
    {
        var connection = new FakeDmConnection { RowsAffected = 1 };
        using var context = new ProbeContext(connection);

        var affected = context.Filtereds.ExecuteUpdate(setters => setters.SetProperty(f => f.Name, "y"));

        Assert.Equal(1, affected);
        Assert.Equal(
            """
            UPDATE "Filtereds" AS "f"
            SET "Name" = 'y'
            WHERE NOT ("f"."IsDeleted" = 1)
            """,
            Sql(Assert.Single(connection.Commands)));
    }

    [Fact]
    public void ExecuteDelete_RewritesTheQueryFilterBoolean()
    {
        var connection = new FakeDmConnection { RowsAffected = 1 };
        using var context = new ProbeContext(connection);

        var affected = context.Filtereds.ExecuteDelete();

        Assert.Equal(1, affected);
        Assert.Equal(
            """
            DELETE FROM "Filtereds" AS "f"
            WHERE NOT ("f"."IsDeleted" = 1)
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

        public DbSet<Filtered> Filtereds => Set<Filtered>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>(entity =>
            {
                entity.Property(b => b.Id).ValueGeneratedNever();
                entity.Property(b => b.Name).HasMaxLength(50);
            });

            modelBuilder.Entity<Filtered>(entity =>
            {
                entity.Property(f => f.Id).ValueGeneratedNever();
                entity.Property(f => f.Name).HasMaxLength(50);
                entity.HasQueryFilter(f => !f.IsDeleted);
            });
        }
    }

    private sealed class Filtered
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }
    }

    private sealed class Blog
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
