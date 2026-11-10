using System.Text.RegularExpressions;
using DM.EntityFrameworkCore.Tests.TestUtilities;

namespace DM.EntityFrameworkCore.Tests.Architecture;

/// <summary>
/// The repository layout in <c>docs/architecture.md</c> claims that everything it lists exists. These
/// tests hold it to that, and hold the provider's own directory list complete, so a directory that
/// appears or disappears is reflected in the document in the same change.
/// </summary>
public class RepositoryLayoutTests
{
    [Fact]
    public void EveryDocumentedPathExists()
    {
        var root = RepositoryRoot.Find();
        var documented = DocumentedPaths(RepositoryRoot.Read("docs", "architecture.md"));

        Assert.NotEmpty(documented);

        foreach (var path in documented)
        {
            var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(
                Directory.Exists(full) || File.Exists(full),
                $"docs/architecture.md lists {path}, which does not exist");
        }
    }

    [Fact]
    public void EveryProviderDirectoryAppearsInTheLayout()
    {
        var layout = RepositoryRoot.Read("docs", "architecture.md");
        var block = LayoutBlock(layout);

        foreach (var directory in RepositoryRoot.Directories("src", "DM.EntityFrameworkCore"))
        {
            Assert.True(
                block.Contains(directory + "/", StringComparison.Ordinal),
                $"src/DM.EntityFrameworkCore/{directory} is not in the repository layout");
        }
    }

    private static string LayoutBlock(string document)
    {
        var lines = document.Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith("## 8.", StringComparison.Ordinal));

        Assert.True(start >= 0, "docs/architecture.md has no repository layout section");

        var fence = Array.FindIndex(lines, start, line => line.Trim() == "```");
        var end = Array.FindIndex(lines, fence + 1, line => line.Trim() == "```");

        Assert.True(fence >= 0 && end > fence, "the repository layout section has no fenced block");

        return string.Join('\n', lines[(fence + 1)..end]);
    }

    private static IEnumerable<string> DocumentedPaths(string document)
        => LayoutBlock(document)
            .Split('\n')
            .Where(line => !line.Contains("(planned)", StringComparison.Ordinal))
            .Select(line => Regex.Match(line.Trim(), @"^(?<path>[A-Za-z0-9_.-]+/[^\s]+|[A-Za-z0-9_.-]+\.(slnx|props))$"))
            .Where(match => match.Success)
            .Select(match => match.Groups["path"].Value);
}
