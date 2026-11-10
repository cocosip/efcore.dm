using System.Data.Common;
using DM.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Entry point for configuring the Dameng provider. Declared in the
/// <c>Microsoft.EntityFrameworkCore</c> namespace so that a project that already references
/// the official provider needs no new <c>using</c> directive.
/// </summary>
public static class DmDbContextOptionsExtensions
{
    public static DbContextOptionsBuilder UseDm(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<DmDbContextOptionsBuilder>? dmOptionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        var extension = GetOrCreateExtension(optionsBuilder).WithConnectionString(connectionString);

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        dmOptionsAction?.Invoke(new DmDbContextOptionsBuilder(optionsBuilder));

        return optionsBuilder;
    }

    public static DbContextOptionsBuilder UseDm(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        Action<DmDbContextOptionsBuilder>? dmOptionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(connection);

        var extension = GetOrCreateExtension(optionsBuilder).WithConnection(connection);

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        dmOptionsAction?.Invoke(new DmDbContextOptionsBuilder(optionsBuilder));

        return optionsBuilder;
    }

    public static DbContextOptionsBuilder<TContext> UseDm<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<DmDbContextOptionsBuilder>? dmOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseDm((DbContextOptionsBuilder)optionsBuilder, connectionString, dmOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseDm<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        Action<DmDbContextOptionsBuilder>? dmOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseDm((DbContextOptionsBuilder)optionsBuilder, connection, dmOptionsAction);

    private static DmOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.Options.FindExtension<DmOptionsExtension>() ?? new DmOptionsExtension();
}
