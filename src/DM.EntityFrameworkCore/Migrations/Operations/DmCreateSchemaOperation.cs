using Microsoft.EntityFrameworkCore.Migrations;

namespace Microsoft.EntityFrameworkCore.Migrations.Operations;

/// <summary>
/// Creates a schema.
/// </summary>
/// <remarks>
/// A schema belongs to a user in Dameng, so the operation names the owner. A schema that already
/// exists fails the migration, which is why <c>EnsureSchemaOperation</c> is the better choice for a
/// schema that may be there already.
/// </remarks>
public class DmCreateSchemaOperation : MigrationOperation
{
    /// <summary>
    /// The name of the schema to create.
    /// </summary>
    public virtual string Name { get; set; } = string.Empty;

    /// <summary>
    /// The user that owns the schema.
    /// </summary>
    public virtual string UserName { get; set; } = string.Empty;

    /// <inheritdoc />
    public override bool IsDestructiveChange => false;
}
