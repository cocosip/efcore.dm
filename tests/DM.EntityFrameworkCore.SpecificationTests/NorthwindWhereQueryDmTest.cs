using Microsoft.EntityFrameworkCore.Query;
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

    /// <summary>
    /// The EF Core 9 base expects this comparison to stay untranslated, because that line has no
    /// translation for a <see cref="StringComparison" /> overload. This provider translates it with
    /// <c>UPPER</c> on both sides, so the expected exception is not thrown; the translation is covered by
    /// the string translation cases.
    /// </summary>
    [ConditionalTheory(Skip = "Not a provider defect: the provider translates the comparison the EF Core 9 base expects to fail")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Where_equals_method_string_with_ignore_case(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    public class NorthwindQueryDmFixture : NorthwindQueryDmFixtureBase
    {
    }
}
