using System.Text.RegularExpressions;

namespace DM.EntityFrameworkCore.Tests.Architecture;

/// <summary>
/// Enforces the namespace rule in the architecture document: a directory declares exactly one
/// namespace, so a path tells you the namespace and no directory mixes the consumer-facing surface
/// with provider internals.
/// </summary>
public class NamespaceLayoutTests
{
    [Fact]
    public void EachSourceDirectory_DeclaresOneNamespace()
    {
        var root = RepositoryRoot();
        var violations = new List<string>();

        foreach (var area in new[] { "src", "tests" })
        {
            var areaPath = Path.Combine(root, area);

            if (!Directory.Exists(areaPath))
            {
                continue;
            }

            var directories = Directory
                .EnumerateFiles(areaPath, "*.cs", SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file))
                .GroupBy(file => Path.GetDirectoryName(file)!);

            foreach (var directory in directories)
            {
                var namespaces = directory
                    .Select(NamespaceOf)
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                if (namespaces.Length > 1)
                {
                    violations.Add($"{Path.GetRelativePath(root, directory.Key)}: {string.Join(", ", namespaces)}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Each directory must declare one namespace. Violations: {string.Join("; ", violations)}");
    }

    [Fact]
    public void CompatibilityDirectories_DeclareTheNamespaceTheirPathNames()
    {
        var root = RepositoryRoot();
        var violations = new List<string>();

        var compatibility = Path.Combine(root, "src", "DM.EntityFrameworkCore", "Compatibility");

        if (!Directory.Exists(compatibility))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(compatibility, "*.cs", SearchOption.AllDirectories))
        {
            var expected = "Microsoft." + Path.GetRelativePath(compatibility, Path.GetDirectoryName(file)!)
                .Replace('\\', '.')
                .Replace('/', '.');

            var actual = NamespaceOf(file);

            if (actual != expected)
            {
                violations.Add($"{Path.GetRelativePath(root, file)}: expected {expected}, found {actual}");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"A compatibility type declares the namespace its path names. Violations: {string.Join("; ", violations)}");
    }

    private static string? NamespaceOf(string file)
        => File
            .ReadLines(file)
            .Select(line => Regex.Match(line, @"^\s*namespace\s+(?<name>[^\s;{]+)"))
            .Where(match => match.Success)
            .Select(match => match.Groups["name"].Value)
            .FirstOrDefault();

    private static bool IsBuildOutput(string directory)
        => directory.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || directory.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DM.EntityFrameworkCore.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root (DM.EntityFrameworkCore.sln) not found.");
    }
}
