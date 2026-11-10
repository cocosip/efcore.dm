using System.Reflection;
using System.Text.RegularExpressions;
using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace DM.EntityFrameworkCore.Tests.Architecture;

/// <summary>
/// Guards the packaging contract: the version the assembly reports and the version the package carries
/// come from one value, and the EF Core dependency keeps the upper bound decision D5 makes mandatory.
/// </summary>
public class PackagingTests
{
    [Fact]
    public void TheAssemblyReportsTheVersionThePackageCarries()
    {
        var version = Property("DmVersion", "Directory.Build.props");
        var assembly = typeof(DmDbContextOptionsExtensions).Assembly;

        Assert.Equal(version, assembly.GetName().Version!.ToString(3));
        Assert.Equal(version + ".0", assembly.GetName().Version!.ToString());
        Assert.StartsWith(version, Informational(assembly), StringComparison.Ordinal);
    }

    /// <summary>
    /// The documentation file is what puts a summary in a consumer's IntelliSense. It sits next to the
    /// assembly, and the pack target copies it into the package beside it, so its absence is a
    /// consumer-visible loss rather than a build detail.
    /// </summary>
    [Fact]
    public void TheAssemblyShipsItsDocumentation()
    {
        var assembly = typeof(DmDbContextOptionsExtensions).Assembly.Location;
        var documentation = Path.ChangeExtension(assembly, ".xml");

        Assert.True(
            File.Exists(documentation),
            $"The assembly at '{assembly}' has no documentation beside it.");
        Assert.Contains(
            "DmDbContextOptionsExtensions",
            File.ReadAllText(documentation),
            StringComparison.Ordinal);
    }
    /// <summary>
    /// An upper bound is what keeps a consumer from loading a provider built against internal API
    /// shapes of a later major version. A range that is open, or that spans more than one major
    /// version, defeats it.
    /// </summary>
    [Fact]
    public void TheEfCoreDependencyCarriesAnUpperBound()
    {
        var range = Property("EfCoreVersionRange", "Directory.Packages.props");
        var match = Regex.Match(
            range,
            @"^\[(?<floor>\d+\.\d+\.\d+),(?<ceiling>\d+\.\d+\.\d+)\)$",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, $"The EF Core range '{range}' is not a bounded range.");

        var floor = Version.Parse(match.Groups["floor"].Value);
        var ceiling = Version.Parse(match.Groups["ceiling"].Value);

        Assert.Equal(floor.Major + 1, ceiling.Major);
        Assert.Equal(0, ceiling.Minor);
        Assert.Equal(0, ceiling.Build);
    }

    private static string Informational(Assembly assembly)
        => assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? string.Empty;

    /// <summary>
    /// Reads a property the build defines. The documents and the build share the repository, so a test
    /// can check that what the build is told and what the package carries are the same value.
    /// </summary>
    private static string Property(string name, string file)
    {
        var match = Regex.Match(
            RepositoryRoot.Read(file),
            $@"<{Regex.Escape(name)}(?:\s[^>]*)?>(?<value>[^<]+)</{Regex.Escape(name)}>",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, $"{name} is not defined in {file}.");

        return match.Groups["value"].Value;
    }
}
