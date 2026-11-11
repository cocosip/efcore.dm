using global::Dm;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Decides whether a failure is one a retry can recover from, from the number the Dameng provider reports.
/// </summary>
/// <remarks>
/// The set is measured rather than taken from a manual: the connection strings of the live runs, the two
/// compatibility modes and the server version they ran against are in <c>docs/testing.md</c>.
/// <list type="bullet">
/// <item><description>
/// <c>-608</c> — 请求执行超时, the statement timeout the driver raises while a lock holds the row. Measured
/// by updating a row a second connection kept locked with a one second command timeout.
/// </description></item>
/// <item><description>
/// <c>6027</c> — 未接收到网络数据, the connection is gone. Measured by closing the server side session of a
/// connection that was open, which leaves the driver reporting <c>Open</c>: only <c>Close</c> then
/// <c>Open</c> answers, which is why <see cref="DmRetryingExecutionStrategy" /> closes the connection
/// before a retry.
/// </description></item>
/// </list>
/// A deadlock is not in the set: with the shipped configuration a deadlock was not detected within five
/// minutes, so there is no number to name. A constraint violation (<c>-6602</c>), a syntax error
/// (<c>-2007</c>) and a missing table (<c>-2106</c>) are permanent: the same statement fails again.
/// </remarks>
internal static class DmTransientExceptionDetector
{
    /// <summary>The failures that are retried whether the consumer names them or not.</summary>
    public static IReadOnlyCollection<int> ErrorNumbers { get; } = [-608, 6027];

    /// <summary>
    /// Whether <paramref name="exception" />, or one of the exceptions it wraps, is one of the failures a
    /// retry recovers from.
    /// </summary>
    public static bool ShouldRetryOn(Exception exception, IReadOnlyCollection<int>? errorNumbersToAdd = null)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DmException { Number: var number }
                && (ErrorNumbers.Contains(number) || (errorNumbersToAdd?.Contains(number) ?? false)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="exception" /> describes a connection that has to be replaced rather than a
    /// statement that can be repeated on the connection it used.
    /// </summary>
    public static bool IsConnectionFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            // 6027 is the measured number for a connection that received nothing; the 6000 range is where
            // the driver puts the failures it detects before or while the transport is used.
            if (current is DmException { Number: var number } && number is >= 6000 and < 7000)
            {
                return true;
            }
        }

        return false;
    }
}
