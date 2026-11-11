using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Dm.Extensions;

namespace DmProviderValidation;

/// <summary>
/// Query steps: each one writes rows through the model and then asks the server a question whose
/// answer is computed in .NET from the values it wrote. A translation that is generated but wrong
/// passes every shape test and fails here, which is why these run before the specification suite.
/// </summary>
internal static class QuerySteps
{
    public static IReadOnlyList<(string Name, Func<ValidationContext, CancellationToken, Task> Run)> All { get; } =
    [
        ("query: string functions", StringFunctionsAsync),
        ("query: date parts and arithmetic", DatePartsAsync),
        ("query: date differences", DateDifferencesAsync),
        ("query: interval between timestamps", IntervalAsync),
        ("query: math", MathAsync),
        ("query: paging and ordering", PagingAsync),
        ("query: grouping and aggregates", GroupingAsync),
        ("query: null semantics", NullSemanticsAsync),
        ("query: boolean projection", BooleanProjectionAsync),
        ("query: json column", JsonColumnAsync)
    ];

    private static async Task StringFunctionsAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        const string text = "Variable String 42";
        const string withPercent = "50% off";

        var run = await SeedAsync(
            context,
            cancellationToken,
            text,
            withPercent);

        var rows = context.Records.Where(item => item.RunId == run);
        var prefix = text[..8];

        Equal(await rows.CountAsync(item => item.Text.Contains("String"), cancellationToken), 1, "Contains");
        Equal(await rows.CountAsync(item => item.Text.StartsWith("Vari"), cancellationToken), 1, "StartsWith");
        Equal(await rows.CountAsync(item => item.Text.EndsWith("42"), cancellationToken), 1, "EndsWith");
        Equal(await rows.CountAsync(item => item.Text.ToUpper() == text.ToUpperInvariant(), cancellationToken), 1, "ToUpper");
        Equal(await rows.CountAsync(item => item.Text.ToLower() == text.ToLowerInvariant(), cancellationToken), 1, "ToLower");
        Equal(
            await rows.CountAsync(item => item.Text.Substring(0, 8) == prefix, cancellationToken),
            1,
            "Substring");
        Equal(
            await rows.CountAsync(item => item.Text.IndexOf("String") == text.IndexOf("String", StringComparison.Ordinal), cancellationToken),
            1,
            "IndexOf");
        Equal(
            await rows.CountAsync(item => item.Text.LastIndexOf('i') == text.LastIndexOf('i'), cancellationToken),
            1,
            "LastIndexOf");
        Equal(
            await rows.CountAsync(item => item.Text.Replace("Variable", "Fixed") == text.Replace("Variable", "Fixed", StringComparison.Ordinal), cancellationToken),
            1,
            "Replace");
        Equal(
            await rows.CountAsync(item => item.Text.Length == text.Length, cancellationToken),
            1,
            "Length");

        // A byte array is reached through the collection methods the framework normalizes the member
        // into, and its length is the byte count rather than the length of the stored hexadecimal form.
        Equal(
            await rows.CountAsync(item => item.Payload.Length > 0, cancellationToken),
            2,
            "a byte array that is not empty");

        Equal(
            await rows.CountAsync(item => item.Payload.Count() == 4, cancellationToken),
            2,
            "a byte array of four bytes");

        // The wildcard the value carries must be matched literally, so this finds the one row that
        // contains "50%" and not a row that merely starts with the same characters.
        Equal(
            await rows.CountAsync(item => item.Text.Contains("50%"), cancellationToken),
            1,
            "Contains with a wildcard must escape it");

        var padded = await rows
            .Where(item => item.Text == text)
            .Select(item => item.Number.ToString().PadLeft(6, '0'))
            .SingleAsync(cancellationToken);

        Equal(padded, 42.ToString().PadLeft(6, '0'), "PadLeft");
    }

    private static async Task DatePartsAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var run = await SeedAsync(context, cancellationToken, "dates", "dates");

        var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, 123, DateTimeKind.Unspecified);

        var parts = await context.Records
            .Where(item => item.RunId == run)
            .Select(item => new
            {
                item.CreatedAt.Year,
                item.CreatedAt.Month,
                item.CreatedAt.Day,
                item.CreatedAt.Hour,
                item.CreatedAt.Minute,
                item.CreatedAt.Second,
                item.CreatedAt.Millisecond,
                item.CreatedAt.DayOfYear
            })
            .FirstAsync(cancellationToken);

        Equal(parts.Year, createdAt.Year, "Year");
        Equal(parts.Month, createdAt.Month, "Month");
        Equal(parts.Day, createdAt.Day, "Day");
        Equal(parts.Hour, createdAt.Hour, "Hour");
        Equal(parts.Minute, createdAt.Minute, "Minute");
        Equal(parts.Second, createdAt.Second, "Second");
        Equal(parts.Millisecond, createdAt.Millisecond, "Millisecond");
        Equal(parts.DayOfYear, createdAt.DayOfYear, "DayOfYear");

        var added = await context.Records
            .Where(item => item.RunId == run)
            .Select(item => new
            {
                Day = item.CreatedAt.AddDays(1),
                Hour = item.CreatedAt.AddHours(1),
                Minute = item.CreatedAt.AddMinutes(1),
                Month = item.CreatedAt.AddMonths(1),
                Year = item.CreatedAt.AddYears(1),
                Date = DateOnly.FromDateTime(item.CreatedAt),
                Time = TimeOnly.FromDateTime(item.CreatedAt)
            })
            .FirstAsync(cancellationToken);

        // The stored value keeps milliseconds, so the comparison is made at that resolution.
        Equal(Truncated(added.Day), Truncated(createdAt.AddDays(1)), "AddDays");
        Equal(Truncated(added.Hour), Truncated(createdAt.AddHours(1)), "AddHours");
        Equal(Truncated(added.Minute), Truncated(createdAt.AddMinutes(1)), "AddMinutes");
        Equal(Truncated(added.Month), Truncated(createdAt.AddMonths(1)), "AddMonths");
        Equal(Truncated(added.Year), Truncated(createdAt.AddYears(1)), "AddYears");
        Equal(added.Date, DateOnly.FromDateTime(createdAt), "DateOnly.FromDateTime");
        Equal(Truncated(added.Time), Truncated(TimeOnly.FromDateTime(createdAt)), "TimeOnly.FromDateTime");

        // The members of a date and of a time of day: a day number counts from the epoch the provider
        // anchors on, and an addition to a time of day wraps past midnight the way .NET does.
        var members = await context.Records
            .Where(item => item.RunId == run)
            .Select(item => new
            {
                DayNumber = item.Day.DayNumber,
                Later = item.Time.AddHours(1.5),
                Earlier = item.Time.AddMinutes(-30),
                Interval = item.Time.Add(TimeSpan.FromMinutes(5))
            })
            .FirstAsync(cancellationToken);

        var day = new DateOnly(2026, 1, 2);
        var time = new TimeOnly(3, 4, 5, 123);

        Equal(members.DayNumber, day.DayNumber, "DateOnly.DayNumber");
        Equal(members.Later, time.AddHours(1.5), "TimeOnly.AddHours");
        Equal(members.Earlier, time.AddMinutes(-30), "TimeOnly.AddMinutes");
        Equal(members.Interval, time.Add(TimeSpan.FromMinutes(5)), "TimeOnly.Add");

        // The clock members read the server clock, so the assertion is an ordering rather than a value.
        var now = await context.Records
            .Where(item => item.RunId == run)
            .Select(item => new
            {
                InThePast = item.CreatedAt < DateTime.Now,
                BeforeToday = item.CreatedAt < DateTime.Today
            })
            .FirstAsync(cancellationToken);

        Equal(now.InThePast, true, "Now");
        Equal(now.BeforeToday, true, "Today");

        // UtcNow is SYS_EXTRACT_UTC(SYSTIMESTAMP). A translation that forgot to remove the server zone
        // would answer the local time, which this catches: the client compares against its own UTC clock.
        var utcNow = await context.Records
            .Where(item => item.RunId == run)
            .Select(item => DateTime.UtcNow)
            .FirstAsync(cancellationToken);

        Equal(
            Math.Abs((utcNow - DateTime.UtcNow).TotalMinutes) < 5,
            true,
            $"UtcNow must answer the UTC clock, not the local one: got {utcNow:O}");

        // Month arithmetic has to clamp the way .NET does, and it has to keep the time of day: on a
        // default-mode instance ADD_MONTHS answered a timestamp at day precision, which this pins.
        var monthEnd = NewRecord(run);
        monthEnd.CreatedAt = new DateTime(2026, 1, 31, 10, 20, 30, DateTimeKind.Unspecified);

        context.Records.Add(monthEnd);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var clamped = await context.Records
            .Where(item => item.Id == monthEnd.Id)
            .Select(item => new
            {
                Next = item.CreatedAt.AddMonths(1),
                Previous = item.CreatedAt.AddMonths(-1),
                Year = item.CreatedAt.AddYears(1)
            })
            .SingleAsync(cancellationToken);

        Equal(clamped.Next, monthEnd.CreatedAt.AddMonths(1), "AddMonths clamps and keeps the time");
        Equal(clamped.Previous, monthEnd.CreatedAt.AddMonths(-1), "AddMonths backwards clamps");
        Equal(clamped.Year, monthEnd.CreatedAt.AddYears(1), "AddYears clamps and keeps the time");
    }

    /// <summary>
    /// <c>EF.Functions.DateDiff*</c> counts the boundaries of a unit that the two values cross, which is
    /// not the same as the whole units between them: 03:59 and 04:01 are one hour apart by the first
    /// measure and zero by the second. Every case here is chosen so the two measures disagree.
    /// </summary>
    private static async Task DateDifferencesAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var run = Guid.NewGuid();

        (DateTime Start, DateTime End, string Label)[] cases =
        [
            (new DateTime(2026, 1, 2, 3, 59, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 2, 4, 1, 0, DateTimeKind.Unspecified), "an hour boundary"),
            (new DateTime(2026, 11, 30, 10, 0, 0, DateTimeKind.Unspecified), new DateTime(2027, 2, 1, 9, 0, 0, DateTimeKind.Unspecified), "a year boundary"),
            (new DateTime(2026, 1, 2, 3, 4, 5, 123, DateTimeKind.Unspecified), new DateTime(2026, 1, 2, 3, 4, 9, 456, DateTimeKind.Unspecified), "a second and a millisecond"),
            (new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), "a backwards difference")
        ];

        foreach (var (start, end, label) in cases)
        {
            var record = NewRecord(run);
            record.CreatedAt = start;

            context.Records.Add(record);
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();

            var differences = await context.Records
                .Where(item => item.Id == record.Id)
                .Select(item => new
                {
                    Year = EF.Functions.DateDiffYear(item.CreatedAt, end),
                    Month = EF.Functions.DateDiffMonth(item.CreatedAt, end),
                    Day = EF.Functions.DateDiffDay(item.CreatedAt, end),
                    Hour = EF.Functions.DateDiffHour(item.CreatedAt, end),
                    Minute = EF.Functions.DateDiffMinute(item.CreatedAt, end),
                    Second = EF.Functions.DateDiffSecond(item.CreatedAt, end)
                })
                .SingleAsync(cancellationToken);

            Equal(differences.Year, end.Year - start.Year, $"{label}: years");
            Equal(differences.Month, (end.Year - start.Year) * 12 + end.Month - start.Month, $"{label}: months");
            Equal((long)differences.Day, Boundaries(start, end, TimeSpan.TicksPerDay), $"{label}: days");
            Equal((long)differences.Hour, Boundaries(start, end, TimeSpan.TicksPerHour), $"{label}: hours");
            Equal((long)differences.Minute, Boundaries(start, end, TimeSpan.TicksPerMinute), $"{label}: minutes");
            Equal((long)differences.Second, Boundaries(start, end, TimeSpan.TicksPerSecond), $"{label}: seconds");

            // A millisecond difference of more than 24.8 days does not fit the int the method returns,
            // and the server answers 数据溢出 rather than a wrong number: a 63-day span measured that on
            // both compatibility modes, which is the same ceiling SQL Server has. The unit is asserted
            // only where the value fits.
            var milliseconds = Boundaries(start, end, TimeSpan.TicksPerMillisecond);

            if (milliseconds is > int.MinValue and < int.MaxValue)
            {
                var measured = await context.Records
                    .Where(item => item.Id == record.Id)
                    .Select(item => EF.Functions.DateDiffMillisecond(item.CreatedAt, end))
                    .SingleAsync(cancellationToken);

                Equal((long)measured, milliseconds, $"{label}: milliseconds");
            }
        }
    }

    /// <summary>
    /// The interval between two timestamps, which the reader hands back as a time span. The difference is
    /// measured in both directions, because a negative interval carries its sign in a place the parts do
    /// not: the accessors answer magnitudes and the sign is in the rendered value.
    /// </summary>
    private static async Task IntervalAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var run = Guid.NewGuid();

        var start = new DateTime(2026, 1, 1, 3, 4, 5, 123, DateTimeKind.Unspecified);
        var end = new DateTime(2026, 1, 2, 4, 5, 6, 456, DateTimeKind.Unspecified);

        var record = NewRecord(run);
        record.CreatedAt = start;

        context.Records.Add(record);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var rows = context.Records.Where(item => item.Id == record.Id);

        var forward = await rows.Select(item => end.Subtract(item.CreatedAt)).SingleAsync(cancellationToken);
        var backward = await rows.Select(item => item.CreatedAt.Subtract(end)).SingleAsync(cancellationToken);

        Equal(forward, end.Subtract(start), "the difference of two timestamps");
        Equal(backward, start.Subtract(end), "a difference that runs backwards");

        Equal(
            await rows.CountAsync(item => end.Subtract(item.CreatedAt) > TimeSpan.FromHours(1), cancellationToken),
            1,
            "a difference in a predicate");

        // The members of an interval that are not a part of it, read from a seeded row and from one that
        // runs backwards, because the sign of a negative interval is the part that is easy to lose.
        var negative = NewRecord(run);
        negative.Duration = TimeSpan.FromSeconds(-5.5);

        context.Records.Add(negative);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var expected = new TimeSpan(1, 2, 3, 4, 5);

        var seeded = await context.Records
            .Where(item => item.Id == record.Id)
            .Select(item => new { item.Duration.Ticks, item.Duration.Milliseconds, Magnitude = item.Duration.Duration() })
            .SingleAsync(cancellationToken);

        Equal(seeded.Ticks, expected.Ticks, "TimeSpan.Ticks");
        Equal(seeded.Milliseconds, expected.Milliseconds, "TimeSpan.Milliseconds");
        Equal(seeded.Magnitude, expected.Duration(), "TimeSpan.Duration");

        var backwards = await context.Records
            .Where(item => item.Id == negative.Id)
            .Select(item => new { item.Duration.Ticks, item.Duration.Milliseconds, Magnitude = item.Duration.Duration() })
            .SingleAsync(cancellationToken);

        Equal(backwards.Ticks, TimeSpan.FromSeconds(-5.5).Ticks, "TimeSpan.Ticks of a negative interval");
        Equal(backwards.Milliseconds, TimeSpan.FromSeconds(-5.5).Milliseconds, "TimeSpan.Milliseconds of a negative interval");
        Equal(backwards.Magnitude, TimeSpan.FromSeconds(5.5), "TimeSpan.Duration of a negative interval");
    }

    private static async Task MathAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var run = await SeedAsync(context, cancellationToken, "math", "math");

        var values = await context.Records
            .Where(item => item.RunId == run)
            .Select(item => new
            {
                Abs = Math.Abs(item.Money),
                Floor = Math.Floor(item.Ratio),
                Ceiling = Math.Ceiling(item.Ratio),
                Truncate = Math.Truncate(item.Ratio),
                Round = Math.Round(item.Money, 1),
                Pow = Math.Pow(item.SmallNumber, 2),
                Sqrt = Math.Sqrt(item.Number),
                Sign = Math.Sign(item.Money),
                Log = Math.Log(item.Number),
                Log10 = Math.Log10(item.Number),
                Max = Math.Max(item.Money, 0m),
                Min = Math.Min(item.Money, 0m)
            })
            .FirstAsync(cancellationToken);

        const decimal money = 1234.56m;
        const double ratio = 0.125;
        const int number = 42;
        const short small = 7;

        Equal(values.Abs, Math.Abs(money), "Abs");
        Equal(values.Floor, Math.Floor(ratio), "Floor");
        Equal(values.Ceiling, Math.Ceiling(ratio), "Ceiling");
        Equal(values.Truncate, Math.Truncate(ratio), "Truncate");
        Equal(values.Round, Math.Round(money, 1), "Round");
        Equal(values.Pow, Math.Pow(small, 2), "Pow");
        Equal(values.Sqrt, Math.Sqrt(number), "Sqrt");
        Equal(values.Sign, Math.Sign(money), "Sign");
        Equal(Math.Round(values.Log, 10), Math.Round(Math.Log(number), 10), "Log");
        Equal(Math.Round(values.Log10, 10), Math.Round(Math.Log10(number), 10), "Log10");
        Equal(values.Max, Math.Max(money, 0m), "Max");
        Equal(values.Min, Math.Min(money, 0m), "Min");
    }

    private static async Task PagingAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var run = Guid.NewGuid();

        for (var index = 0; index < 5; index++)
        {
            var record = NewRecord(run);
            record.Number = index;

            context.Records.Add(record);
        }

        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var rows = context.Records
            .Where(item => item.RunId == run)
            .OrderBy(item => item.Number);

        var page = await rows
            .Skip(1)
            .Take(2)
            .Select(item => item.Number)
            .ToListAsync(cancellationToken);

        Equal(string.Join(",", page), "1,2", "Skip and Take");

        var last = await rows
            .OrderByDescending(item => item.Number)
            .Select(item => item.Number)
            .FirstAsync(cancellationToken);

        Equal(last, 4, "OrderByDescending and First");

        Equal(await rows.CountAsync(cancellationToken), 5, "Count");
    }

    private static async Task GroupingAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var run = Guid.NewGuid();

        foreach (var (status, number) in new[] { ("a", 1), ("a", 2), ("b", 10) })
        {
            var record = NewRecord(run);
            record.Status = status;
            record.Number = number;

            context.Records.Add(record);
        }

        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var groups = await context.Records
            .Where(item => item.RunId == run)
            .GroupBy(item => item.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count(),
                Sum = group.Sum(item => item.Number),
                Min = group.Min(item => item.Number),
                Max = group.Max(item => item.Number)
            })
            .OrderBy(group => group.Status)
            .ToListAsync(cancellationToken);

        Equal(groups.Count, 2, "GroupBy count");
        Equal($"{groups[0].Status}:{groups[0].Count}:{groups[0].Sum}:{groups[0].Min}:{groups[0].Max}", "a:2:3:1:2", "the first group");
        Equal($"{groups[1].Status}:{groups[1].Count}:{groups[1].Sum}:{groups[1].Min}:{groups[1].Max}", "b:1:10:10:10", "the second group");

        var having = await context.Records
            .Where(item => item.RunId == run)
            .GroupBy(item => item.Status)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);

        Equal(string.Join(",", having), "a", "GroupBy with Having");
    }

    private static async Task NullSemanticsAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var run = Guid.NewGuid();

        var withNull = NewRecord(run);
        withNull.Optional = null;

        var withoutNull = NewRecord(run);
        withoutNull.Optional = "present";

        context.Records.AddRange(withNull, withoutNull);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var rows = context.Records.Where(item => item.RunId == run);

        Equal(await rows.CountAsync(item => item.Optional == null, cancellationToken), 1, "a null comparison");
        Equal(await rows.CountAsync(item => item.Optional != null, cancellationToken), 1, "a non-null comparison");
        Equal(
            await rows.CountAsync(item => item.Optional == null || item.Optional == "present", cancellationToken),
            2,
            "an OR over null and a value");
        Equal(
            await rows.CountAsync(item => item.Optional!.StartsWith("pre"), cancellationToken),
            1,
            "a member access behind a null check");
    }

    /// <summary>
    /// A condition in a select list is answered as <c>1</c> or <c>0</c>, which the base generator spelled
    /// as <c>SELECT EXISTS (...)</c> until a live run rejected it. Reading the value back proves both the
    /// statement and the materialization.
    /// </summary>
    private static async Task BooleanProjectionAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var run = await SeedAsync(context, cancellationToken, "boolean", "boolean");

        var projected = await context.Records
            .Where(item => item.RunId == run)
            .Select(item => new
            {
                Greater = item.Number > 1,
                NotNull = item.Optional != null,
                Any = context.Records.Any(other => other.RunId == run && other.Number == item.Number)
            })
            .FirstAsync(cancellationToken);

        Equal(projected.Greater, true, "a comparison in a projection");
        Equal(projected.NotNull, false, "a null check in a projection");
        Equal(projected.Any, true, "an EXISTS in a projection");

        var any = await context.Records
            .Where(item => item.RunId == run)
            .AnyAsync(cancellationToken);

        Equal(any, true, "Any over a set");

        // A boolean column takes part in a predicate only when it is compared with 1: the server rejects
        // the bare column, and the same column under NOT or beside a comparison, with
        // 查询使用值表达式作为过滤条件. Each of the three shapes is asked here, because each was measured
        // to fail on its own.
        var rows = context.Records.Where(item => item.RunId == run);

        Equal(await rows.CountAsync(item => item.Flag, cancellationToken), 2, "a boolean column as a predicate");
        Equal(await rows.CountAsync(item => !item.Flag, cancellationToken), 0, "a negated boolean column");
        Equal(
            await rows.CountAsync(item => item.Flag && item.Number == 42, cancellationToken),
            2,
            "a boolean column beside a comparison");
    }

    /// <summary>
    /// A JSON column holds an owned entity. Measured on 8.1.4.80: the server has a `JSON` type, the driver
    /// moves it as text, `JSON_VALUE` answers a member as text, and the `RETURNING` form that would type it
    /// is rejected with `返回值的数据类型无效`, so a member that is not text is cast from that text. The
    /// document itself is the column, so reading the entity back is a plain column read.
    /// </summary>
    private static async Task JsonColumnAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var run = Guid.NewGuid();

        var first = NewRecord(run);
        first.Document = new Document { Title = "first", Pages = 3, Flag = true };

        var second = NewRecord(run);
        second.Document = new Document { Title = "second", Pages = 7 };

        context.Records.AddRange(first, second);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var rows = context.Records.Where(item => item.RunId == run);

        var documents = await rows
            .OrderBy(item => item.Document.Pages)
            .Select(item => new { item.Document.Title, item.Document.Pages })
            .ToListAsync(cancellationToken);

        Equal($"{documents[0].Title}/{documents[0].Pages}", "first/3", "the first document");
        Equal($"{documents[1].Title}/{documents[1].Pages}", "second/7", "the second document");

        Equal(await rows.CountAsync(item => item.Document.Title == "second", cancellationToken), 1, "a text member");
        Equal(await rows.CountAsync(item => item.Document.Pages > 5, cancellationToken), 1, "a number member");

        // A boolean is stored as text, so it is compared with that text rather than cast to BIT.
        Equal(await rows.CountAsync(item => item.Document.Flag, cancellationToken), 1, "a boolean member");

        // A change inside the document is written back as the whole document, because the column holds
        // the document rather than the members.
        var tracked = await rows.SingleAsync(item => item.Document.Title == "first", cancellationToken);
        tracked.Document.Pages = 9;
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var pages = await context.Records
            .Where(item => item.RunId == run && item.Document.Title == "first")
            .Select(item => item.Document.Pages)
            .SingleAsync(cancellationToken);

        Equal(pages, 9, "a changed member");
    }

    private static async Task<Guid> SeedAsync(
        ValidationContext context,
        CancellationToken cancellationToken,
        string text,
        string percentText)
    {
        var run = Guid.NewGuid();

        var first = NewRecord(run);
        first.Text = text;

        var second = NewRecord(run);
        second.Text = percentText;

        context.Records.AddRange(first, second);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        return run;
    }

    private static Record NewRecord(Guid run) => new()
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
        CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, 123, DateTimeKind.Unspecified),
        Offset = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(8)),
        Day = new DateOnly(2026, 1, 2),
        Time = new TimeOnly(3, 4, 5, 123),
        Duration = new TimeSpan(1, 2, 3, 4, 5),
        Key = Guid.NewGuid(),
        Payload = [1, 2, 3, 4]
    };

    private static DateTime Truncated(DateTime value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMillisecond));

    private static TimeOnly Truncated(TimeOnly value) => new(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond);

    /// <summary>
    /// The number of <paramref name="unitTicks" /> boundaries two values cross, which is what
    /// <c>DATEDIFF</c> counts: both values are truncated to the unit first, then subtracted.
    /// </summary>
    private static long Boundaries(DateTime start, DateTime end, long unitTicks)
        => (end.Ticks - end.Ticks % unitTicks) / unitTicks - (start.Ticks - start.Ticks % unitTicks) / unitTicks;

    private static void Equal<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
        }
    }
}