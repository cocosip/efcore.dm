using Microsoft.EntityFrameworkCore.Migrations;

namespace Microsoft.EntityFrameworkCore.Migrations.Operations;

/// <summary>
/// Drops a schema and everything in it.
/// </summary>
public class DmDropSchemaOperation : MigrationOperation
{
    /// <summary>
    /// The name of the schema to drop.
    /// </summary>
    public virtual string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public override bool IsDestructiveChange => true;
}
