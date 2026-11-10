using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Tells a migration whether it is being built for this provider.
/// </summary>
/// <remarks>
/// A migration that mixes providers guards its provider specific operations with this method, so the
/// same migration assembly can target more than one database.
/// </remarks>
public static class DmMigrationBuilderExtensions
{
    /// <summary>
    /// Whether the migration is being built for the Dameng provider.
    /// </summary>
    public static bool IsDm(this MigrationBuilder migrationBuilder)
        => string.Equals(
            migrationBuilder.ActiveProvider,
            typeof(DmMigrationBuilderExtensions).GetTypeInfo().Assembly.GetName().Name,
            StringComparison.Ordinal);
}
