using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Tells whether a database facade belongs to this provider.
/// </summary>
/// <remarks>
/// Declared in <see cref="Microsoft.EntityFrameworkCore" /> rather than in a provider namespace, so that
/// code written for the Dameng provider keeps compiling against this one.
/// </remarks>
public static class DmDatabaseFacadeExtensions
{
    /// <summary>
    /// Returns <see langword="true" /> when the context behind <paramref name="database" /> is configured
    /// for Dameng.
    /// </summary>
    public static bool IsDm(this DatabaseFacade database)
    {
        ArgumentNullException.ThrowIfNull(database);

        return string.Equals(
            database.ProviderName,
            typeof(DmDatabaseFacadeExtensions).GetTypeInfo().Assembly.GetName().Name,
            StringComparison.Ordinal);
    }
}
