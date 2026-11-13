namespace DM.EntityFrameworkCore.Tests.Dialect;

/// <summary>
/// Enforces architecture decision D1: the dialect layer carries no EF Core dependency,
/// which is what makes it shareable across branches.
/// </summary>
public class DialectLayerIsolationTests
{
    private const string Forbidden = "Microsoft.EntityFrameworkCore";

    [Fact]
    public void DialectSourceFiles_DoNotDependOnEfCore()
    {
        var offenders = Directory
            .EnumerateFiles(FindDialectDirectory(), "*.cs", SearchOption.AllDirectories)
            .Where(file => CodeLines(File.ReadAllLines(file)).Any(line => line.Contains(Forbidden, StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"The dialect layer must not depend on EF Core. Offending files: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void DialectDirectory_ContainsSource()
    {
        Assert.NotEmpty(Directory.EnumerateFiles(FindDialectDirectory(), "*.cs", SearchOption.AllDirectories));
    }

    /// <summary>
    /// Drops comments, since a comment creates no dependency. Block comments are tracked so
    /// that a commented-out reference does not trip the guard.
    /// </summary>
    private static IEnumerable<string> CodeLines(IEnumerable<string> lines)
    {
        var inBlockComment = false;

        foreach (var line in lines)
        {
            var code = new System.Text.StringBuilder();
            var index = 0;

            while (index < line.Length)
            {
                if (inBlockComment)
                {
                    var end = line.IndexOf("*/", index, StringComparison.Ordinal);

                    if (end < 0)
                    {
                        break;
                    }

                    index = end + 2;
                    inBlockComment = false;
                    continue;
                }

                var lineComment = line.IndexOf("//", index, StringComparison.Ordinal);
                var blockComment = line.IndexOf("/*", index, StringComparison.Ordinal);

                if (lineComment < 0 && blockComment < 0)
                {
                    code.Append(line, index, line.Length - index);
                    break;
                }

                if (blockComment < 0 || (lineComment >= 0 && lineComment < blockComment))
                {
                    code.Append(line, index, lineComment - index);
                    break;
                }

                code.Append(line, index, blockComment - index);
                index = blockComment + 2;
                inBlockComment = true;
            }

            var trimmed = code.ToString().Trim();

            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    private static string FindDialectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DM.EntityFrameworkCore.sln")))
            {
                var dialect = Path.Combine(
                    directory.FullName,
                    "src",
                    "DM.EntityFrameworkCore",
                    "Dialect");

                Assert.True(Directory.Exists(dialect), $"Dialect directory not found at {dialect}.");

                return dialect;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root (DM.EntityFrameworkCore.sln) not found.");
    }
}
