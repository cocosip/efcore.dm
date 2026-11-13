using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;
using Xunit.Abstractions;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The EF Core 10 <c>PrimitiveCollections</c> cases of the specification suite.
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

    [ConditionalFact(Skip = "Translation gap: a collection parameter is not expanded into a JSON_TABLE row source")]
    public override Task Inline_collection_Contains_with_EF_Parameter()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Translation gap: a collection parameter is not expanded into a JSON_TABLE row source")]
    public override Task Inline_collection_Contains_with_IEnumerable_EF_Parameter()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Translation gap: a collection parameter is not expanded into a JSON_TABLE row source")]
    public override Task Inline_collection_Count_with_column_predicate_with_EF_Parameter()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Translation gap: a collection parameter is not expanded into a JSON_TABLE row source")]
    public override Task Parameter_collection_of_nullable_ints_Contains_nullable_int_with_EF_Parameter()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: a dynamic LIMIT/OFFSET expression is rejected (TOP、LIMIT或OFFSET子句中的表达式无效)")]
    public override Task Inline_collection_index_Column()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: a dynamic LIMIT/OFFSET expression is rejected (TOP、LIMIT或OFFSET子句中的表达式无效)")]
    public override Task Inline_collection_index_Column_with_EF_Constant()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: a dynamic LIMIT/OFFSET expression is rejected (TOP、LIMIT或OFFSET子句中的表达式无效)")]
    public override Task Inline_collection_List_value_index_Column()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: a dynamic LIMIT/OFFSET expression is rejected (TOP、LIMIT或OFFSET子句中的表达式无效)")]
    public override Task Inline_collection_value_index_Column()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: a dynamic LIMIT/OFFSET expression is rejected (TOP、LIMIT或OFFSET子句中的表达式无效)")]
    public override Task Parameter_collection_index_Column_equal_Column()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: a dynamic LIMIT/OFFSET expression is rejected (TOP、LIMIT或OFFSET子句中的表达式无效)")]
    public override Task Parameter_collection_index_Column_equal_constant()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: a dynamic LIMIT/OFFSET expression is rejected (TOP、LIMIT或OFFSET子句中的表达式无效)")]
    public override Task Parameter_collection_with_type_inference_for_JsonScalarExpression()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: JSON_TABLE rejects BIT as its declared element type (无效的数据类型)")]
    public override Task Column_collection_of_bools_Contains()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: JSON_TABLE rows have no ordering, so paging returns a different row")]
    public override Task Column_collection_Take()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: JSON_TABLE rows have no ordering, so paging returns a different row")]
    public override Task Column_collection_Skip_Take()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: JSON_TABLE rows have no ordering, so paging returns a different row")]
    public override Task Column_collection_in_subquery_Union_parameter_collection()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: JSON_TABLE does not preserve the array order the projection asserts")]
    public override Task Project_collection_of_ints_ordered()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: JSON_TABLE row order makes the filtered projection differ")]
    public override Task Project_collection_of_datetimes_filtered()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: JSON_TABLE paging has no defined order")]
    public override Task Project_collection_of_nullable_ints_with_paging()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: JSON_TABLE paging has no defined order")]
    public override Task Project_collection_of_nullable_ints_with_paging2()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: JSON_TABLE paging has no defined order")]
    public override Task Project_collection_of_nullable_ints_with_paging3()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: JSON_TABLE null elements and paging have no defined order")]
    public override Task Project_empty_collection_of_nullables_and_collection_only_containing_nulls()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: JSON_TABLE row order across two collections is not defined")]
    public override Task Project_multiple_collections()
        => Task.CompletedTask;

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
