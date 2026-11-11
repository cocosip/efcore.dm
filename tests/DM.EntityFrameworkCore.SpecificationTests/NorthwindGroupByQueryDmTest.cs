using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The Northwind <c>GroupBy</c> cases of the specification suite.
/// </summary>
/// <remarks>
/// The base model, the seed data and the expected results come from the shipped package; what this
/// project supplies is the store and the provider-specific model. A case that does not pass is
/// recorded in <c>Skipped.md</c> with the measurement that decided it.
/// </remarks>
public class NorthwindGroupByQueryDmTest : NorthwindGroupByQueryRelationalTestBase<NorthwindGroupByQueryDmTest.NorthwindQueryDmFixture>
{
    public NorthwindGroupByQueryDmTest(NorthwindQueryDmFixture fixture)
        : base(fixture)
    {
    }

    public class NorthwindQueryDmFixture : NorthwindQueryRelationalFixture<NoopModelCustomizer>
    {
        protected override ITestStoreFactory TestStoreFactory
            => DmTestStoreFactory.Instance;

        // The fixture base throws every warning to catch what a provider raises, and this one is guidance
        // about splitting a query that loads two collections rather than a defect of this provider, so it
        // is logged as the shipped fixtures log the other guidance warnings.
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .ConfigureWarnings(c => c.Log(RelationalEventId.MultipleCollectionIncludeWarning));
    }
}
