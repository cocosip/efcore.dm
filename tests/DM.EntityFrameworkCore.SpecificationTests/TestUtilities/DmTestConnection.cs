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

    public static string ConnectionString => LazyConnectionString.Value;

    private static string Resolve()
    {
        if (Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } configured)
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

        if (document.RootElement.TryGetProperty(SecretKey, out var value) && value.GetString() is { Length: > 0 } secret)
        {
            return secret;
        }

        throw new InvalidOperationException($"The user secrets do not hold {SecretKey}");
    }
}
