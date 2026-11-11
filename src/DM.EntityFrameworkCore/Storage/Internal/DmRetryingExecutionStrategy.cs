using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// An execution strategy that retries the failures <see cref="DmTransientExceptionDetector" /> recognises.
/// </summary>
/// <remarks>
/// Installed by <c>EnableRetryOnFailure</c>, which is the only way a consumer reaches it: without one the
/// provider keeps the framework's non-retrying strategy, because a retry replays a unit of work whose
/// first attempt may already have changed the database.
/// </remarks>
internal sealed class DmRetryingExecutionStrategy : ExecutionStrategy
{
    private readonly IReadOnlyCollection<int>? _errorNumbersToAdd;

    /// <summary>
    /// Creates the strategy a consumer asked for, with the retry count and delay it named.
    /// </summary>
    public DmRetryingExecutionStrategy(
        ExecutionStrategyDependencies dependencies,
        int maxRetryCount,
        TimeSpan maxRetryDelay,
        IEnumerable<int>? errorNumbersToAdd)
        : base(dependencies, maxRetryCount, maxRetryDelay)
    {
        _errorNumbersToAdd = errorNumbersToAdd?.Distinct().ToArray();
    }

    /// <summary>
    /// Classifies the failure by the number the provider reports, so a constraint violation, a syntax
    /// error and a missing table are thrown to the caller on the first attempt.
    /// </summary>
    protected override bool ShouldRetryOn(Exception exception)
        => DmTransientExceptionDetector.ShouldRetryOn(exception, _errorNumbersToAdd);

    /// <summary>
    /// Replaces a connection the failure broke before the statement is repeated on it.
    /// </summary>
    /// <remarks>
    /// Measured: the driver leaves a connection whose session the server closed reporting
    /// <see cref="ConnectionState.Open" />, and a statement on it fails again with the same number, while
    /// closing it and opening it answers. A failed statement that is not a connection failure keeps its
    /// connection, and a committed transaction keeps its own.
    /// </remarks>
    protected override void OnRetry()
    {
        if (ExceptionsEncountered[^1] is not { } exception || !DmTransientExceptionDetector.IsConnectionFailure(exception))
        {
            return;
        }

        var context = Dependencies.CurrentContext.Context;
        context.Database.CloseConnection();

        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Closed)
        {
            // Nothing EF opened, so the count the facade keeps did not close the transport itself.
            connection.Close();
        }
    }
}
