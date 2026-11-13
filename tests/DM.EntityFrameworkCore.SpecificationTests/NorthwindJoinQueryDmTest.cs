using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The Northwind <c>Join</c> cases of the specification suite.
/// </summary>
/// <remarks>
/// The base model, the seed data and the expected results come from the shipped package; what this
/// project supplies is the store and the provider-specific model. A case that does not pass is
/// recorded in <c>Skipped.md</c> with the measurement that decided it.
/// </remarks>
public class NorthwindJoinQueryDmTest : NorthwindJoinQueryRelationalTestBase<NorthwindJoinQueryDmTest.NorthwindQueryDmFixture>
{
    public NorthwindJoinQueryDmTest(NorthwindQueryDmFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// The statement is the same nested <c>CROSS APPLY</c> the SQL Server test asserts, but Dameng does
    /// not preserve an ordering inside a subquery: the set of rows is the same and the first row of one
    /// customer differs. SQL gives the outer key as the whole ordering guarantee, so the case is
    /// inapplicable as written. Recorded in <c>Skipped.md</c>.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: Dameng does not order rows inside a subquery")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task SelectMany_with_selecting_outer_entity_column_and_inner_column(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    public class NorthwindQueryDmFixture : NorthwindQueryDmFixtureBase
    {
    }
}
