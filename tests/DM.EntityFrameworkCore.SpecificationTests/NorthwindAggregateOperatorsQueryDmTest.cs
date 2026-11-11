using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
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
    /// The framework fails this case for every relational provider: the local array of tuples is not a
    /// set a statement can compare against (EF Core issue #27785).
    /// </summary>
    public override Task Contains_with_local_tuple_array_closure(bool async)
        => AssertTranslationFailed(() => base.Contains_with_local_tuple_array_closure(async));

    /// <summary>
    /// The framework fails this case for every relational provider: the local array of anonymous types
    /// is not a set a statement can compare against (EF Core issue #27785).
    /// </summary>
    public override Task Contains_with_local_anonymous_type_array_closure(bool async)
        => AssertTranslationFailed(() => base.Contains_with_local_anonymous_type_array_closure(async));

    /// <summary>
    /// The framework fails this case for every relational provider: an inline enumerable is not a
    /// parameter or a set the pipeline can read (EF Core issue #31776).
    /// </summary>
    public override async Task Contains_with_local_enumerable_inline(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(
            () => base.Contains_with_local_enumerable_inline(async));

    /// <summary>
    /// The framework fails this case for every relational provider, for the same reason as the case
    /// above (EF Core issue #31776).
    /// </summary>
    public override async Task Contains_with_local_enumerable_inline_closure_mix(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(
            () => base.Contains_with_local_enumerable_inline_closure_mix(async));

    /// <summary>
    /// The case asserts an average the server computes to its own precision; SQL Server answers a
    /// different value, so its test asserts the failure rather than pinning a number (#34256).
    /// </summary>
    public override async Task Average_over_max_subquery(bool async)
        => await Assert.ThrowsAsync<EqualException>(() => base.Average_over_max_subquery(async));

    /// <summary>
    /// The server answers an average of nested averages to its own decimal scale, which differs from the
    /// client's value past the third decimal; the reference provider compares at that precision for the
    /// same reason.
    /// </summary>
    public override async Task Average_over_nested_subquery(bool async)
        => await AssertAverage(
            async,
            ss => ss.Set<Customer>().OrderBy(c => c.CustomerID).Take(3),
            selector: c => (decimal)c.Orders.Average(o => 5 + o.OrderDetails.Average(od => od.ProductID)),
            asserter: (e, a) => Assert.Equal(e, a, precision: 3));

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
