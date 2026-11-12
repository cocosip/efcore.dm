namespace DmProviderFeatureProbe;

/// <summary>
/// A step that the server cannot pass, with the measurement that decided it. The vocabulary and the
/// citation rule are those of <c>docs/testing.md</c> §3; an entry is added only with the error the
/// server answered, never to make a red run green.
/// </summary>
internal sealed record LedgerEntry(string Step, string Category, string Reason, string Citation);

internal static class ProbeLedger
{
    private static readonly LedgerEntry[] Entries = [];

    public static bool IsSkipped(string step, out string reason)
    {
        var entry = Entries.FirstOrDefault(candidate => candidate.Step == step);

        reason = entry is null ? string.Empty : $"{entry.Category}: {entry.Reason} [{entry.Citation}]";

        return entry is not null;
    }
}
