using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DM.EntityFrameworkCore.Tests.Metadata;

/// <summary>
/// T1 tests for the binary column a concurrency token takes. Dameng has no equality operator for a
/// large object, and the concurrency check compares the token in the statement's predicate, so an
/// unbounded byte array token has to be a bounded binary column. Without that, every update or delete
/// of the entity is rejected by the server before the row count can be checked.
/// </summary>
public class DmByteArrayConcurrencyTokenTests
{
    [Fact]
    public void UnboundedToken_GetsTheBoundedBinaryType()
    {
        using var context = new TokenContext();

        var token = context.Model.FindEntityType(typeof(Probe))!.FindProperty(nameof(Probe.Token))!;

        Assert.Equal("VARBINARY(900)", token.GetColumnType());
    }

    [Fact]
    public void UnboundedPayload_StaysALargeObject()
    {
        using var context = new TokenContext();

        var payload = context.Model.FindEntityType(typeof(Probe))!.FindProperty(nameof(Probe.Payload))!;

        Assert.Equal("BLOB", payload.GetColumnType());
    }

    private sealed class Probe
    {
        public int Id { get; set; }

        public byte[] Token { get; set; } = [];

        public byte[] Payload { get; set; } = [];
    }

    private sealed class TokenContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDm("Server=unused");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Probe>(builder =>
            {
                builder.Property(e => e.Token).IsConcurrencyToken();
                builder.Property(e => e.Payload);
            });
    }
}
