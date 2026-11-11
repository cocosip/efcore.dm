using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

/// <summary>
/// The fixture every Northwind query set shares.
/// </summary>
/// <remarks>
/// The reference store is created from a script that also fills the keyless tables the model maps; this
/// store is created from the model, which leaves them empty, and the specification seed only adds the
/// entities that have a key. The keyless rows are therefore projected from the keyed ones here, the way
/// the script does. Without them a case that reads a keyless set answers no row, and one that reads it
/// while building the query under test fails before that query is translated.
/// </remarks>
public abstract class NorthwindQueryDmFixtureBase : NorthwindQueryRelationalFixture<NoopModelCustomizer>
{
    protected override ITestStoreFactory TestStoreFactory
        => DmTestStoreFactory.Instance;

    protected override async Task SeedAsync(NorthwindContext context)
    {
        await base.SeedAsync(context);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "CustomerQueries" ("CompanyName", "ContactName", "ContactTitle", "Address", "City")
            SELECT "CompanyName", "ContactName", "ContactTitle", "Address", "City" FROM "Customers"
            """);
    }
}
