using DM.EntityFrameworkCore.Infrastructure;

namespace Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Dameng-specific options builder. Declared in the namespace the Dameng provider declares it in,
/// so that consumer source that names the type compiles unchanged.
/// </summary>
/// <remarks>
/// Deriving from <see cref="RelationalDbContextOptionsBuilder{TBuilder, TExtension}" /> is what puts the
/// relational settings within reach of a <c>UseDm</c> call: command timeout, max batch size, migrations
/// assembly, the collection translation mode, the execution strategy and the rest. A plain class here
/// would compile consumer code that sets none of them.
/// </remarks>
public class DmDbContextOptionsBuilder
    : RelationalDbContextOptionsBuilder<DmDbContextOptionsBuilder, DmOptionsExtension>
{
    /// <summary>
    /// Creates the builder that a <c>UseDm</c> call hands to its options action.
    /// </summary>
    /// <param name="optionsBuilder">The builder the extension is being added to.</param>
    public DmDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
        : base(optionsBuilder)
    {
    }
}
