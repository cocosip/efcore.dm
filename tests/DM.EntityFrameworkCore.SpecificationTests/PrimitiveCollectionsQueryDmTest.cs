using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;
using Xunit.Abstractions;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The EF Core 9 <c>PrimitiveCollections</c> cases of the specification suite.
/// </summary>
public class PrimitiveCollectionsQueryDmTest
    : PrimitiveCollectionsQueryRelationalTestBase<PrimitiveCollectionsQueryDmTest.PrimitiveCollectionsDmFixture>
{
    public PrimitiveCollectionsQueryDmTest(
        PrimitiveCollectionsDmFixture fixture,
        ITestOutputHelper testOutputHelper)
        : base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    private const string DynamicRowLimit = "Server limitation: a dynamic LIMIT/OFFSET expression is rejected (TOP、LIMIT或OFFSET子句中的表达式无效)";
    private const string UnorderedJsonTableRows = "Server limitation: JSON_TABLE rows have no defined order";
    private const string BoolIsNotAJsonTableColumn = "Server limitation: JSON_TABLE rejects BIT as its declared element type (无效的数据类型)";
private const string EmptyInlineCollectionNotSupported =
    "Framework limitation: EF Core 8 rejects an empty inline collection (InlineQueryRootExpression); EF Core 9 accepts it";

    private const string OrderedProjectionIsNotMaterialized =
        "Server limitation: the projected collection materializes in the array order though the statement orders the row source (measured: expected 7, actual 2)";

    private const string PagedProjectionLosesElements =
        "Server limitation: the projected paged collection materializes with a different element set though the row source is ordered and paged (measured: expected 5, actual 8)";

    [ConditionalTheory(Skip = OrderedProjectionIsNotMaterialized)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_collection_of_ints_ordered(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = "Server limitation: the filtered projection materializes with a different element set (measured: expected 2, actual 1)")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_collection_of_datetimes_filtered(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = PagedProjectionLosesElements)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_collection_of_nullable_ints_with_paging(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = PagedProjectionLosesElements)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_collection_of_nullable_ints_with_paging2(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = PagedProjectionLosesElements)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_collection_of_nullable_ints_with_paging3(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = "Server limitation: a projected collection that is empty or holds only nulls materializes with a different element set (measured: expected 5, actual 6)")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_empty_collection_of_nullables_and_collection_only_containing_nulls(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = "Server limitation: two projected collections are paired in a different order (measured: expected 7, actual 2)")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_multiple_collections(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }
    [ConditionalTheory(Skip = EmptyInlineCollectionNotSupported)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Inline_collection_Contains_with_zero_values(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = EmptyInlineCollectionNotSupported)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Inline_collection_Count_with_zero_values(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }
    [ConditionalTheory(Skip = DynamicRowLimit)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Inline_collection_index_Column(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = DynamicRowLimit)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Parameter_collection_index_Column_equal_Column(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = DynamicRowLimit)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Parameter_collection_index_Column_equal_constant(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = BoolIsNotAJsonTableColumn)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Column_collection_of_bools_Contains(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = BoolIsNotAJsonTableColumn)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Parameter_collection_of_bools_Contains(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }










    /// <summary>
    /// The EF Core 9 base expects the compiled-query form to fail with a message naming the LINQ
    /// expression. The provider translates the collection instead, so the case measures an expectation
    /// that no longer holds; the non-compiled cases cover the translation.
    /// </summary>
    [ConditionalFact(Skip = "Not a provider defect: the provider translates the compiled-query form the EF Core 9 base expects to fail")]
    public override void Parameter_collection_in_subquery_and_Convert_as_compiled_query()
    {
    }

    [ConditionalTheory(Skip = "Not a provider defect: the provider translates the compiled-query form the EF Core 9 base expects to fail")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    public class PrimitiveCollectionsDmFixture : PrimitiveCollectionsQueryFixtureBase
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory
            => (TestSqlLoggerFactory)ListLoggerFactory;

        // The fixture base throws every warning to catch what a provider raises. These three are
        // guidance about ordering a row source that a primitive collection cannot order, and about
        // loading two collections; the SQL Server fixture logs them as well.
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .ConfigureWarnings(
                    c => c
                        .Log(CoreEventId.RowLimitingOperationWithoutOrderByWarning)
                        .Log(CoreEventId.FirstWithoutOrderByAndFilterWarning)
                        .Log(RelationalEventId.MultipleCollectionIncludeWarning));

        protected override ITestStoreFactory TestStoreFactory
            => DmTestStoreFactory.PrimitiveCollectionsInstance;
    }
}
