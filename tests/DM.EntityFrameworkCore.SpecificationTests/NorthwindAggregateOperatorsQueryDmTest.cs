using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Sdk;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The Northwind <c>AggregateOperators</c> cases of the specification suite.
/// </summary>
/// <remarks>
/// The base model, the seed data and the expected results come from the shipped package; what this
/// project supplies is the store and the provider-specific model. A case that does not pass is
/// recorded in <c>Skipped.md</c> with the measurement that decided it.
/// </remarks>
public class NorthwindAggregateOperatorsQueryDmTest : NorthwindAggregateOperatorsQueryRelationalTestBase<NorthwindAggregateOperatorsQueryDmTest.NorthwindQueryDmFixture>
{
    public NorthwindAggregateOperatorsQueryDmTest(NorthwindQueryDmFixture fixture)
        : base(fixture)
    {
    }


    /// <summary>
    /// The framework fails this case for every relational provider: an inline enumerable is not a
    /// parameter or a set the pipeline can read (EF Core issue #31776).
    /// </summary>
    public override async Task Contains_with_local_enumerable_inline(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(
            () => base.Contains_with_local_enumerable_inline(async));

    /// <summary>
    /// The EF Core 9 base expects this shape to fail translation, as it does for every relational
    /// provider on that line (EF Core issue #31776). This provider translates a mixed inline and closure
    /// collection through <c>JSON_TABLE</c>, so the expected exception is not thrown; the translation is
    /// covered by the primitive-collection cases.
    /// </summary>
    [ConditionalTheory(Skip = "Not a provider defect: the provider translates the shape the EF Core 9 base expects to fail")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Contains_with_local_enumerable_inline_closure_mix(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// EF Core 8 hands the collection to its parameter processor, which answers with its own
    /// parameter-query-root message rather than the translation-failure message the 8 base asserts.
    /// The query is refused either way, so only the text differs. Recorded in <c>Skipped.md</c>.
    /// </summary>
    [ConditionalTheory(Skip = "Provider difference: EF Core 8 refuses the collection in the parameter processor, so only the message differs")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Contains_with_local_tuple_array_closure(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The same collection of an anonymous type, refused in the same place on this line.
    /// </summary>
    [ConditionalTheory(Skip = "Provider difference: EF Core 8 refuses the collection in the parameter processor, so only the message differs")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Contains_with_local_anonymous_type_array_closure(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The server answers an average of a nested average to its own decimal scale, which differs from
    /// the client's value past the third decimal; the reference provider compares at that precision for
    /// the same reason.
    /// </summary>
    public override async Task Average_over_nested_subquery_is_client_eval(bool async)
        => await AssertAverage(
            async,
            ss => ss.Set<Customer>().OrderBy(c => c.CustomerID).Take(3),
            selector: c => (decimal)c.Orders.Average(o => 5 + o.OrderDetails.Average(od => od.ProductID)),
            asserter: (e, a) => Assert.Equal(e, a, precision: 3));

    /// <summary>
    /// The server answers an average of a maximum to its own decimal scale, which differs from the
    /// client's value past the third decimal, as the nested case does.
    /// </summary>
    public override async Task Average_over_max_subquery_is_client_eval(bool async)
        => await AssertAverage(
            async,
            ss => ss.Set<Customer>().OrderBy(c => c.CustomerID).Take(3),
            selector: c => (decimal)c.Orders.Average(o => 5 + o.OrderDetails.Max(od => od.ProductID)),
            asserter: (e, a) => Assert.Equal(e, a, precision: 3));
    /// <summary>
    /// The statement composes a chained navigation into an <c>OUTER APPLY</c> whose scalar subqueries
    /// reference the outermost row; Dameng answers <c>不支持的语句类型</c>. Measured on 8.1.4.80: the same
    /// scalar subquery in a plain subquery answers, while inside the <c>APPLY</c> it does not. Recorded in
    /// <c>Skipped.md</c>.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: an OUTER APPLY whose scalar subqueries reference the outermost row is rejected")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Multiple_collection_navigation_with_FirstOrDefault_chained(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    public class NorthwindQueryDmFixture : NorthwindQueryDmFixtureBase
    {
        // The fixture base throws every warning to catch what a provider raises, and this one is guidance
        // about splitting a query that loads two collections rather than a defect of this provider, so it
        // is logged as the shipped fixtures log the other guidance warnings.
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .ConfigureWarnings(c => c.Log(RelationalEventId.MultipleCollectionIncludeWarning));
    }
}
