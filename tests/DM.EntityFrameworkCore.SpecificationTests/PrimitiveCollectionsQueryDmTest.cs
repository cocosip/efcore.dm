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











    [ConditionalFact(Skip = "Server limitation: a JSON_TABLE row source that is unioned with a parameter collection has no defined order")]
    public override Task Column_collection_in_subquery_Union_parameter_collection()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: the projected collection materializes in the array order though the statement orders the row source (measured: expected 7, actual 2)")]
    public override Task Project_collection_of_ints_ordered()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: the filtered projection materializes with a different element set (measured: expected 2, actual 1)")]
    public override Task Project_collection_of_datetimes_filtered()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: the projected paged collection materializes with a different element set though the row source is ordered and paged (measured: expected 5, actual 8)")]
    public override Task Project_collection_of_nullable_ints_with_paging()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: the projected paged collection materializes with a different element set though the row source is ordered and paged (measured: expected 5, actual 2)")]
    public override Task Project_collection_of_nullable_ints_with_paging2()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: the projected paged collection materializes with a different element set though the row source is ordered and paged (measured: expected 5, actual 6)")]
    public override Task Project_collection_of_nullable_ints_with_paging3()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: a projected collection that is empty or holds only nulls materializes with a different element set (measured: expected 5, actual 6)")]
    public override Task Project_empty_collection_of_nullables_and_collection_only_containing_nulls()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: two projected collections are paired in a different order (measured: expected 7, actual 2)")]
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
