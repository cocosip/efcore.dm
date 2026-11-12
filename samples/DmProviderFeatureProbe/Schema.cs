using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace DmProviderFeatureProbe;

/// <summary>
/// The schema the probe owns. Tables are listed children first, which is the order the server needs
/// for a delete and the order a drop tolerates; every object is prefixed with <c>DM_FEATURE_</c>.
/// </summary>
internal static class Schema
{
    public const string Sequence = "DM_FEATURE_HILO";

    public static IReadOnlyList<string> Tables { get; } =
    [
        "DM_FEATURE_STUDENT_COURSE",
        "DM_FEATURE_MEMBERSHIPS",
        "DM_FEATURE_ORDER_LINES",
        "DM_FEATURE_INVOICES",
        "DM_FEATURE_PROFILES",
        "DM_FEATURE_TICKETS",
        "DM_FEATURE_NOTES",
        "DM_FEATURE_VERSIONED",
        "DM_FEATURE_SHADOW_CHILDREN",
        "DM_FEATURE_CATEGORIES",
        "DM_FEATURE_ORDERS",
        "DM_FEATURE_USERS",
        "DM_FEATURE_MEMBERS",
        "DM_FEATURE_PROJECTS",
        "DM_FEATURE_STUDENTS",
        "DM_FEATURE_COURSES",
        "DM_FEATURE_CUSTOMERS",
        "DM_FEATURE_TAGGED",
        "DM_FEATURE_ALL_TYPES",
        "DM_FEATURE_KEY_SHAPES",
        "DM_FEATURE_LONG_KEYS",
        "DM_FEATURE_COMPOSITE_KEYS",
        "DM_FEATURE_HILO_ROWS",
        "DM_FEATURE_SHADOWED",
        "DM_FEATURE_ANNOTATED",
        "DM_FEATURE_SELECT",
        "DM_FEATURE_ANIMALS",
        "DM_FEATURE_CARS",
        "DM_FEATURE_TRUCKS",
        "DM_FEATURE_VEHICLES",
        "DM_FEATURE_CARD_PAYMENTS",
        "DM_FEATURE_CASH_PAYMENTS"
    ];

    /// <summary>
    /// Drops every object this program declares and nothing else. Dameng has no
    /// <c>DROP TABLE IF EXISTS</c>, so a missing object answers an error that is ignored: the run is
    /// about to create it again either way.
    /// </summary>
    public static async Task DropAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new Dm.DmConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var statement in Tables
                     .Select(table => $"DROP TABLE \"{table}\" CASCADE CONSTRAINTS")
                     .Append($"DROP SEQUENCE \"{Sequence}\""))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (DbException)
            {
                // The object was not there, which is what the first run against a database looks like.
            }
        }
    }

    /// <summary>
    /// Empties the probe's tables before a step. A failure here is not swallowed: leftover rows would
    /// make the next step count wrong, and a count that is wrong in the passing direction hides a defect.
    /// </summary>
    public static async Task ClearAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        // A self-referencing parent would otherwise block the delete of its category.
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE \"DM_FEATURE_CATEGORIES\" SET \"ParentId\" = NULL",
            cancellationToken);

        foreach (var table in Tables)
        {
            // The name comes from the constant list above, never from input, so it is safe to concatenate.
            await context.Database.ExecuteSqlRawAsync(
                string.Concat("DELETE FROM \"", table, "\""),
                cancellationToken);
        }
    }
}
