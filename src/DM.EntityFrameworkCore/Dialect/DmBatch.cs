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
    /// generated scripts and their tests are identical on Windows and Linux. The line feed after the
    /// slash is part of the terminator: the script writer appends the next statement directly after it,
    /// so a terminator without one glues the two together and leaves a line that reads <c>/START</c>.
    /// </remarks>
    public const string Terminator = "/\n";
}
