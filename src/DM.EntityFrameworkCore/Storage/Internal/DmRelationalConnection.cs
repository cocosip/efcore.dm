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
    /// The driver's second constructor argument is named <c>forEFCore</c>, so it is the vendor's
    /// own switch for a connection driven by Entity Framework. Leaving it off would run the
    /// provider on the path the driver keeps for other consumers.
    /// </summary>
    protected override DbConnection CreateDbConnection()
        => new global::Dm.DmConnection(ConnectionString, forEFCore: true);
}
