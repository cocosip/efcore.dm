using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    // The fixture base throws every warning to catch what a provider raises, and this one is guidance
    // about splitting a query that loads two collections rather than a defect of this provider, so it
    // is logged as the shipped fixtures log the other guidance warnings. It belongs here because every
    // set that includes more than one collection would otherwise have to repeat the override.
    public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        => base.AddOptions(builder)
            .ConfigureWarnings(c => c.Log(RelationalEventId.MultipleCollectionIncludeWarning));

    protected override void Seed(NorthwindContext context)
    {
        base.Seed(context);

        context.Database.ExecuteSqlRaw(
            """
            INSERT INTO "CustomerQueries" ("CompanyName", "ContactName", "ContactTitle", "Address", "City")
            SELECT "CompanyName", "ContactName", "ContactTitle", "Address", "City" FROM "Customers"
            """);
    }
}
