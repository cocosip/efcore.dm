using System.Text.RegularExpressions;
using DM.EntityFrameworkCore.Tests.TestUtilities;

namespace DM.EntityFrameworkCore.Tests.Architecture;

/// <summary>
/// Guards the documents against drifting apart from each other and from the repository: a relative
/// link that no longer resolves, a document the index does not mention, a decision a document cites
/// but the architecture record does not define, and a roadmap phase the matrix cites but the plan does
/// not define. The documents are deliverables, so a reference to one that is gone is a defect.
/// </summary>
public class DocumentationTests
{
    private static readonly string[] HttpPrefixes = ["http://", "https://", "mailto:", "#"];

    [Fact]
    public void EveryRelativeLinkResolves()
    {
        foreach (var document in Documents())
        {
            foreach (Match link in Regex.Matches(document.Text, @"\[[^\]]*\]\(([^)\s]+)\)", RegexOptions.CultureInvariant))
            {
                var target = link.Groups[1].Value;

                if (HttpPrefixes.Any(prefix => target.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    continue;
                }

                var path = target.Split('#')[0];

                if (path.Length == 0)
                {
                    continue;
                }

                Assert.True(
                    Exists(document.Path, path),
                    $"{document.Name} links to '{target}', which does not exist.");
            }
        }
    }

    [Fact]
    public void EveryDocumentIsIndexed()
    {
        var index = RepositoryRoot.Read("docs", "README.md");

        foreach (var document in Documents().Where(document => document.Name != "README.md"))
        {
            var link = document.Path.StartsWith(Path.Combine("docs"), StringComparison.Ordinal)
                ? document.Name
                : Path.Combine("docs", document.Name).Replace('\\', '/');

            Assert.Contains($"({link})", index, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryCitedDecisionIsDefined()
    {
        var defined = Regex
            .Matches(RepositoryRoot.Read("docs", "architecture.md"), @"(?m)^\| (D\d+) \|", RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(defined);

        foreach (var document in Documents())
        {
            foreach (Match citation in Regex.Matches(document.Text, @"\bD\d+\b", RegexOptions.CultureInvariant))
            {
                Assert.True(
                    defined.Contains(citation.Value),
                    $"{document.Name} cites decision {citation.Value}, which the architecture record does not define.");
            }
        }
    }

    [Fact]
    public void EveryCitedPhaseIsDefined()
    {
        var roadmap = RepositoryRoot.Read("docs", "roadmap.md");

        var defined = Regex
            .Matches(roadmap, @"(?m)^(?:### |\| )(P\d+[ab]?)\b", RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(defined);

        foreach (var document in Documents())
        {
            foreach (Match citation in Regex.Matches(document.Text, @"\bP\d+[ab]?\b", RegexOptions.CultureInvariant))
            {
                Assert.True(
                    defined.Contains(citation.Value),
                    $"{document.Name} cites phase {citation.Value}, which the roadmap does not define.");
            }
        }
    }

    private static bool Exists(string documentPath, string target)
    {
        var directory = Path.GetDirectoryName(documentPath) ?? string.Empty;
        var resolved = Path.GetFullPath(Path.Combine(RepositoryRoot.Find(), directory, target));

        return File.Exists(resolved) || Directory.Exists(resolved);
    }

    private static IEnumerable<(string Path, string Name, string Text)> Documents()
    {
        var root = RepositoryRoot.Find();
        var documents = new List<(string Path, string Name, string Text)>
        {
            ("README.md", "README.md", RepositoryRoot.Read("README.md"))
        };

        foreach (var path in Directory.GetFiles(Path.Combine(root, "docs"), "*.md").Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, path);

            documents.Add((relative, Path.GetFileName(path), File.ReadAllText(path)));
        }

        return documents;
    }
}
