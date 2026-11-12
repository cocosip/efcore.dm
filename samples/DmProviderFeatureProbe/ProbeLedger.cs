namespace DmProviderFeatureProbe;

/// <summary>
/// A step that the server cannot pass, with the measurement that decided it. The vocabulary and the
/// citation rule are those of <c>docs/testing.md</c> §3; an entry is added only with the error the
/// server answered, never to make a red run green.
/// </summary>
internal sealed record LedgerEntry(string Step, string Category, string Reason, string Citation);

internal static class ProbeLedger
{
    private static readonly LedgerEntry[] Entries =
    [
        new(
            "model: primitive collection predicate",
            "Not implemented",
            "Containment on a primitive collection is not translated: the provider answers "
            + "'could not be translated' for DbSet<Tagged>().Count(row => row.Tags.Contains(\"alpha\")).",
            "probe measurement 2026-11-12 against DM 8.1.4.80; capability matrix row and issue pending"),
        new(
            "update: query filter under ExecuteUpdate",
            "Known defect",
            "The filter predicate reaches the update pipeline unrewritten: the server answers "
            + "'查询使用值表达式作为过滤条件' (a value expression used as a filter condition) for the "
            + "boolean the query filter emits.",
            "probe measurement 2026-11-12 against DM 8.1.4.80; the query pipeline rewrites the same "
            + "boolean, the update pipeline does not; issue pending"),
    ];

    public static bool IsSkipped(string step, out string reason)
    {
        var entry = Entries.FirstOrDefault(candidate => candidate.Step == step);

        reason = entry is null ? string.Empty : $"{entry.Category}: {entry.Reason} [{entry.Citation}]";

        return entry is not null;
    }
}
