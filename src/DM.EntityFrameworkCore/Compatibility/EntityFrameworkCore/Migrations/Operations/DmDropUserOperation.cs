using Microsoft.EntityFrameworkCore.Migrations;

namespace Microsoft.EntityFrameworkCore.Migrations.Operations;

/// <summary>
/// Drops a user and the objects it owns.
/// </summary>
public class DmDropUserOperation : MigrationOperation
{
    /// <summary>
    /// The name of the user to drop.
    /// </summary>
    public virtual string UserName { get; set; } = string.Empty;

    /// <inheritdoc />
    public override bool IsDestructiveChange => true;
}
