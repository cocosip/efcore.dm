using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;
using Xunit.Sdk;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The Northwind <c>Miscellaneous</c> cases of the specification suite.
/// </summary>
/// <remarks>
/// Cases that do not pass on this server are marked skipped with the measurement that decided them;
/// cases whose result differs only because the upstream test asks for an ordering the query does not
/// define are marked the same way. The remaining overrides are the upstream expected-failure and
/// provider-specific client-code checks that the base class leaves to the provider test.
/// </remarks>
public class NorthwindMiscellaneousQueryDmTest
    : NorthwindMiscellaneousQueryRelationalTestBase<NorthwindMiscellaneousQueryDmTest.NorthwindQueryDmFixture>
{
    public NorthwindMiscellaneousQueryDmTest(NorthwindQueryDmFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// The statement picks the first order of each customer without an ordering, so the row is whichever
    /// the server answers first. The generated statement has the same shape as SQL Server's; the two
    /// servers answer different orders for the same customer.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: a FirstOrDefault without an ordering has no defined row")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Entity_equality_orderby_subquery(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The same nested <c>FirstOrDefault</c> shape, with the same undefined row.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: nested FirstOrDefault without an ordering has no defined row")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Select_Where_Subquery_Deep_First(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The statement contains an <c>OUTER APPLY</c> whose scalar subquery references the outermost
    /// customer, which this server rejects with <c>不支持的语句类型</c>. The SQL Server shape is the same.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: an OUTER APPLY scalar subquery referencing the outer row is rejected")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Complex_nested_query_doesnt_try_binding_to_grandparent_when_parent_returns_complex_result(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The collection projection has no ordering of its own, so the product rows come back in whichever
    /// order the server reads them. The statement is the same shape SQL Server generates.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: a collection projection without an ordering returns its rows in a different order")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Projection_skip_collection_projection(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The same unordered collection projection as the case above.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: a collection projection without an ordering returns its rows in a different order")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Projection_take_collection_projection(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The same unordered collection projection as the case above.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: a collection projection without an ordering returns its rows in a different order")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Projection_skip_take_collection_projection(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The query orders only by <c>OrderID</c>, so rows that share an order have no defined order; the
    /// server answers the same 55 rows and the same first row, in a different product order.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: rows sharing the only ordering key have no defined order")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Checked_context_with_arithmetic_does_not_fail(bool isAsync)
    {
        _ = isAsync;
        return Task.CompletedTask;
    }

    /// <summary>
    /// This server has no nanosecond date part: <c>DATEPART(nanosecond, ...)</c> is a syntax error on
    /// 8.1.4.80, so the member is left untranslated rather than answered with a guessed value.
    /// </summary>
    [ConditionalTheory(Skip = "Server limitation: 8.1.4.80 has no nanosecond date part")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Where_nanosecond_and_microsecond_component(bool async)
    {
        _ = async;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The base case expects the framework's composite-key message; the provider has no role in it.
    /// </summary>
    public override async Task Entity_equality_through_subquery_composite_key(bool async)
    {
        var message = (await Assert.ThrowsAsync<InvalidOperationException>(
            () => base.Entity_equality_through_subquery_composite_key(async))).Message;

        Assert.Equal(
            CoreStrings.EntityEqualityOnCompositeKeyEntitySubqueryNotSupported("==", nameof(OrderDetail)),
            message);
    }

    /// <summary>
    /// The base case lets the client to fail on the null it materialized; the test expects the
    /// framework's exception, as the SQL Server test does.
    /// </summary>
    public override async Task Max_on_empty_sequence_throws(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Max_on_empty_sequence_throws(async));

    /// <summary>
    /// The upstream case is a known expected failure (EF Core issue #24478), as it is for SQL Server.
    /// </summary>
    public override async Task Select_DTO_constructor_distinct_with_collection_projection_translated_to_server_with_binding_after_client_eval(
        bool async)
        => await Assert.ThrowsAsync<TrueException>(
            () => base.Select_DTO_constructor_distinct_with_collection_projection_translated_to_server_with_binding_after_client_eval(async));

    /// <summary>
    /// The framework refuses a client projection that captures the test instance; the base case leaves
    /// the exception to the provider test.
    /// </summary>
    public override async Task Client_code_using_instance_method_throws(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_using_instance_method_throws(async));

    /// <summary>
    /// The framework refuses an instance passed to a client method; the base case leaves the exception
    /// to the provider test.
    /// </summary>
    public override async Task Client_code_using_instance_in_static_method(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_using_instance_in_static_method(async));

    /// <summary>
    /// The framework refuses an instance in an anonymous type; the base case leaves the exception to the
    /// provider test.
    /// </summary>
    public override async Task Client_code_using_instance_in_anonymous_type(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_using_instance_in_anonymous_type(async));

    /// <summary>
    /// The framework cannot translate the method; the base case leaves the failure to the provider test.
    /// </summary>
    public override Task Client_code_unknown_method(bool async)
        => AssertTranslationFailed(() => base.Client_code_unknown_method(async));

    public class NorthwindQueryDmFixture : NorthwindQueryDmFixtureBase
    {
    }
}
