using System.Data;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace DmProviderValidation;

/// <summary>
/// The steps of the acceptance run. Each one is independent: it writes its own rows, marks them with
/// its own run identifier and reads only those back, so a failing step does not spoil the next one.
/// </summary>
internal static class Steps
{
    public static IReadOnlyList<(string Name, Func<ValidationContext, CancellationToken, Task> Run)> All { get; } =
    [
        ("connection open and close", ConnectionAsync),
        ("scalar query", ScalarAsync),
        ("insert", InsertAsync),
        ("type round trip", TypesAsync),
        ("update", UpdateAsync),
        ("delete", DeleteAsync),
        ("transaction commit", CommitAsync),
        ("transaction rollback", RollbackAsync),
    ("transaction scope", ScopeAsync),
        ("savepoint", SavepointAsync),
        ("optimistic concurrency", ConcurrencyAsync),
        ("retrying execution strategy", RetryingExecutionAsync),
        ("identity readback", IdentityAsync),
        ("sequence readback", SequenceAsync),
        ("execute update", ExecuteUpdateAsync),
        ("execute delete", ExecuteDeleteAsync),
        ("left join", LeftJoinAsync),
        ("right join", RightJoinAsync),
        .. QuerySteps.All,
        ("cancellation", CancellationAsync),
        ("migrations: apply, read back and apply again", MigrationsAsync)
    ];

    private static async Task ConnectionAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        Expect(
            await context.Database.CanConnectAsync(cancellationToken),
            "the database must be reachable");

        await context.Database.OpenConnectionAsync(cancellationToken);

        Expect(
            context.Database.GetDbConnection().State == ConnectionState.Open,
            "the connection must be open after opening it");

        await context.Database.CloseConnectionAsync();

        Expect(
            context.Database.GetDbConnection().State == ConnectionState.Closed,
            "the connection must be closed after closing it");
    }

    private static async Task ScalarAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var value = await context.Database
            .SqlQuery<int>($"SELECT 1 AS \"Value\" FROM DUAL")
            .SingleAsync(cancellationToken);

        Expect(value == 1, "SELECT 1 must answer 1");
    }

    private static async Task InsertAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var run = Guid.NewGuid();
        var record = NewRecord(run);

        context.Records.Add(record);
        var written = await context.SaveChangesAsync(cancellationToken);

        Expect(written == 1, "one row must be written");
        Expect(record.Id > 0, "the identity value must be read back into the entity");
        Expect(
            await context.Records.CountAsync(item => item.RunId == run, cancellationToken) == 1,
            "the row must be found by its run identifier");
    }

    private static async Task TypesAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var expected = NewRecord(Guid.NewGuid());

        context.Records.Add(expected);
        await context.SaveChangesAsync(cancellationToken);

        await using var reader = new ValidationContext(context.ConnectionString);
        var actual = await reader.Records
            .AsNoTracking()
            .SingleAsync(item => item.Id == expected.Id, cancellationToken);

        Expect(actual.Text == expected.Text, "a variable length string must round trip");
        Expect(
            actual.FixedText.TrimEnd() == expected.FixedText.TrimEnd(),
            "a fixed length string must round trip, padded");
        Expect(actual.Optional is null, "a null string must round trip as null");
        Expect(actual.Number == expected.Number, "an integer must round trip");
        Expect(actual.BigNumber == expected.BigNumber, "a long must round trip");
        Expect(actual.SmallNumber == expected.SmallNumber, "a short must round trip");
        Expect(actual.Money == expected.Money, "a decimal must round trip with its scale");
        Expect(Math.Abs(actual.Ratio - expected.Ratio) < 1e-12, "a double must round trip");
        Expect(Math.Abs(actual.Ratio32 - expected.Ratio32) < 1e-6f, "a single must round trip");
        Expect(actual.Flag == expected.Flag, "a boolean must round trip");
        Expect(actual.CreatedAt == expected.CreatedAt, "a timestamp must round trip");
        Expect(
            actual.Offset.UtcDateTime == expected.Offset.UtcDateTime,
            "an offset timestamp must round trip to the same instant");
        Expect(actual.Day == expected.Day, "a date must round trip");
        Expect(actual.Time == expected.Time, "a time of day must round trip");
        Expect(actual.Duration == expected.Duration, "an interval must round trip");
        Expect(actual.Key == expected.Key, "a Guid must round trip");
        Expect(actual.Payload.SequenceEqual(expected.Payload), "a blob must round trip");
        Expect(actual.Status == expected.Status, "the status must round trip");
    }

    private static async Task UpdateAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var run = Guid.NewGuid();
        var record = NewRecord(run);

        context.Records.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        record.Status = "updated";
        record.Version++;
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var reloaded = await context.Records
            .AsNoTracking()
            .SingleAsync(item => item.RunId == run, cancellationToken);

        Expect(reloaded.Status == "updated", "the update must be visible after reading again");
    }

    private static async Task DeleteAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var run = Guid.NewGuid();
        var record = NewRecord(run);

        context.Records.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        context.Records.Remove(record);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        Expect(
            await context.Records.CountAsync(item => item.RunId == run, cancellationToken) == 0,
            "the deleted row must be gone");
    }

    private static async Task CommitAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var run = Guid.NewGuid();

        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            context.Records.Add(NewRecord(run));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        Expect(
            await context.Records.CountAsync(item => item.RunId == run, cancellationToken) == 1,
            "a committed row must be visible");
    }

    private static async Task RollbackAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var run = Guid.NewGuid();

        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            context.Records.Add(NewRecord(run));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
        }

        context.ChangeTracker.Clear();

        Expect(
            await context.Records.CountAsync(item => item.RunId == run, cancellationToken) == 0,
            "a rolled back row must be gone");
    }

    /// <summary>
    /// An ambient transaction: the framework enlists it only for a provider that says it supports them,
    /// and the base default is no, so the write of a scope that is never completed must not survive.
    /// </summary>
    private static async Task ScopeAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var aborted = Guid.NewGuid();

        using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            context.Records.Add(NewRecord(aborted));
            await context.SaveChangesAsync(cancellationToken);
        }

        context.ChangeTracker.Clear();
        var committed = Guid.NewGuid();

        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            context.Records.Add(NewRecord(committed));
            await context.SaveChangesAsync(cancellationToken);
            scope.Complete();
        }

        context.ChangeTracker.Clear();

        Expect(
            await context.Records.CountAsync(item => item.RunId == aborted, cancellationToken) == 0,
            "a row written in a scope that was not completed must be gone");

        Expect(
            await context.Records.CountAsync(item => item.RunId == committed, cancellationToken) == 1,
            "a row written in a completed scope must be visible");
    }

    private static async Task SavepointAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var run = Guid.NewGuid();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        context.Records.Add(NewRecord(run));
        await context.SaveChangesAsync(cancellationToken);

        await transaction.CreateSavepointAsync("validation", cancellationToken);

        context.Records.Add(NewRecord(run));
        await context.SaveChangesAsync(cancellationToken);

        await transaction.RollbackToSavepointAsync("validation", cancellationToken);
        await transaction.ReleaseSavepointAsync("validation", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        Expect(
            await context.Records.CountAsync(item => item.RunId == run, cancellationToken) == 1,
            "the row written inside the savepoint must be undone, and the one before it kept");
    }

    private static async Task ConcurrencyAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var run = Guid.NewGuid();
        var record = NewRecord(run);

        context.Records.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        await using var other = new ValidationContext(context.ConnectionString);
        var stale = await other.Records.SingleAsync(item => item.Id == record.Id, cancellationToken);

        record.Status = "first writer";
        record.Version++;
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        stale.Status = "second writer";
        stale.Version++;

        var conflicted = false;

        try
        {
            await other.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            conflicted = true;
        }

        Expect(conflicted, "a write over a stale row must raise a concurrency exception");
    }

    /// <summary>
    /// A transient failure is retried and a permanent one is not. The transient case closes the server side
    /// session of a context that holds an open connection, which is the measured 6027: the first attempt
    /// fails, and the retry only answers once a new connection is open, because the driver leaves the dead
    /// one reporting Open. The permanent case is a table that does not exist, which has to reach the caller
    /// on the first attempt.
    /// </summary>
    private static async Task RetryingExecutionAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var total = await context.Records.CountAsync(cancellationToken);

        await using var retried = new ValidationContext(
            context.ConnectionString,
            builder => builder.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2)));

        await retried.Database.OpenConnectionAsync(cancellationToken);

        string? session;

        await using (var command = ((global::Dm.DmConnection)retried.Database.GetDbConnection()).CreateCommand())
        {
            command.CommandText = "SELECT SYS_CONTEXT('USERENV', 'SID') FROM DUAL";
            session = (await command.ExecuteScalarAsync(cancellationToken))?.ToString();
        }

        // The id comes from the server, and a call that takes it as a value is not a statement the driver
        // parameterises, so it is checked before it is embedded.
        Expect(session is { Length: > 0 } && session.All(char.IsAsciiDigit), "the session id must be a number");

        await using (var killer = new global::Dm.DmConnection(context.ConnectionString))
        {
            await killer.OpenAsync(cancellationToken);

            await using var command = killer.CreateCommand();
            command.CommandText = $"CALL SP_CLOSE_SESSION({session})";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var attempts = 0;
        var strategy = retried.Database.CreateExecutionStrategy();

        var count = await strategy.ExecuteAsync(
            async () =>
            {
                attempts++;
                return await retried.Records.CountAsync(cancellationToken);
            });

        Expect(attempts > 1, "a killed connection must be retried");
        Expect(count == total, "the retried statement must answer the same count");

        var permanentAttempts = 0;
        global::Dm.DmException? failure = null;

        try
        {
            await strategy.ExecuteAsync(
                async () =>
                {
                    permanentAttempts++;
                    await retried.Database.ExecuteSqlRawAsync(
                        "DELETE FROM \"DM_VALIDATION_ABSENT\"",
                        cancellationToken);
                });
        }
        catch (global::Dm.DmException exception)
        {
            failure = exception;
        }

        Expect(failure is not null, "a missing table must be thrown to the caller");
        Expect(permanentAttempts == 1, "a missing table must not be retried");
        Expect(failure?.Number == -2106, "a missing table is the measured -2106");
    }
    private static async Task IdentityAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var first = NewRecord(Guid.NewGuid());
        var second = NewRecord(Guid.NewGuid());

        context.Records.AddRange(first, second);
        await context.SaveChangesAsync(cancellationToken);

        Expect(first.Id > 0 && second.Id > 0, "both identity values must be generated");
        Expect(first.Id != second.Id, "the identity values must differ");
        Expect(second.Id > first.Id, "the identity values must increase");
    }

    private static async Task SequenceAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var first = await NextValueAsync(context, cancellationToken);
        var second = await NextValueAsync(context, cancellationToken);

        Expect(first >= 1000, "the sequence must start at its declared value");
        Expect(second > first, "NEXTVAL must increase");
    }

    private static Task<int> NextValueAsync(ValidationContext context, CancellationToken cancellationToken)
        => context.Database
            .SqlQuery<int>($"SELECT DM_VALIDATION_SEQ.NEXTVAL AS \"Value\" FROM DUAL")
            .SingleAsync(cancellationToken);

    private static async Task ExecuteUpdateAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var run = Guid.NewGuid();

        context.Records.AddRange(NewRecord(run), NewRecord(run));
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var updated = await context.Records
            .Where(item => item.RunId == run)
            .ExecuteUpdateAsync(setter => setter.SetProperty(item => item.Status, "bulk"), cancellationToken);

        Expect(updated == 2, "both rows must be reported as updated");
        Expect(
            await context.Records.CountAsync(
                item => item.RunId == run && item.Status == "bulk",
                cancellationToken) == 2,
            "both rows must carry the new value");
    }

    private static async Task ExecuteDeleteAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var run = Guid.NewGuid();

        context.Records.AddRange(NewRecord(run), NewRecord(run));
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var deleted = await context.Records
            .Where(item => item.RunId == run)
            .ExecuteDeleteAsync(cancellationToken);

        Expect(deleted == 2, "both rows must be reported as deleted");
        Expect(
            await context.Records.CountAsync(item => item.RunId == run, cancellationToken) == 0,
            "both rows must be gone");
    }

    private static async Task LeftJoinAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        await ResetJoinTablesAsync(context, cancellationToken);

        var parent = new Parent { Name = "left" };
        var child = new Child { Name = "left child" };

        context.Parents.AddRange(parent, new Parent { Name = "no children" });
        await context.SaveChangesAsync(cancellationToken);

        child.ParentId = parent.Id;
        context.Children.Add(child);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var rows = await (
            from candidate in context.Parents
            join other in context.Children on candidate.Id equals other.ParentId into children
            from other in children.DefaultIfEmpty()
            orderby candidate.Id, other!.Id
            select new { ParentId = candidate.Id, ChildId = (int?)other.Id })
            .ToListAsync(cancellationToken);

        Expect(rows.Count == 2, "a left join must keep the parent without children");
        Expect(rows.Any(row => row.ChildId is null), "the parent without children must have no child");
        Expect(rows.Any(row => row.ChildId is not null), "the parent with a child must have one");
    }

    private static async Task RightJoinAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        await ResetJoinTablesAsync(context, cancellationToken);

        var parent = new Parent { Name = "right" };

        context.Parents.Add(parent);
        await context.SaveChangesAsync(cancellationToken);

        context.Children.AddRange(
            new Child { ParentId = parent.Id, Name = "matched" },
            new Child { Name = "orphan" });

        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var rows = await context.Parents
            .RightJoin(
                context.Children,
                candidate => candidate.Id,
                child => child.ParentId,
                (candidate, child) => new { ParentName = candidate != null ? candidate.Name : null, child.Name })
            .ToListAsync(cancellationToken);

        Expect(rows.Count == 2, "a right join must keep the child without a parent");
        Expect(rows.Any(row => row.ParentName is null), "the orphan child must have no parent");
        Expect(rows.Any(row => row.ParentName is not null), "the matched child must have a parent");
    }

    private static async Task ResetJoinTablesAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();

        await context.Children.ExecuteDeleteAsync(cancellationToken);
        await context.Parents.ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task CancellationAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var cancelled = false;

        try
        {
            await context.Records.ToListAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Expect(cancelled, "a token cancelled before the call must stop the query");
    }

    private static Record NewRecord(Guid run)
    {
        var created = new DateTime(2026, 1, 2, 3, 4, 5, 123, DateTimeKind.Unspecified);

        return new Record
        {
            RunId = run,
            Text = "variable",
            FixedText = "fixed",
            Optional = null,
            Status = "new",
            Number = 42,
            BigNumber = 4_200_000_000,
            SmallNumber = 7,
            Money = 1234.56m,
            Ratio = 0.125,
            Ratio32 = 0.25f,
            Flag = true,
            CreatedAt = created,
            Offset = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(8)),
            Day = new DateOnly(2026, 1, 2),
            Time = new TimeOnly(3, 4, 5, 123),
            Duration = new TimeSpan(1, 2, 3, 4, 5),
            Key = Guid.NewGuid(),
            Payload = [1, 2, 3, 4]
        };
    }

    /// <summary>
    /// Applies the migration the sample ships and reads the table it created back through the model. The
    /// second apply is the part that matters: it runs against a database where every statement has already
    /// been applied, which is what a deployment pipeline does after a partial failure.
    /// </summary>
    private static async Task MigrationsAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        await using var journal = new JournalContext(context.ConnectionString);

        var migrations = journal.GetService<IMigrationsAssembly>().Migrations.Count;

        Expect(
            (await journal.Database.GetPendingMigrationsAsync(cancellationToken)).Count() == migrations,
            "every migration must be pending on a database no migration has run against");

        await journal.Database.MigrateAsync(cancellationToken);

        Expect(
            !(await journal.Database.GetPendingMigrationsAsync(cancellationToken)).Any(),
            "no migration must be pending once they have been applied");

        await using (var write = new JournalContext(context.ConnectionString))
        {
            write.Journal.Add(new Journal { Note = "migrated" });

            await write.SaveChangesAsync(cancellationToken);
        }

        await using (var read = new JournalContext(context.ConnectionString))
        {
            Expect(
                await read.Journal.AnyAsync(entry => entry.Note == "migrated", cancellationToken),
                "a row written through the model must be readable from the migrated table");
        }

        await journal.Database.MigrateAsync(cancellationToken);

        var history = await journal.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM \"__EFMigrationsHistory\"")
            .SingleAsync(cancellationToken);

        Expect(history == migrations, $"the history table must hold {migrations} row(s), not {history}");
    }
    private static void Expect(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
