using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Database lifecycle operations for Dameng.
/// </summary>
/// <remarks>
/// ponytail: only <see cref="Exists" /> is implemented, by attempting a connection. Catalog
/// queries for <see cref="HasTables" /> and schema creation belong to roadmap phase P1, where
/// they are written against a real server catalog rather than guessed. Until then those
/// members fail loudly instead of returning a plausible wrong answer.
/// </remarks>
internal sealed class DmDatabaseCreator : RelationalDatabaseCreator
{
    public DmDatabaseCreator(RelationalDatabaseCreatorDependencies dependencies)
        : base(dependencies)
    {
    }

    public override bool Exists()
    {
        try
        {
            Dependencies.Connection.Open();

            return true;
        }
        catch (global::Dm.DmException)
        {
            return false;
        }
        finally
        {
            Dependencies.Connection.Close();
        }
    }

    public override async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Dependencies.Connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (global::Dm.DmException)
        {
            return false;
        }
        finally
        {
            await Dependencies.Connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public override bool HasTables()
        => throw new NotSupportedException(
            "HasTables requires Dameng catalog queries, which are implemented in roadmap phase P1.");

    public override void Create()
        => throw new NotSupportedException(
            "Create is implemented in roadmap phase P1, together with the Dameng catalog queries.");

    public override void Delete()
        => throw new NotSupportedException(
            "Delete is implemented in roadmap phase P1, together with the Dameng catalog queries.");
}
