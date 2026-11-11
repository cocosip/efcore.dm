using System.Reflection;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// Holds the skip ledger to the suite: a case named there has to be a case the suite has.
/// </summary>
/// <remarks>
/// The ledger is the record of what does not pass, and a record that names a case which was renamed or
/// removed reads as coverage the suite does not have. This guard runs without an instance, so it is part
/// of the offline run even though the cases it describes are not.
/// </remarks>
public class SkipLedgerTests
{
    [Fact]
    public void EveryCaseNamedInTheLedgerExistsInTheSuite()
    {
        var named = NamedCases(File.ReadAllLines(LedgerPath));

        Assert.NotEmpty(named);

        var available = CaseNames();
        var unknown = named.Where(name => !available.Contains(name)).ToList();

        Assert.Empty(unknown);
    }

    /// <summary>
    /// Reads the first column of every table row, which is where a case is named.
    /// </summary>
    private static List<string> NamedCases(IEnumerable<string> lines)
        => lines
            .Where(line => line.StartsWith("| `", StringComparison.Ordinal))
            .Select(line => line.Split('|')[1].Trim().Trim('`'))
            .ToList();

    /// <summary>
    /// The cases the fixture bases declare, so a name is checked against what the suite inherited rather
    /// than against what this project wrote. Every test class the project owns is listed here, because a
    /// case in any of them can be recorded in the ledger. A case is either a fact or a theory: the
    /// async forms of a case are data rows of the same method, so the method name is the case name.
    /// </summary>
    private static HashSet<string> CaseNames()
        => new[]
            {
                typeof(BuiltInDataTypesDmTest),
                typeof(StoreGeneratedDmTest),
                typeof(NorthwindWhereQueryDmTest),
                typeof(NorthwindJoinQueryDmTest),
                typeof(NorthwindSelectQueryDmTest)
            }
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Where(
                method => method.CustomAttributes.Any(
                    attribute => attribute.AttributeType.Name.Contains("Fact")
                        || attribute.AttributeType.Name.Contains("Theory")))
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);
    private static string LedgerPath
        => Path.Combine(AppContext.BaseDirectory, "Skipped.md");
}
