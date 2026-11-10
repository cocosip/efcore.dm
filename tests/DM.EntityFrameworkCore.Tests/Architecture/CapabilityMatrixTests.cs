using System.Text.RegularExpressions;

namespace DM.EntityFrameworkCore.Tests.Architecture;

/// <summary>
/// Guards the capability matrix against drifting from itself: every status count in the summary has
/// to match the rows, the rows have to add up to the declared total, and an implemented capability
/// has to name what proves it, which is the matrix's own rule.
/// </summary>
public class CapabilityMatrixTests
{
    private static readonly string[] Statuses =
        ["Implemented", "Partial", "Not started", "Blocked", "Deferred", "Not applicable"];

    [Fact]
    public void TheSummaryCountsMatchTheRows()
    {
        var lines = MatrixLines();
        var counts = CountStatuses(DataRows(lines));

        foreach (var status in Statuses)
        {
            Assert.Equal(counts.GetValueOrDefault(status), SummaryCount(lines, status));
        }
    }

    [Fact]
    public void TheRowsAddUpToTheDeclaredTotal()
    {
        var lines = MatrixLines();
        var rows = DataRows(lines).Count();
        var summarized = Statuses.Aggregate(0, (int total, string status) => total + SummaryCount(lines, status));

        Assert.Equal(rows, summarized);
        Assert.Equal(rows, SummaryTotal(lines));
    }

    [Fact]
    public void AnImplementedCapabilityNamesWhatProvesIt()
    {
        foreach (var row in DataRows(MatrixLines()))
        {
            var cells = RowCells(row);

            if (cells[1] != "`Implemented`")
            {
                continue;
            }

            Assert.False(
                string.IsNullOrWhiteSpace(cells[2]) || cells[2] == "—",
                $"{cells[0]} is implemented but names no test");
        }
    }

    /// <summary>
    /// The rows of the matrix proper, which are the table rows whose first cell is the capability and
    /// whose second cell is the status. The legend and the summary have the status first.
    /// </summary>
    private static IEnumerable<string> DataRows(string[] lines)
        => lines
            .TakeWhile(line => !line.StartsWith("## 6.", StringComparison.Ordinal))
            .Where(line => line.StartsWith('|'))
            .Where(line => RowCells(line).Length >= 5)
            .Where(line => Statuses.Contains(RowCells(line)[1].Trim('`')));

    private static Dictionary<string, int> CountStatuses(IEnumerable<string> rows)
    {
        var counts = new Dictionary<string, int>();

        foreach (var row in rows)
        {
            var status = RowCells(row)[1].Trim('`');
            counts[status] = counts.GetValueOrDefault(status) + 1;
        }

        return counts;
    }

    private static string[] RowCells(string row)
        => row.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

    private static int SummaryCount(string[] lines, string status)
    {
        var line = SummaryLines(lines).SingleOrDefault(
            candidate => Regex.IsMatch(candidate, $@"^\|\s*`{Regex.Escape(status)}`\s*\|\s*\d+\s*\|$"));

        Assert.NotNull(line);

        return int.Parse(Regex.Match(line, @"\d+").Value);
    }

    private static int SummaryTotal(string[] lines)
    {
        var line = SummaryLines(lines).SingleOrDefault(candidate => candidate.Contains("**Total rows**", StringComparison.Ordinal));

        Assert.NotNull(line);

        return int.Parse(Regex.Match(line, @"\*\*(\d+)\*\*").Groups[1].Value);
    }

    private static IEnumerable<string> SummaryLines(string[] lines)
        => lines.SkipWhile(line => !line.StartsWith("## 6.", StringComparison.Ordinal));

    private static string[] MatrixLines()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docs", "capability-matrix.md")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return File.ReadAllLines(Path.Combine(directory.FullName, "docs", "capability-matrix.md"));
    }
}
