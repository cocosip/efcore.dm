using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.BulkUpdates;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;
using Xunit.Abstractions;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The Northwind <c>BulkUpdates</c> cases of the specification suite.
/// </summary>
/// <remarks>
/// Cases that do not pass on this server or whose upstream result depends on a SQL Server shape are
/// marked skipped with the measurement recorded in <c>Skipped.md</c>.
/// </remarks>
public class NorthwindBulkUpdatesDmTest
    : NorthwindBulkUpdatesTestBase<NorthwindBulkUpdatesDmTest.NorthwindBulkUpdatesDmFixture>
{
    public NorthwindBulkUpdatesDmTest(
        NorthwindBulkUpdatesDmFixture fixture,
        ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }

    /// <summary>
    /// The upstream raw SQL names the SQL Server table <c>[Order Details]</c>; the model this fixture
    /// shares maps the same table as <c>OrderDetails</c>, so the case writes the command body against
    /// that name instead of marking a valid feature skipped.
    /// </summary>
    [ConditionalTheory]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Delete_FromSql_converted_to_subquery(bool async)
        => TestHelpers.ExecuteWithStrategyInTransactionAsync(
            () => Fixture.CreateContext(),
            (facade, transaction) => Fixture.UseTransaction(facade, transaction),
            async context =>
            {
                var queryable = context.Set<OrderDetail>().FromSqlRaw(
                    """
                    SELECT "OrderID", "ProductID", "UnitPrice", "Quantity", "Discount"
                    FROM "OrderDetails"
                    WHERE "OrderID" < 10300
                    """);

                if (async)
                {
                    await queryable.ExecuteDeleteAsync();
                }
                else
                {
                    queryable.ExecuteDelete();
                }
            });

    [ConditionalTheory(Skip = "Server limitation: a joined update source projected through a left join is not resolved")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Update_with_cross_join_left_join_set_constant(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = "Server limitation: APPLY in an update source is rejected")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Update_with_cross_join_cross_apply_set_constant(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    [ConditionalTheory(Skip = "Server limitation: OUTER APPLY in an update source is rejected")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Update_with_cross_join_outer_apply_set_constant(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// SQL Server's update generator coalesces the expression to an empty string; Dameng preserves the
    /// null, which is what this provider's update contract does for a nullable string.
    /// </summary>
    [ConditionalTheory(Skip = "Provider difference: the upstream assertion expects SQL Server's empty-string coalesce")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Update_Where_Join_set_property_from_joined_single_result_table(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// SQL Server's update generator coalesces the expression to an empty string; Dameng preserves the
    /// null, which is what this provider's update contract does for a nullable string.
    /// </summary>
    [ConditionalTheory(Skip = "Provider difference: the upstream assertion expects SQL Server's empty-string coalesce")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Update_Where_Join_set_property_from_joined_single_result_scalar(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    public class NorthwindBulkUpdatesDmFixture
        : NorthwindBulkUpdatesFixture<NoopModelCustomizer>
    {
        protected override ITestStoreFactory TestStoreFactory
            => DmTestStoreFactory.Instance;

        /// <summary>
        /// The bulk-update fixture drops and recreates the model's tables, including the keyless
        /// <c>CustomerQueries</c> set another fixture seeds for its cases. Refill it here so the
        /// serial run does not depend on which class ran first.
        /// </summary>
        protected override void Seed(NorthwindContext context)
        {
            base.Seed(context);

            context.Database.ExecuteSqlRaw(
                """
                INSERT INTO "CustomerQueries" ("CompanyName", "ContactName", "ContactTitle", "Address", "City")
                SELECT "CompanyName", "ContactName", "ContactTitle", "Address", "City" FROM "Customers"
                """);
        }
    }
}
