using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace DmProviderValidation;

/// <summary>
/// End-to-end acceptance program for the provider (docs/testing.md §5).
/// </summary>
/// <remarks>
/// Nothing here proves a translation; it executes one. Every step reports separately, a failing step
/// does not stop the run, and the exit code is non-zero when any step failed. The connection string
/// is read from the first argument, or from <c>ConnectionStrings__Dm</c>, and is never printed.
/// <para>
/// The run creates its own tables rather than calling <c>EnsureCreated</c>: a Dameng database is made
/// with the <c>dminit</c> tool before a connection can reach it, so the provider cannot create one,
/// and the catalog the tables live in may already hold tables this run does not own.
/// </para>
/// </remarks>
internal static class Program
{
    private static readonly string[] Tables =
    [
        "DM_VALIDATION_CHILD",
        "DM_VALIDATION_PARENT",
        "DM_VALIDATION_RECORD"
    ];

    private static async Task<int> Main(string[] args)
    {
        if (args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase))
        {
            return DryRun();
        }

        var connectionString = Connection(args);

        if (connectionString is null)
        {
            Console.Error.WriteLine(
                "No connection string. Pass it as the first argument, set ConnectionStrings__Dm, "
                + "or set the \"ConnectionStrings:Dm\" user secret.");

            return 2;
        }

        var keep = args.Contains("--keep", StringComparer.OrdinalIgnoreCase);
        var cancellation = CancellationToken.None;
        var failures = 0;
        var steps = Steps.All;

        Console.WriteLine("Dameng provider acceptance");
        Console.WriteLine();

        await using var context = new ValidationContext(connectionString);

        try
        {
            await DropAsync(context, cancellation);
            await context.Database.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(cancellation);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"  fail  schema: {exception.GetType().Name}: {exception.Message}");

            return 1;
        }

        foreach (var (name, run) in steps)
        {
            try
            {
                await run(context, cancellation);
                Console.WriteLine($"  pass  {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"  fail  {name}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        if (!keep)
        {
            try
            {
                await DropAsync(context, cancellation);
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"  fail  cleanup: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            failures == 0
                ? $"all {steps.Count} steps passed"
                : $"{failures} failure(s) in {steps.Count} steps");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// Builds the model and generates the DDL a run would need, without a database. A build machine has
    /// no Dameng instance, so this is what keeps the sample from rotting between the runs that do: it
    /// fails when the model stops building or stops producing the tables and the sequence.
    /// </summary>
    private static int DryRun()
    {
        Console.WriteLine("Dameng provider acceptance, dry run");
        Console.WriteLine();

        var failures = 0;

        try
        {
            using var context = new ValidationContext(DryRunConnectionString);

            var script = context.Database.GenerateCreateScript();

            var missing = Tables.Append("DM_VALIDATION_SEQ")
                .Where(name => !script.Contains(name, StringComparison.Ordinal))
                .ToArray();

            if (missing.Length > 0)
            {
                failures++;
                Console.Error.WriteLine("  fail  the generated script has no " + string.Join(", ", missing));
            }
            else
            {
                Console.WriteLine($"  pass  create script covers {Tables.Length} tables and the sequence");
            }

            failures += MigrationChecks();
        }
        catch (Exception exception)
        {
            failures++;
            Console.Error.WriteLine($"  fail  model: {exception.GetType().Name}: {exception.Message}");
        }

        foreach (var (name, _) in Steps.All)
        {
            Console.WriteLine($"  step  {name}");
        }

        Console.WriteLine();
        Console.WriteLine(
            failures == 0
                ? $"dry run passed; {Steps.All.Count} steps declared"
                : "the sample does not describe the run it would make");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// Checks what a run would apply, without a database: that the migration the sample ships still matches
    /// the model it was generated from, and that the idempotent script of it is a script this provider's
    /// shape. Both fail when a change to the model is committed without the migration that carries it.
    /// </summary>
    private static int MigrationChecks()
    {
        using var journal = new JournalContext(DryRunConnectionString);

        var failures = 0;
        var snapshot = journal.GetService<IMigrationsAssembly>().ModelSnapshot;

        if (snapshot is null)
        {
            failures++;
            Console.Error.WriteLine("  fail  the sample ships no migration snapshot");
        }
        else
        {
            var model = journal.GetService<IDesignTimeModel>().Model;

            // The model the snapshot carries is not finalized: nothing builds it until something reads it,
            // and the differ needs the relational shape of it.
            var written = journal.GetService<IModelRuntimeInitializer>()
                .Initialize(snapshot.Model, designTime: true, validationLogger: null);

            var differences = journal.GetService<IMigrationsModelDiffer>()
                .GetDifferences(written.GetRelationalModel(), model.GetRelationalModel());

            if (differences.Count > 0)
            {
                failures++;
                Console.Error.WriteLine(
                    $"  fail  the migration snapshot no longer matches the model ({differences.Count} difference(s))");
            }
            else
            {
                Console.WriteLine("  pass  the migration snapshot matches the model");
            }
        }

        var idempotent = journal.GetService<IMigrator>()
            .GenerateScript(null, null, MigrationsSqlGenerationOptions.Idempotent);

        var problems = new List<string>();

        if (!idempotent.Contains("DM_VALIDATION_JOURNAL", StringComparison.Ordinal))
        {
            problems.Add("no statement of the migration");
        }

        if (!idempotent.Contains("FROM \"__EFMigrationsHistory\"", StringComparison.Ordinal))
        {
            problems.Add("no history guard");
        }

        if (!idempotent.Contains("FROM ALL_TABLES", StringComparison.Ordinal))
        {
            problems.Add("no catalog guard");
        }

        // The separator placement is pinned by the dialect test, and the statement that used to be glued to
        // it by the script writer is gone; what is left to check here is that nothing puts one back.
        if (idempotent.Contains("START TRANSACTION", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("a transaction statement Dameng does not define");
        }

        if (problems.Count > 0)
        {
            failures++;
            Console.Error.WriteLine("  fail  the idempotent script carries " + string.Join(", ", problems));
        }
        else
        {
            Console.WriteLine("  pass  the idempotent script carries its guards and names no transaction statement");
        }

        return failures;
    }
    /// <summary>
    /// A connection string for a dry run, which never opens a connection: the model builds from it and
    /// the driver is not asked to resolve anything.
    /// </summary>
    private const string DryRunConnectionString = "Server=localhost;Port=5236;User Id=SYSDBA;Password=SYSDBA";
    /// <summary>
    /// Drops what a previous run left, and the sequence with it. Dameng has no
    /// <c>DROP TABLE IF EXISTS</c>, so a table that is not there answers an error that has to be
    /// ignored; the run is about to create it again either way.
    /// </summary>
    private static async Task DropAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        await using var connection = new Dm.DmConnection(context.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // The last two belong to the migration steps: the table its migration creates, and the history the
        // relational layer keeps beside it. A run that left them behind would find the migration already
        // applied and stop exercising it.
        foreach (var statement in Tables
                     .Append("DM_VALIDATION_JOURNAL")
                     .Append("__EFMigrationsHistory")
                     .Select(table => $"DROP TABLE \"{table}\" CASCADE CONSTRAINTS")
                     .Append("DROP SEQUENCE \"DM_VALIDATION_SEQ\""))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (DbException)
            {
                // The object was not there, which is what the first run of a database looks like.
            }
        }
    }

    private static string? Connection(string[] args)
    {
        var fromArguments = args.FirstOrDefault(
            argument => !argument.StartsWith("--", StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(fromArguments))
        {
            return fromArguments;
        }

        var fromEnvironment = Environment.GetEnvironmentVariable("ConnectionStrings__Dm")
            ?? Environment.GetEnvironmentVariable("DM_CONNECTION_STRING");

        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        // Credentials for a live instance are kept in user secrets, which stay on the machine that set
        // them and never enter the repository:
        //   dotnet user-secrets set "ConnectionStrings:Dm" "<connection string>" --project samples/DmProviderValidation
        // A second instance is named with --secrets=<key> and read from "ConnectionStrings:<key>".
        var key = Option(args, "secrets") ?? "Dm";
        var fromSecrets = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly)
            .Build()[$"ConnectionStrings:{key}"];

        return string.IsNullOrWhiteSpace(fromSecrets) ? null : fromSecrets;
    }

    /// <summary>
    /// Reads a <c>--name=value</c> option, so a named connection string is not mistaken for the
    /// positional one.
    /// </summary>
    private static string? Option(string[] args, string name)
    {
        var prefix = "--" + name + "=";

        return args
            .Where(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(argument => argument[prefix.Length..])
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
