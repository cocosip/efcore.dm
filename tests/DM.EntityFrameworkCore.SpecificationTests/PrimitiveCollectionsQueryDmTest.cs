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
    public override Task Inline_collection_List_value_index_Column(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = DynamicRowLimit)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Inline_collection_value_index_Column(bool async)
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

    [ConditionalTheory(Skip = DynamicRowLimit)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Parameter_collection_with_type_inference_for_JsonScalarExpression(bool async)
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

    [ConditionalTheory(Skip = UnorderedJsonTableRows)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Column_collection_Take(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = UnorderedJsonTableRows)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Column_collection_Skip_Take(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = "Server limitation: JSON_TABLE does not preserve the array order the projection asserts")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_collection_of_ints_ordered(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = "Server limitation: JSON_TABLE row order makes the filtered projection differ")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_collection_of_datetimes_filtered(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = "Server limitation: JSON_TABLE paging has no defined order")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_collection_of_nullable_ints_with_paging(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = "Server limitation: JSON_TABLE paging has no defined order")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_collection_of_nullable_ints_with_paging2(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = "Server limitation: JSON_TABLE paging has no defined order")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_collection_of_nullable_ints_with_paging3(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = "Server limitation: JSON_TABLE null elements and paging have no defined order")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_empty_collection_of_nullables_and_collection_only_containing_nulls(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = "Server limitation: JSON_TABLE row order across two collections is not defined")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Project_multiple_collections(bool async)
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

    public class PrimitiveCollectionsDmFixture : PrimitiveCollectionsQueryFixtureBase, ITestSqlLoggerFactory
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
