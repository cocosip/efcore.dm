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

    protected override DbConnection CreateDbConnection() => new global::Dm.DmConnection(ConnectionString);
}
