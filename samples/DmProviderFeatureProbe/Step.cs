namespace DmProviderFeatureProbe;

/// <summary>One assertion of the probe: a name, an area and something that must hold.</summary>
internal sealed record Step(string Name, string Area, Func<FeatureContext, CancellationToken, Task> Run);

internal static class Steps
{
    /// <summary>
    /// Every step, in reading order. Names are unique; <c>--dry-run</c> fails on a duplicate because
    /// a duplicate would make a failure report ambiguous.
    /// </summary>
    public static IReadOnlyList<Step> All { get; } =
    [
        .. ModelSteps.All,
        .. TypeSteps.All,
        .. RelationshipSteps.All,
        .. SqlSteps.All
    ];
}

internal static class Assert
{
    public static void Equal<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{what}: expected <{expected}>, got <{actual}>");
        }
    }

    public static void True(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException(what);
        }
    }

    public static void NotNull(object? value, string what)
    {
        if (value is null)
        {
            throw new InvalidOperationException($"{what}: null");
        }
    }

    public static void SequenceEqual(byte[] expected, byte[]? actual, string what)
    {
        if (actual is null || !expected.AsSpan().SequenceEqual(actual))
        {
            throw new InvalidOperationException($"{what}: the byte array differs");
        }
    }

    public static async Task ThrowsAsync<TException>(Func<Task> action, string what)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"{what}: expected {typeof(TException).Name}, got {exception.GetType().Name}: {exception.Message}");
        }

        throw new InvalidOperationException($"{what}: expected {typeof(TException).Name}, nothing was thrown");
    }
}
