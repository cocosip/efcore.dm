using System.Data.Common;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using DM.EntityFrameworkCore.Storage.Internal;

namespace DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

/// <summary>
/// The model factory the migrations suite scaffolds through: it reads the connection's current schema
/// when the caller names none, so the assertions see the suite's own tables rather than every schema the
/// database holds.
/// </summary>
/// <remarks>
/// The shipped factory scaffolds every accessible schema when no schema is named, which is right for
/// <c>dotnet ef dbcontext scaffold</c> and wrong for a suite that shares a database with other schemas.
/// </remarks>
internal sealed class DmSpecDatabaseModelFactory : DmDatabaseModelFactory
{
    public override DatabaseModel Create(DbConnection connection, DatabaseModelFactoryOptions options)
    {
        if (!options.Schemas.Any())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL";

            if (command.ExecuteScalar()?.ToString() is { Length: > 0 } schema)
            {
                // The current schema plus the named schemas the cases create tables in;
                // every other schema in the database belongs to an application the suite shares it with.
                string[] schemas = [schema, .. DmTestStore.MigrationsSchemas];
                options = new DatabaseModelFactoryOptions(options.Tables, schemas);
            }
        }

        return base.Create(connection, options);
    }
}
