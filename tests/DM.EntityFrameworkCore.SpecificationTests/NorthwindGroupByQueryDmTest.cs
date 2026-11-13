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

    /// <summary>
    /// The statement has a scalar subquery inside an <c>OUTER APPLY</c> whose derived table references
    /// the outermost <c>Customers</c> row; Dameng answers <c>引用列未找到</c>. Measured on 8.1.4.80: the
    /// same shape with the reference narrowed to the <c>APPLY</c>'s own row answers. Recorded in
    /// <c>Skipped.md</c>.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: a reference two scopes out through a derived table inside an APPLY is not resolved")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Complex_query_with_groupBy_in_subquery4(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The framework reads an outer column inside a subquery's aggregate, which SQL Server accepts;
    /// Dameng requires the referenced column to be a grouping column and answers <c>不是 GROUP BY 表达式</c>.
    /// Measured on 8.1.4.80. Recorded in <c>Skipped.md</c>.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: an outer column inside a subquery aggregate must be a grouping column")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task GroupBy_aggregate_from_multiple_query_in_same_projection_2(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The framework places the outer query's aggregate in the subquery's predicate, which SQL Server
    /// accepts; Dameng answers <c>存在集函数</c>. Measured on 8.1.4.80. Recorded in <c>Skipped.md</c>.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: an outer aggregate in a subquery predicate is rejected")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task GroupBy_with_aggregate_containing_complex_where(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    public class NorthwindQueryDmFixture : NorthwindQueryDmFixtureBase
    {
    }
}
