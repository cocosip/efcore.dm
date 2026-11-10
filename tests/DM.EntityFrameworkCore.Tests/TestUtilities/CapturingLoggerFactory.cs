using Microsoft.Extensions.Logging;

namespace DM.EntityFrameworkCore.Tests.TestUtilities;

/// <summary>
/// Collects the formatted messages a context logs, so that a test can assert on a provider warning.
/// </summary>
/// <remarks>
/// A logger factory is used rather than <c>LogTo</c>, because the diagnostics logger the model
/// validator receives is created from the factory the options carry; a factory supplied here reaches
/// it.
/// </remarks>
internal sealed class CapturingLoggerFactory : ILoggerFactory
{
    private static readonly CapturingLoggerFactory Shared = new();

    private readonly List<string> _messages = [];
    private readonly object _gate = new();

    private CapturingLoggerFactory()
    {
    }

    /// <summary>The formatted messages logged since the last <see cref="Reset" />.</summary>
    public static IReadOnlyList<string> Messages
    {
        get
        {
            lock (Shared._gate)
            {
                return Shared._messages.ToArray();
            }
        }
    }

    /// <summary>The factory to hand to <c>UseLoggerFactory</c>.</summary>
    public static ILoggerFactory Instance => Shared;

    public static void Reset()
    {
        lock (Shared._gate)
        {
            Shared._messages.Clear();
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private void Add(string message)
    {
        lock (_gate)
        {
            _messages.Add(message);
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly CapturingLoggerFactory _factory;
        private readonly string _category;

        public CapturingLogger(CapturingLoggerFactory factory, string category)
        {
            _factory = factory;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _factory.Add($"{_category}|{eventId.Id}|{formatter(state, exception)}");
        }
    }
}
