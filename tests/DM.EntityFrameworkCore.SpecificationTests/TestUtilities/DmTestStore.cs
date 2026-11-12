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
/// cannot create one and does not try. What it owns is the schema: the tables, sequences and views of the model
/// it is handed, dropped with their constraints and created again for every fixture, which is what a
/// specification test expects of a store it shares.
/// <para>
/// A table the store drops is one the model declares. It never drops a table by name from the catalog, so
/// a database that holds objects the suite does not own keeps them.
/// </para>
/// <para>
/// The migrations suite is the exception: it creates the tables of the migration it applies rather than
/// the tables of the context model, so the store is handed a schema of its own
/// (<see cref="MigrationsSchema" />) and drops every object in that schema. Nothing outside it is touched.
/// </para>
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

    private readonly bool _ownsWholeSchema;

    private DmTestStore(string name, bool shared, string? connectionString = null, bool ownsWholeSchema = false)
        : base(name, shared, new DmConnection(connectionString ?? DmTestConnection.ConnectionString))
        => _ownsWholeSchema = ownsWholeSchema;

    public static DmTestStore Create(string name) => new(name, shared: false);

    public static DmTestStore GetOrCreate(string name) => new(name, shared: true);

    public static DmTestStore CreateForMigrations(string name)
        => new(
            name,
            shared: false,
            WithSchema(DmTestConnection.ConnectionString, MigrationsSchema),
            ownsWholeSchema: true);

    private static string WithSchema(string connectionString, string schema)
        => Regex.Replace(
            connectionString,
            @"(^|;)\s*schema\s*=[^;]*",
            $"$1schema={schema}",
            RegexOptions.IgnoreCase);

    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => builder.UseDm(Connection);

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
        if (_ownsWholeSchema)
        {
            await CleanOwnSchemaAsync();
        }
        else
        {
            // The drops run on a connection of their own: the context manages the state of the one it was
            // built with, and a command issued on it while it is closed fails.
            await using var connection = new DmConnection(ConnectionString);
            await connection.OpenAsync();

            await DropViewsAsync(connection, context);
            await DropSequencesAsync(connection, context);
            await DropTablesAsync(connection, context);
        }

        // EnsureCreated is not used: it answers "does this schema have any table", and the database the
        // suite runs against is one an application already uses, so it would decide there is nothing to do.
        // Creating the tables of the model directly is what the store needs.
        var creator = context.GetService<IRelationalDatabaseCreator>();

        await creator.CreateTablesAsync();
    }

    /// <summary>
    /// Empties the migrations schema: the suite creates the tables of the migration it applies rather than
    /// the tables of the context model, so the model cannot name what has to go.
    /// </summary>
    private async Task CleanOwnSchemaAsync()
    {
        await EnsureMigrationsSchemaAsync();

        await using var connection = new DmConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var schema in MigrationsSchemas)
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
    /// Creates the schemas of the migrations suite with a connection that does not name one, because a
    /// connection cannot open on a schema that does not exist yet.
    /// </summary>
    private static async Task EnsureMigrationsSchemaAsync()
    {
        await using var connection = new DmConnection(DmTestConnection.ConnectionString);
        await connection.OpenAsync();

        foreach (var schema in MigrationsSchemas)
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

    private static async Task DropViewsAsync(DmConnection connection, DbContext context)
    {
        // A view outlives a table the model maps, and the suite creates the view of its model itself, so
        // the second run against the same database would fail with 对象[ProductView]已存在 without this.
        var views = context.Model
            .GetEntityTypes()
            .Select(entityType => entityType.GetViewName())
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var view in views)
        {
            await TryExecuteAsync(connection, $"DROP VIEW \"{view}\"");
        }
    }

    private static async Task DropTablesAsync(DmConnection connection, DbContext context)
    {
        var tables = context.Model
            .GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            // CASCADE CONSTRAINTS drops the foreign keys that point at the table, not the tables that
            // hold them, so every table is dropped rather than only the roots.
            await TryExecuteAsync(connection, $"DROP TABLE \"{table}\" CASCADE CONSTRAINTS");
        }
    }

    private static async Task DropSequencesAsync(DmConnection connection, DbContext context)
    {
        foreach (var sequence in context.Model.GetSequences())
        {
            await TryExecuteAsync(connection, $"DROP SEQUENCE \"{sequence.Name}\"");
        }
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



