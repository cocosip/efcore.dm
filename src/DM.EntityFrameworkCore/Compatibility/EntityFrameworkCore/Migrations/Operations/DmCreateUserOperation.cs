using Microsoft.EntityFrameworkCore.Migrations;

namespace Microsoft.EntityFrameworkCore.Migrations.Operations;

/// <summary>
/// Creates a user and, when a schema is named, a schema it owns.
/// </summary>
/// <remarks>
/// The password is part of the migration, so it ends up in the migration file and in every script
/// generated from it. Granting privileges is not part of this operation on purpose: a migration that
/// grants its own account elevated rights is a decision the application has to make explicitly, in a
/// statement of its own.
/// </remarks>
public class DmCreateUserOperation : MigrationOperation
{
    /// <summary>
    /// The name of the user to create.
    /// </summary>
    public virtual string UserName { get; set; } = string.Empty;

    /// <summary>
    /// The password the user signs in with.
    /// </summary>
    public virtual string Password { get; set; } = string.Empty;

    /// <summary>
    /// The schema the user owns, or <see langword="null" /> to create the user alone.
    /// </summary>
    public virtual string? Schema { get; set; }

    /// <inheritdoc />
    public override bool IsDestructiveChange => false;
}
