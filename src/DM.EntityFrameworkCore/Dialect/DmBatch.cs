namespace DM.EntityFrameworkCore.Dialect;

/// <summary>
/// Script batching rules for Dameng.
/// </summary>
internal static class DmBatch
{
    /// <summary>
    /// Dameng terminates a batch with a forward slash on its own line.
    /// </summary>
    /// <remarks>
    /// Written as an explicit line feed rather than <see cref="Environment.NewLine"/> so that
    /// generated scripts and their tests are identical on Windows and Linux.
    /// </remarks>
    public const string Terminator = "\n/";
}
