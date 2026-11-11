using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The Northwind <c>Select</c> cases of the specification suite.
/// </summary>
/// <remarks>
/// The base model, the seed data and the expected results come from the shipped package; what this
/// project supplies is the store and the provider-specific model. A case that does not pass is
/// recorded in <c>Skipped.md</c> with the measurement that decided it.
/// </remarks>
public class NorthwindSelectQueryDmTest : NorthwindSelectQueryRelationalTestBase<NorthwindSelectQueryDmTest.NorthwindQueryDmFixture>
{
    public NorthwindSelectQueryDmTest(NorthwindQueryDmFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// The case projects a property the model does not map, so the framework has no SQL for it and
    /// fails the way the reference provider expects.
    /// </summary>
    public override async Task SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(
        bool async)
        => await AssertUnableToTranslateEFProperty(
            () => base.SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(async));

    /// <summary>
    /// The framework fails this case for every provider: a constructor's arguments are not a query
    /// source, so the projection cannot be translated.
    /// </summary>
    public override Task Member_binding_after_ctor_arguments_fails_with_client_eval(bool async)
        => AssertTranslationFailed(() => base.Member_binding_after_ctor_arguments_fails_with_client_eval(async));

    /// <summary>
    /// The framework fails this case for every relational provider: the projection before
    /// <c>Distinct</c> does not carry the key that identifies the collection's element (EF Core issue
    /// #24440).
    /// </summary>
    public override async Task Correlated_collection_after_distinct_with_complex_projection_not_containing_original_identifier(bool async)
        => Assert.Equal(
            RelationalStrings.InsufficientInformationToIdentifyElementOfCollectionJoin,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Correlated_collection_after_distinct_with_complex_projection_not_containing_original_identifier(async))).Message);

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
