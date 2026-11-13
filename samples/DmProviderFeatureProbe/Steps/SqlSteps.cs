using System.Data;
using Microsoft.EntityFrameworkCore;

namespace DmProviderFeatureProbe;

/// <summary>
/// Projections, grouping, raw SQL, transactions and the update pipeline. Each predicate runs before
/// materialisation, so a translation the client would have done instead throws here.
/// </summary>
internal static class SqlSteps
{
    public static IReadOnlyList<Step> All { get; } =
    [
        new("query: projections and DTOs", "query", ProjectionAsync),
        new("query: Include and ThenInclude, filtered include", "query", FilteredIncludeAsync),
        new("query: grouping and aggregates", "query", GroupingAsync),
        new("query: FromSql and composable raw SQL", "query", FromSqlAsync),
        new("query: raw commands", "query", RawCommandsAsync),
        new("query: transaction mixing raw SQL and SaveChanges", "query", TransactionMixAsync),
        new("update: ExecuteUpdate and ExecuteDelete", "update", ExecuteUpdateAsync),
        new("update: query filter under ExecuteUpdate", "update", ExecuteUpdateWithFilterAsync),
        new("query: no-tracking read", "query", NoTrackingAsync),
        new("query: compiled query", "query", CompiledQueryAsync),
    ];

    private static async Task<Order> SeedOrderAsync(
        FeatureContext context,
        CancellationToken cancellationToken,
        string number,
        params (string Sku, decimal Price)[] lines)
    {
        var order = new Order
        {
            Number = number,
            Shipping = new Shipping { Street = "2 query street", City = "query" },
        };

        foreach (var (sku, price) in lines)
        {
            order.Lines.Add(new OrderLine { Sku = sku, Price = price });
        }

        context.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        return order;
    }

    private static async Task ProjectionAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        await SeedOrderAsync(context, cancellationToken, "P1", ("a", 10m), ("b", 20m));

        var summaries = await context.Orders
            .Select(order => new OrderSummary
            {
                Number = order.Number,
                Lines = order.Lines.Count,
                Total = order.Lines.Sum(line => line.Price),
            })
            .ToListAsync(cancellationToken);

        Assert.Equal(1, summaries.Count, "one summary");
        Assert.Equal("P1", summaries[0].Number, "the projected string");
        Assert.Equal(2, summaries[0].Lines, "the projected count is answered by the server");
        Assert.Equal(30m, summaries[0].Total, "the projected sum is answered by the server");
    }

    private static async Task FilteredIncludeAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        await SeedOrderAsync(context, cancellationToken, "FI", ("cheap", 5m), ("dear", 15m));

        var read = await context.Orders
            .Include(order => order.Lines.Where(line => line.Price > 10m))
            .SingleAsync(cancellationToken);

        Assert.Equal(1, read.Lines.Count, "the filtered include loads only the matching line");
        Assert.Equal("dear", read.Lines.Single().Sku, "the matching line is the dear one");
    }

    private static async Task GroupingAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        await SeedOrderAsync(context, cancellationToken, "G1", ("a", 10m), ("a", 20m), ("b", 5m));

        var groups = await context.OrderLines
            .GroupBy(line => line.Sku)
            .Select(group => new
            {
                group.Key,
                Count = group.Count(),
                Total = group.Sum(line => line.Price),
            })
            .OrderBy(item => item.Key)
            .ToListAsync(cancellationToken);

        Assert.Equal(2, groups.Count, "two groups");
        Assert.Equal("a", groups[0].Key, "the first key");
        Assert.Equal(2, groups[0].Count, "the first group counts two rows");
        Assert.Equal(30m, groups[0].Total, "the first group sums, which is where a Having would apply");
        Assert.Equal(5m, groups[1].Total, "the second group sums");
    }

    private static async Task FromSqlAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        await SeedOrderAsync(context, cancellationToken, "FS", ("x", 1m));

        var rows = await context.Orders
            .FromSql($"SELECT * FROM \"DM_FEATURE_ORDERS\" WHERE \"Number\" = {"FS"}")
            .Where(order => order.Id > 0)
            .OrderBy(order => order.Number)
            .ToListAsync(cancellationToken);

        Assert.Equal(1, rows.Count, "the parameterised raw query returns its row");
        Assert.Equal("FS", rows[0].Number, "the composed raw query returns the value written");
    }

    private static async Task RawCommandsAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        context.Add(new Note { Text = "seed" });
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var raw = await context.Database.ExecuteSqlRawAsync(
            "UPDATE \"DM_FEATURE_NOTES\" SET \"Text\" = 'raw' WHERE \"Text\" = 'seed'",
            cancellationToken);
        Assert.Equal(1, raw, "ExecuteSqlRaw affected the row");

        var interpolated = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"DM_FEATURE_NOTES\" SET \"Text\" = {"interpolated"} WHERE \"Text\" = {"raw"}",
            cancellationToken);
        Assert.Equal(1, interpolated, "ExecuteSqlInterpolated affected the row");

        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE \"DM_FEATURE_NOTES\" SET \"Text\" = 'command' WHERE \"Text\" = 'interpolated'";
        var fromCommand = await command.ExecuteNonQueryAsync(cancellationToken);
        Assert.Equal(1, fromCommand, "a raw DbCommand affected the row");

        Assert.Equal(1, await context.Notes.CountAsync(note => note.Text == "command", cancellationToken),
            "all three writes are visible through the context");
    }

    private static async Task TransactionMixAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        context.Add(new Note { Text = "tx" });
        await context.SaveChangesAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE \"DM_FEATURE_NOTES\" SET \"Text\" = 'tx2' WHERE \"Text\" = 'tx'",
            cancellationToken);

        Assert.Equal(1, await context.Notes.CountAsync(note => note.Text == "tx2", cancellationToken),
            "the raw write is visible inside the transaction");

        await transaction.RollbackAsync(cancellationToken);
        context.ChangeTracker.Clear();

        Assert.Equal(0, await context.Notes.CountAsync(cancellationToken),
            "the rollback undoes both the tracked and the raw write");
    }

    private static async Task ExecuteUpdateAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        await SeedOrderAsync(context, cancellationToken, "EU", ("a", 10m), ("b", 20m));

        var updated = await context.OrderLines
            .Where(line => line.Sku == "a")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(line => line.Price, 11m),
                cancellationToken);
        Assert.Equal(1, updated, "ExecuteUpdate reports one row affected");

        var read = await context.OrderLines.SingleAsync(line => line.Sku == "a", cancellationToken);
        Assert.Equal(11m, read.Price, "the updated number reached the server");

        var deleted = await context.OrderLines
            .Where(line => line.Sku == "b")
            .ExecuteDeleteAsync(cancellationToken);
        Assert.Equal(1, deleted, "ExecuteDelete reports one row affected");
        Assert.Equal(1, await context.OrderLines.CountAsync(cancellationToken), "only the other line is left");
    }

    /// <summary>
    /// A filtered entity's update carries the filter in the SQL. The update pipeline rewrites the
    /// filter's boolean the same way the select pipeline does, so the server receives a predicate
    /// rather than a value expression.
    /// </summary>
    private static async Task ExecuteUpdateWithFilterAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        context.Add(new Note { Text = "filtered" });
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var updated = await context.Notes
            .Where(note => note.Text == "filtered")
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(note => note.Score, 9),
                cancellationToken);

        Assert.Equal(1, updated, "the filtered update reports one row affected");
    }

    private static async Task NoTrackingAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        context.AddRange(new Note { Text = "one" }, new Note { Text = "two" });
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var first = await context.Notes.AsNoTracking().ToListAsync(cancellationToken);
        var second = await context.Notes.AsNoTracking().ToListAsync(cancellationToken);

        Assert.Equal(2, first.Count, "the no-tracking query returns the rows");
        var id = first[0].Id;
        Assert.True(!ReferenceEquals(first.Single(note => note.Id == id), second.Single(note => note.Id == id)),
            "no identity resolution: the same key is a different instance in each read");
    }

    private static readonly Func<FeatureContext, string, Task<Order?>> ByNumber =
        EF.CompileAsyncQuery((FeatureContext context, string number) =>
            context.Orders.FirstOrDefault(order => order.Number == number));

    private static async Task CompiledQueryAsync(FeatureContext context, CancellationToken cancellationToken)
    {
        await SeedOrderAsync(context, cancellationToken, "CQ", ("compiled", 1m));

        var first = await ByNumber(context, "CQ");
        var second = await ByNumber(context, "missing");

        Assert.NotNull(first, "the compiled query finds the row");
        Assert.Equal("CQ", first!.Number, "the compiled query returns the value written");
        Assert.True(second is null, "the compiled query returns null for a missing row");
    }
}

internal sealed class OrderSummary
{
    public string Number { get; set; } = string.Empty;

    public int Lines { get; set; }

    public decimal Total { get; set; }
}
