using System.Text.RegularExpressions;
using Dm;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

/// <summary>
/// The store the specification suite runs against: a Dameng database that already exists.
/// </summary>
/// <remarks>
/// Dameng creates a database with the <c>dminit</c> tool before a connection can reach it, so this store
/// cannot create one and does not try. What it owns is a schema of its own: every table, sequence and view
/// in it is dropped and the model's tables are created again for every fixture, which is what a
/// specification test expects of a store it shares.
/// <para>
/// Each fixture gets a schema named after it, so two classes running at the same time never see each
/// other's tables; nothing outside that schema is touched.</para>
/// <para>
/// The migrations suite creates the tables of the migration it applies rather than the tables of the
/// context model, so it is handed the named schemas its cases use
/// (<see cref="MigrationsSchemas" />) and drops every object in them.</para>
/// </remarks>
public sealed class DmTestStore : RelationalTestStore
{
    public const int CommandTimeout = 300;

    public const string MigrationsSchema = "DM_SPEC_MIGRATIONS";

    /// <summary>
    /// The schemas the migrations suite owns: its own, the default one, and the named ones the cases
    /// create tables in.
    /// </summary>
    internal static readonly string[] MigrationsSchemas =
        [MigrationsSchema, "dbo2", "SomeOtherSchema", "TestTableSchema"];

    private readonly string[] _schemas;

    private DmTestStore(string name, bool shared, string[] schemas)
        : base(name, shared, new DmConnection(WithSchema(DmTestConnection.ConnectionString, schemas[0])))
        => _schemas = schemas;

    public static DmTestStore Create(string name) => new(name, shared: false, [StoreSchema(name)]);

    public static DmTestStore GetOrCreate(string name) => new(name, shared: true, [StoreSchema(name)]);

    public static DmTestStore CreateForMigrations(string name)
        => new(name, shared: false, MigrationsSchemas);

    /// <summary>
    /// The schema a fixture owns. Each fixture gets one of its own in the shared database, so two classes
    /// running at the same time never drop or create each other's objects.
    /// </summary>
    private static string StoreSchema(string name)
    {
        var cleaned = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        return "DM_SPEC_" + (cleaned.Length > 100 ? cleaned[..100] : cleaned);
    }

    private static string WithSchema(string connectionString, string schema)
        => Regex.Replace(
            connectionString,
            @"(^|;)\s*schema\s*=[^;]*",
            $"$1schema={schema}",
            RegexOptions.IgnoreCase);

    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => builder.UseDm(Connection, options => options.CommandTimeout(CommandTimeout));

    protected override async Task InitializeAsync(
        Func<DbContext> createContext,
        Func<DbContext, Task>? seed,
        Func<DbContext, Task>? clean)
    {
        await using var context = createContext();

        await CleanAsync(context);

        if (clean is not null)
        {
            await clean(context);
        }

        if (seed is not null)
        {
            await seed(context);
        }
    }

    public override async Task CleanAsync(DbContext context)
    {
        await CleanOwnSchemasAsync();

        // EnsureCreated is not used: it answers "does this schema have any table", and the database the
        // suite runs against is one an application already uses, so it would decide there is nothing to do.
        // Creating the tables of the model directly is what the store needs.
        var creator = context.GetService<IRelationalDatabaseCreator>();

        await creator.CreateTablesAsync();
    }

    /// <summary>
    /// Empties the schema this fixture owns. The migrations suite creates the tables of the migration it
    /// applies rather than the tables of the context model, so the model cannot name what has to go; the
    /// other fixtures have a schema of their own for the same reason, which keeps their clean independent
    /// of what another class is doing.
    /// </summary>
    private async Task CleanOwnSchemasAsync()
    {
        await EnsureOwnSchemasAsync();

        await using var connection = new DmConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var schema in _schemas)
        {
            foreach (var view in await NamesAsync(
                         connection,
                         $"SELECT VIEW_NAME FROM ALL_VIEWS WHERE OWNER = '{schema}'"))
            {
                await TryExecuteAsync(connection, $"DROP VIEW \"{schema}\".\"{view}\"");
            }

            foreach (var sequence in await NamesAsync(
                         connection,
                         $"SELECT SEQUENCE_NAME FROM ALL_SEQUENCES WHERE SEQUENCE_OWNER = '{schema}'"))
            {
                await TryExecuteAsync(connection, $"DROP SEQUENCE \"{schema}\".\"{sequence}\"");
            }

            foreach (var table in await NamesAsync(
                         connection,
                         $"SELECT TABLE_NAME FROM ALL_TABLES WHERE OWNER = '{schema}'"))
            {
                await TryExecuteAsync(connection, $"DROP TABLE \"{schema}\".\"{table}\" CASCADE CONSTRAINTS");
            }
        }
    }

    /// <summary>
    /// Creates the schemas this store owns with a connection that does not name one, because a connection
    /// cannot open on a schema that does not exist yet.
    /// </summary>
    private async Task EnsureOwnSchemasAsync()
    {
        await using var connection = new DmConnection(DmTestConnection.ConnectionString);
        await connection.OpenAsync();

        foreach (var schema in _schemas)
        {
            await TryExecuteAsync(connection, $"CREATE SCHEMA \"{schema}\"");
        }
    }

    private static async Task<List<string>> NamesAsync(DmConnection connection, string sql)
    {
        var names = new List<string>();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeout;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static async Task TryExecuteAsync(DmConnection connection, string sql)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = CommandTimeout;

            await command.ExecuteNonQueryAsync();
        }
        catch (DmException)
        {
            // The object was not there, which is the state a first run starts from.
        }
    }
}



