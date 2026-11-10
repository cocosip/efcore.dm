namespace DM.EntityFrameworkCore.Tests.TestUtilities;

/// <summary>
/// Locates the repository from the test output directory, so a test can read the documents the
/// repository ships.
/// </summary>
internal static class RepositoryRoot
{
    public static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DM.EntityFrameworkCore.slnx")))
        {
            directory = directory.Parent;
        }

        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }

    public static string Read(params string[] parts)
        => File.ReadAllText(Path.Combine([Find(), .. parts]));

    public static IEnumerable<string> Directories(params string[] parts)
        => Directory
            .GetDirectories(Path.Combine([Find(), .. parts]))
            .Select(path => Path.GetFileName(path))
            .Where(name => name is not "bin" and not "obj")
            .Order();
}
