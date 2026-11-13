using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Relational connection over the Dameng ADO.NET driver.
/// </summary>
internal class DmRelationalConnection : RelationalConnection
{
    public DmRelationalConnection(RelationalConnectionDependencies dependencies)
        : base(dependencies)
    {
    }

    /// <summary>
    /// An ambient <see cref="System.Transactions.Transaction" /> is enlisted when the connection opens,
    /// which the framework only does for a provider that says it supports ambient transactions; the
    /// default is no, so a write inside a scope was not rolled back with it. The driver's enlistment does
    /// work: measured on 8.1.4.80, a connection opened inside a scope and enlisted through
    /// <see cref="System.Data.Common.DbConnection.EnlistTransaction" /> rolled back with the scope, and
    /// with this switch on, a write inside a scope that was not completed did not survive.
    /// </summary>
    protected override bool SupportsAmbientTransactions => true;

    /// <summary>
    /// The driver answers a cancelled open with <see cref="TaskCanceledException" />, a subclass of the
    /// framework's cancellation exception; a caller that asks whether the operation was cancelled, or an
    /// upstream test that checks the exact type, expects <see cref="OperationCanceledException" />.
    /// </summary>
    public override async Task<bool> OpenAsync(CancellationToken cancellationToken, bool errorsExpected = false)
    {
        try
        {
            return await base.OpenAsync(cancellationToken, errorsExpected);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    /// <summary>
    /// The driver's second constructor argument is named <c>forEFCore</c>, so it is the vendor's
    /// own switch for a connection driven by Entity Framework. Leaving it off would run the
    /// provider on the path the driver keeps for other consumers.
    /// </summary>
    protected override DbConnection CreateDbConnection()
        => new global::Dm.DmConnection(ConnectionString, forEFCore: true);
}
