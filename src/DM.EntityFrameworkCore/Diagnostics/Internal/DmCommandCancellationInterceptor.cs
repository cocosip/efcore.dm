using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DM.EntityFrameworkCore.Diagnostics.Internal;

/// <summary>
/// Turns a cancelled token into <see cref="DbCommand.Cancel" /> for the asynchronous execution paths.
/// </summary>
/// <remarks>
/// <para>
/// <c>DM.DmProvider</c> inherits the cancellation-token overloads of <see cref="DbCommand" />, whose
/// default implementations run the command and ignore the token while it executes. Without a bridge a
/// cancelled query therefore stops nothing on the server: the caller gets an exception only after the
/// statement finishes.
/// </para>
/// <para>
/// The registration is held against the command weakly and released as soon as the command ends, so an
/// interceptor that lives as long as the internal service provider never retains a command. The
/// synchronous paths take no token and are not touched.
/// </para>
/// </remarks>
internal sealed class DmCommandCancellationInterceptor : DbCommandInterceptor
{
    private readonly ConditionalWeakTable<DbCommand, CommandRegistration> _registrations = new();

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A reader is cancelled while its rows are read as well, so its registration outlives this
        // event and is released when the reader closes instead.
        Register(command, cancellationToken);

        return new ValueTask<InterceptionResult<DbDataReader>>(result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Register(command, cancellationToken);

        return new ValueTask<InterceptionResult<object>>(result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Register(command, cancellationToken);

        return new ValueTask<InterceptionResult<int>>(result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Release(command);

        return new ValueTask<object?>(result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Release(command);

        return new ValueTask<int>(result);
    }

    public override Task CommandCanceledAsync(
        DbCommand command,
        CommandEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Release(command);

        return Task.CompletedTask;
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Release(command);

        return Task.CompletedTask;
    }

    public override InterceptionResult DataReaderClosing(
        DbCommand command,
        DataReaderClosingEventData eventData,
        InterceptionResult result)
    {
        Release(command);

        return result;
    }

    public override ValueTask<InterceptionResult> DataReaderClosingAsync(
        DbCommand command,
        DataReaderClosingEventData eventData,
        InterceptionResult result)
    {
        Release(command);

        return new ValueTask<InterceptionResult>(result);
    }

    public override InterceptionResult DataReaderDisposing(
        DbCommand command,
        DataReaderDisposingEventData eventData,
        InterceptionResult result)
    {
        Release(command);

        return result;
    }

    private void Register(DbCommand command, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || _registrations.TryGetValue(command, out _))
        {
            return;
        }

        _registrations.Add(command, new CommandRegistration(command, cancellationToken));
    }

    private void Release(DbCommand command)
    {
        if (_registrations.TryGetValue(command, out var registration))
        {
            _registrations.Remove(command);
            registration.Release();
        }
    }

    private sealed class CommandRegistration
    {
        private readonly CancellationTokenRegistration _registration;

        public CommandRegistration(DbCommand command, CancellationToken cancellationToken)
            => _registration = cancellationToken.Register(static state => ((DbCommand)state!).Cancel(), command);

        /// <summary>
        /// Releases the callback. A token that already fired has run it and released it, and disposing
        /// there would wait on the callback that is still unwinding this path.
        /// </summary>
        public void Release()
        {
            if (!_registration.Token.IsCancellationRequested)
            {
                _registration.Dispose();
            }
        }
    }
}
