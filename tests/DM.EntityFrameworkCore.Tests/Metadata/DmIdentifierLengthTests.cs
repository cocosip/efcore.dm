using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DM.EntityFrameworkCore.Tests.Metadata;

/// <summary>
/// T1 tests for the identifier length limit. Dameng rejects a name longer than 128 characters, so the
/// provider declares the limit and the framework truncates a generated name that would exceed it. The
/// truncation itself is pinned by the <c>Updates</c> specification case, which builds a model whose
/// convention-generated table, key, foreign key, index and column names are longer than the limit.
/// </summary>
public class DmIdentifierLengthTests
{
    [Fact]
    public void Model_DeclaresThe128CharacterLimit()
    {
        using var context = new ProbeContext();

        Assert.Equal(128, context.Model.GetMaxIdentifierLength());
    }

    private sealed class Probe
    {
        public int Id { get; set; }
    }

    private sealed class ProbeContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=unused");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>();
    }
}
