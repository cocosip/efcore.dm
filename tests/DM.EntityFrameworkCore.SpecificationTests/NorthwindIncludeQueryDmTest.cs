using Microsoft.EntityFrameworkCore.Query;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The Northwind <c>Include</c> cases of the specification suite.
/// </summary>
/// <remarks>
/// The base model, the seed data and the expected results come from the shipped package; what this
/// project supplies is the store and the provider-specific model. A case that does not pass is
/// recorded in <c>Skipped.md</c> with the measurement that decided it.
/// </remarks>
public class NorthwindIncludeQueryDmTest : NorthwindIncludeQueryRelationalTestBase<NorthwindIncludeQueryDmTest.NorthwindQueryDmFixture>
{
    public NorthwindIncludeQueryDmTest(NorthwindQueryDmFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// The ordering key is a predicate over an empty list, so every row ties and the server answers the
    /// customers in an order the statement does not define: measured on 8.1.4.80, the first customer is
    /// <c>ALFKI</c> where the expected data starts with <c>ANATR</c>. The rows are the same set.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: ordering by an empty-list predicate leaves no ordering key")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Include_collection_OrderBy_empty_list_contains(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The same tie: the negated empty-list predicate is a constant, so the statement defines no order.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: ordering by an empty-list predicate leaves no ordering key")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Include_collection_OrderBy_empty_list_does_not_contains(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    public class NorthwindQueryDmFixture : NorthwindQueryDmFixtureBase
    {
    }
}
