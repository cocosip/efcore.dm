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
