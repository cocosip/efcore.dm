using DM.EntityFrameworkCore.Infrastructure;
using DM.EntityFrameworkCore.Storage.Internal;
using Microsoft.EntityFrameworkCore.Storage;

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

    /// <summary>
    /// Retries a unit of work when it fails for a reason a retry can recover from.
    /// </summary>
    /// <param name="maxRetryCount">How many times the operation is repeated after the first attempt.</param>
    /// <param name="maxRetryDelay">The longest wait between two attempts; the default is 30 seconds.</param>
    /// <param name="errorNumbersToAdd">
    /// Dameng error numbers to retry in addition to the ones the provider classifies as transient.
    /// </param>
    /// <returns>The same builder, so calls chain.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRetryCount" /> is below zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRetryDelay" /> is not positive.</exception>
    public virtual DmDbContextOptionsBuilder EnableRetryOnFailure(
        int maxRetryCount = 6,
        TimeSpan? maxRetryDelay = null,
        IEnumerable<int>? errorNumbersToAdd = null)
    {
        if (maxRetryCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRetryCount),
                maxRetryCount,
                "The retry count cannot be negative.");
        }

        var delay = maxRetryDelay ?? TimeSpan.FromSeconds(30);

        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRetryDelay),
                delay,
                "The retry delay has to be positive.");
        }

        return ExecutionStrategy(
            dependencies => new DmRetryingExecutionStrategy(dependencies, maxRetryCount, delay, errorNumbersToAdd));
    }
}
