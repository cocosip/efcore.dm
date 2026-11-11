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
/// cannot create one and does not try. What it owns is the schema: the tables and sequences of the model
/// it is handed, dropped with their constraints and created again for every fixture, which is what a
/// specification test expects of a store it shares.
/// <para>
/// A table the store drops is one the model declares. It never drops a table by name from the catalog, so
/// a database that holds objects the suite does not own keeps them.
/// </para>
/// </remarks>
public sealed class DmTestStore : RelationalTestStore
{
    public const int CommandTimeout = 300;

    private DmTestStore(string name, bool shared)
        : base(name, shared, new DmConnection(DmTestConnection.ConnectionString))
    {
    }

    public static DmTestStore Create(string name) => new(name, shared: false);

    public static DmTestStore GetOrCreate(string name) => new(name, shared: true);

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
        // The drops run on a connection of their own: the context manages the state of the one it was
        // built with, and a command issued on it while it is closed fails.
        await using (var connection = new DmConnection(ConnectionString))
        {
            await connection.OpenAsync();

            await DropSequencesAsync(connection, context);
            await DropTablesAsync(connection, context);
        }

        // EnsureCreated is not used: it answers "does this schema have any table", and the database the
        // suite runs against is one an application already uses, so it would decide there is nothing to do.
        // Creating the tables of the model directly is what the store needs.
        var creator = context.GetService<IRelationalDatabaseCreator>();

        await creator.CreateTablesAsync();
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
