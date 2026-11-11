using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Update;

/// <summary>
/// T2 tests for the statement that writes a change inside a JSON document. They drive the real update
/// pipeline against a recording connection, because the framework reports such a change as a value for a
/// path rather than as the whole document, and the value alone would replace the document.
/// </summary>
public class DmJsonUpdateSqlTests
{
    [Fact]
    public void MemberChange_WritesTheMemberWithJsonSet()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        var probe = new Probe { Id = 1, Document = new Document { Title = "first", Pages = 3 } };
        context.Attach(probe);
        probe.Document.Pages = 9;
        context.SaveChanges();

        Assert.Equal(
            """
            UPDATE "Probes" SET "Document" = JSON_SET("Document", '$.Pages', :p0)
            WHERE "Id" = :p1;
            """,
            Statement(Assert.Single(connection.Commands)));
    }

    /// <summary>
    /// A document that is replaced as a whole is written as the whole document, which needs no
    /// <c>JSON_SET</c>.
    /// </summary>
    [Fact]
    public void WholeDocumentChange_WritesTheDocument()
    {
        var connection = new FakeDmConnection();
        using var context = new ProbeContext(connection);

        var probe = new Probe { Id = 1, Document = new Document { Title = "first", Pages = 3 } };
        context.Attach(probe);
        probe.Document = new Document { Title = "second", Pages = 7 };
        context.SaveChanges();

        Assert.Equal(
            """
            UPDATE "Probes" SET "Document" = :p0
            WHERE "Id" = :p1;
            """,
            Statement(Assert.Single(connection.Commands)));
    }

    private static string Statement(string command)
        => string.Join(
            "\n",
            command
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Where(line => !line.StartsWith("--", StringComparison.Ordinal)))
            .TrimEnd('\n');

    private sealed class ProbeContext : DbContext
    {
        private readonly FakeDmConnection _connection;

        public ProbeContext(FakeDmConnection connection) => _connection = connection;

        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm(_connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(
                entity =>
                {
                    entity.HasKey(probe => probe.Id);
                    entity.Property(probe => probe.Id).ValueGeneratedNever();
                    entity.OwnsOne(probe => probe.Document, owned => owned.ToJson());
                });
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public Document Document { get; set; } = new();
    }

    private sealed class Document
    {
        public string Title { get; set; } = string.Empty;

        public int Pages { get; set; }
    }
}
