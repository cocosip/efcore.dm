using System.Text.Json;

namespace DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

/// <summary>
/// Where the specification suite finds the database it runs against.
/// </summary>
/// <remarks>
/// The suite creates and drops the tables its own models declare, so it needs a database it is allowed
/// to write to, and it needs one before a single test can run. The connection string is read from
/// <c>DM_SPEC_CONNECTION</c>, or from the user secrets of the acceptance sample, which is where the
/// repository keeps the only copy of a credential. When neither is present the suite reports what to
/// set instead of failing with a driver error.
/// </remarks>
internal static class DmTestConnection
{
    private const string Variable = "DM_SPEC_CONNECTION";
    private const string SecretsId = "dm-entityframeworkcore-validation";
    private const string SecretKey = "ConnectionStrings:Dm";

    private static readonly Lazy<string> LazyConnectionString = new(Resolve);

    private static readonly Lazy<string> LazyFullConnectionString = new(ResolveFull);

    public static string ConnectionString => LazyConnectionString.Value;

    /// <summary>
    /// The default-compatibility instance. The primitive-collection fixture needs it because its seed
    /// writes an empty string into a required column, and Oracle-compatible mode stores an empty string
    /// as null, which the column refuses.
    /// </summary>
    public static string FullConnectionString => LazyFullConnectionString.Value;

    private static string ResolveFull()
    {
        if (Environment.GetEnvironmentVariable("DM_SPEC_CONNECTION_FULL") is { Length: > 0 } configured)
        {
            return configured;
        }

        try
        {
            return Resolve("ConnectionStrings:DmFull");
        }
        catch (InvalidOperationException)
        {
            return ConnectionString;
        }
    }

    private static string Resolve()
        => Resolve(SecretKey);

    private static string Resolve(string key)
    {
        if (key == SecretKey
            && Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } configured)
        {
            return configured;
        }

        var file = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets",
            SecretsId,
            "secrets.json");

        if (!File.Exists(file))
        {
            throw new InvalidOperationException(
                $"The specification suite needs a Dameng instance. Set {Variable}, or run: "
                + $"dotnet user-secrets set \"{SecretKey}\" \"<connection string>\" --project samples/DmProviderValidation");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(file));

        if (document.RootElement.TryGetProperty(key, out var value) && value.GetString() is { Length: > 0 } secret)
        {
            return secret;
        }

        throw new InvalidOperationException($"The user secrets do not hold {key}");
    }
}
