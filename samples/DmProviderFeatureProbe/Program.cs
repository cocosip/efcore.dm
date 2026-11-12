using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace DmProviderFeatureProbe;

/// <summary>
/// Functional probe for the provider (docs/testing.md §5). It is run by a human against a reachable
/// Dameng instance, is not a CI step, and reports every step separately: a failing step does not stop
/// the run and the exit code is non-zero when any step failed.
/// </summary>
internal static class Program
{
    private const string DryRunConnectionString =
        "Server=localhost;Port=5236;User Id=SYSDBA;Password=SYSDBA";

    private static async Task<int> Main(string[] args)
    {
        if (Has(args, "--list"))
        {
            PrintSteps(Filter(args));
            return 0;
        }

        if (Has(args, "--dry-run"))
        {
            return DryRun(args);
        }

        var connectionString = Connection(args);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine(
                "No connection string. Pass it as the first argument, set ConnectionStrings__Dm, "
                + "or set the \"ConnectionStrings:Dm\" user secret.");

            return 2;
        }

        if (!ConvertsToTimeZone(connectionString))
        {
            Console.Error.WriteLine(
                "The connection string must set convertToTz=True: the probe reads DateTimeOffset columns, "
                + "and without it the driver returns DateTime for a DATETIME WITH TIME ZONE column, which "
                + "the materializer cannot cast.");

            return 2;
        }

        var keep = Has(args, "--keep");
        var cancellation = CancellationToken.None;
        var steps = Filter(args);
        var failures = 0;
        var skipped = 0;

        Console.WriteLine("Dameng provider feature probe");
        Console.WriteLine();

        try
        {
            await Schema.DropAsync(connectionString, cancellation);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"  fail  drop: {exception.GetType().Name}: {exception.Message}");

            return 1;
        }

        await using var context = new FeatureContext(connectionString);

        try
        {
            await context.Database.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(cancellation);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"  fail  schema: {exception.GetType().Name}: {exception.Message}");

            return 1;
        }

        foreach (var step in steps)
        {
            if (ProbeLedger.IsSkipped(step.Name, out var reason))
            {
                skipped++;
                Console.WriteLine($"  skip  {step.Name}: {reason}");
                continue;
            }

            try
            {
                // A step that failed its own concurrency check leaves a pending entry, and a step that
                // succeeded may leave tracked rows; neither belongs to the next step.
                context.ChangeTracker.Clear();
                await Schema.ClearAsync(context, cancellation);
                await step.Run(context, cancellation);
                Console.WriteLine($"  pass  {step.Name}");
            }
            catch (Exception exception)
            {
                failures++;

                // A failed SaveChanges leaves its entries pending, and every later step would then fail
                // with the same error. The run reports each step's own state, so the tracker is reset.
                context.ChangeTracker.Clear();
                Console.Error.WriteLine($"  fail  {step.Name}: {Describe(exception)}");
            }
        }

        if (!keep)
        {
            try
            {
                await Schema.DropAsync(connectionString, cancellation);
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
                ? $"{steps.Count - skipped} passed, {skipped} skipped"
                : $"{failures} failed, {steps.Count - skipped - failures} passed, {skipped} skipped");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// Builds the model and the schema script without a database: the check a build machine can make,
    /// and the one that stops the probe from rotting between the runs that have an instance.
    /// </summary>
    private static int DryRun(string[] args)
    {
        Console.WriteLine("Dameng provider feature probe, dry run");
        Console.WriteLine();

        var failures = 0;
        var steps = Filter(args);

        try
        {
            using var context = new FeatureContext(DryRunConnectionString);

            var script = context.Database.GenerateCreateScript();
            var missing = Schema.Tables
                .Where(table => !script.Contains(table, StringComparison.Ordinal))
                .ToArray();

            if (missing.Length > 0)
            {
                failures++;
                Console.Error.WriteLine("  fail  the generated script has no " + string.Join(", ", missing));
            }
            else
            {
                Console.WriteLine($"  pass  the script covers {Schema.Tables.Count} tables");
            }

            if (!script.Contains(Schema.Sequence, StringComparison.Ordinal))
            {
                failures++;
                Console.Error.WriteLine($"  fail  the script has no sequence {Schema.Sequence}");
            }
            else
            {
                Console.WriteLine($"  pass  the script has the sequence {Schema.Sequence}");
            }
        }
        catch (Exception exception)
        {
            failures++;
            Console.Error.WriteLine($"  fail  model: {exception.GetType().Name}: {exception.Message}");
        }

        var duplicates = steps
            .GroupBy(step => step.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            failures++;
            Console.Error.WriteLine("  fail  duplicate step names: " + string.Join(", ", duplicates));
        }
        else
        {
            Console.WriteLine($"  pass  {steps.Count} step names are unique");
        }

        foreach (var step in steps)
        {
            Console.WriteLine($"  step  {step.Name}");
        }

        Console.WriteLine();
        Console.WriteLine(
            failures == 0
                ? $"dry run passed; {steps.Count} steps declared"
                : "the probe does not describe the run it would make");

        return failures == 0 ? 0 : 1;
    }

    private static void PrintSteps(IReadOnlyList<Step> steps)
    {
        foreach (var step in steps)
        {
            Console.WriteLine($"{step.Area}\t{step.Name}");
        }
    }

    private static IReadOnlyList<Step> Filter(string[] args)
    {
        var filter = Option(args, "filter");

        return string.IsNullOrWhiteSpace(filter)
            ? Steps.All
            : Steps.All
                .Where(step => step.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                               || step.Area.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToArray();
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
        //   dotnet user-secrets set "ConnectionStrings:Dm" "<connection string>" --project samples/DmProviderFeatureProbe
        // A second instance is named with --secrets=<key> and read from "ConnectionStrings:<key>".
        var key = Option(args, "secrets") ?? "Dm";
        var fromSecrets = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly)
            .Build()[$"ConnectionStrings:{key}"];

        return string.IsNullOrWhiteSpace(fromSecrets) ? null : fromSecrets;
    }

    private static string? Option(string[] args, string name)
    {
        var prefix = "--" + name + "=";

        return args
            .Where(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(argument => argument[prefix.Length..])
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static bool ConvertsToTimeZone(string connectionString)
    {
        var option = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .FirstOrDefault(parts => parts.Length == 2
                                     && parts[0].Trim().Equals("convertToTz", StringComparison.OrdinalIgnoreCase));

        return option is not null && bool.TryParse(option[1].Trim(), out var enabled) && enabled;
    }

    private static bool Has(string[] args, string name)
        => args.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static string Describe(Exception exception)
    {
        var builder = new StringBuilder(exception.GetType().Name).Append(": ").Append(exception.Message);

        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            builder.Append(" -> ").Append(inner.GetType().Name).Append(": ").Append(inner.Message);
        }

        return builder.ToString();
    }
}
