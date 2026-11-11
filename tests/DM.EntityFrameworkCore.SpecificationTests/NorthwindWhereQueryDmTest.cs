using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The Northwind <c>Where</c> cases of the specification suite.
/// </summary>
/// <remarks>
/// The base model, the seed data and the expected results come from the shipped package; what this
/// project supplies is the store and the provider-specific model. A case that does not pass is
/// recorded in <c>Skipped.md</c> with the measurement that decided it.
/// </remarks>
public class NorthwindWhereQueryDmTest : NorthwindWhereQueryRelationalTestBase<NorthwindWhereQueryDmTest.NorthwindQueryDmFixture>
{
    public NorthwindWhereQueryDmTest(NorthwindQueryDmFixture fixture)
        : base(fixture)
    {
    }

    // Comparing two constructed objects in a predicate is not translated by the relational pipeline, so
    // the case asserts the failure every relational provider produces (EF Core issue #14672). A provider
    // that translated it would answer rows here and fail the assertion.
    public override async Task Where_compare_constructed_equal(bool async)
        => await AssertTranslationFailed(() => base.Where_compare_constructed_equal(async));

    public override async Task Where_compare_constructed_multi_value_equal(bool async)
        => await AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_equal(async));

    public override async Task Where_compare_constructed_multi_value_not_equal(bool async)
        => await AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_not_equal(async));

    public override async Task Where_compare_tuple_constructed_equal(bool async)
        => await AssertTranslationFailed(() => base.Where_compare_tuple_constructed_equal(async));

    public override async Task Where_compare_tuple_constructed_multi_value_equal(bool async)
        => await AssertTranslationFailed(() => base.Where_compare_tuple_constructed_multi_value_equal(async));

    public override async Task Where_compare_tuple_constructed_multi_value_not_equal(bool async)
        => await AssertTranslationFailed(() => base.Where_compare_tuple_constructed_multi_value_not_equal(async));

    public override async Task Where_compare_tuple_create_constructed_equal(bool async)
        => await AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_equal(async));

    public override async Task Where_compare_tuple_create_constructed_multi_value_equal(bool async)
        => await AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_multi_value_equal(async));

    public override async Task Where_compare_tuple_create_constructed_multi_value_not_equal(bool async)
        => await AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_multi_value_not_equal(async));

    public class NorthwindQueryDmFixture : NorthwindQueryRelationalFixture<NoopModelCustomizer>
    {
        protected override ITestStoreFactory TestStoreFactory
            => DmTestStoreFactory.Instance;
    }
}
