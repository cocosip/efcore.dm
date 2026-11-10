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
    /// <summary>
    /// Configures the context to connect to a Dameng database with a connection string.
    /// </summary>
    /// <param name="optionsBuilder">The builder being configured.</param>
    /// <param name="connectionString">The connection string, as the driver's connection accepts it.</param>
    /// <param name="dmOptionsAction">An optional action that configures the provider's own options.</param>
    /// <returns>The same builder, so that calls can be chained.</returns>
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

    /// <summary>
    /// Configures the context to connect to a Dameng database with the given connection. The caller
    /// owns the connection and is responsible for disposing it.
    /// </summary>
    /// <param name="optionsBuilder">The builder being configured.</param>
    /// <param name="connection">The connection the context uses.</param>
    /// <param name="dmOptionsAction">An optional action that configures the provider's own options.</param>
    /// <returns>The same builder, so that calls can be chained.</returns>
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

    /// <summary>
    /// Configures the context to connect to a Dameng database with a connection string.
    /// </summary>
    /// <typeparam name="TContext">The context type being configured.</typeparam>
    /// <param name="optionsBuilder">The builder being configured.</param>
    /// <param name="connectionString">The connection string, as the driver's connection accepts it.</param>
    /// <param name="dmOptionsAction">An optional action that configures the provider's own options.</param>
    /// <returns>The same builder, so that calls can be chained.</returns>
    public static DbContextOptionsBuilder<TContext> UseDm<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<DmDbContextOptionsBuilder>? dmOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseDm((DbContextOptionsBuilder)optionsBuilder, connectionString, dmOptionsAction);

    /// <summary>
    /// Configures the context to connect to a Dameng database with the given connection. The caller
    /// owns the connection and is responsible for disposing it.
    /// </summary>
    /// <typeparam name="TContext">The context type being configured.</typeparam>
    /// <param name="optionsBuilder">The builder being configured.</param>
    /// <param name="connection">The connection the context uses.</param>
    /// <param name="dmOptionsAction">An optional action that configures the provider's own options.</param>
    /// <returns>The same builder, so that calls can be chained.</returns>
    public static DbContextOptionsBuilder<TContext> UseDm<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        Action<DmDbContextOptionsBuilder>? dmOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseDm((DbContextOptionsBuilder)optionsBuilder, connection, dmOptionsAction);

    private static DmOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.Options.FindExtension<DmOptionsExtension>() ?? new DmOptionsExtension();
}
