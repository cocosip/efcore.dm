using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

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
        var connectionString = Connection(args);

        if (connectionString is null)
        {
            Console.Error.WriteLine(
                "No connection string. Pass it as the first argument, or set ConnectionStrings__Dm.");

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
    /// Drops what a previous run left, and the sequence with it. Dameng has no
    /// <c>DROP TABLE IF EXISTS</c>, so a table that is not there answers an error that has to be
    /// ignored; the run is about to create it again either way.
    /// </summary>
    private static async Task DropAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        await using var connection = new Dm.DmConnection(context.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var statement in Tables.Select(table => $"DROP TABLE \"{table}\" CASCADE CONSTRAINTS")
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

        return string.IsNullOrWhiteSpace(fromEnvironment) ? null : fromEnvironment;
    }
}
