using Microsoft.EntityFrameworkCore.Query;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The Northwind <c>Navigations</c> cases of the specification suite.
/// </summary>
/// <remarks>
/// The base model, the seed data and the expected results come from the shipped package; what this
/// project supplies is the store and the provider-specific model. A case that does not pass is
/// recorded in <c>Skipped.md</c> with the measurement that decided it.
/// </remarks>
public class NorthwindNavigationsQueryDmTest
    : NorthwindNavigationsQueryRelationalTestBase<NorthwindNavigationsQueryDmTest.NorthwindQueryDmFixture>
{
    public NorthwindNavigationsQueryDmTest(NorthwindQueryDmFixture fixture)
        : base(fixture)
    {
    }

    public class NorthwindQueryDmFixture : NorthwindQueryDmFixtureBase
    {
    }
}
