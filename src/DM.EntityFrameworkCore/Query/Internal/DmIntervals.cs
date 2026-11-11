using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Builds the expressions that read an interval, which both the member translator and the method
/// translator need.
/// </summary>
/// <remarks>
/// Dameng reads the parts of an interval with <c>EXTRACT(PART FROM value)</c>, which is not a normal
/// function call shape: the first argument is a keyword, so it is passed as a fragment.
/// </remarks>
internal static class DmIntervals
{
    private const string ExtractFunction = "EXTRACT";

    /// <summary>
    /// The interval expressed in seconds, which is the sum over its parts. The fraction of a second is
    /// kept, because a total is not truncated, unlike the parts.
    /// </summary>
    public static SqlExpression Seconds(ISqlExpressionFactory sqlExpressionFactory, SqlExpression interval)
        => sqlExpressionFactory.Add(
            sqlExpressionFactory.Add(
                Scaled(sqlExpressionFactory, "DAY", interval, 86400.0),
                Scaled(sqlExpressionFactory, "HOUR", interval, 3600.0)),
            sqlExpressionFactory.Add(
                Scaled(sqlExpressionFactory, "MINUTE", interval, 60.0),
                sqlExpressionFactory.ApplyDefaultTypeMapping(Part(sqlExpressionFactory, "SECOND", interval))!));

    /// <summary>
    /// One part of the interval as a number, which is arithmetic over the parts rather than a count read
    /// straight from the interval.
    /// </summary>
    public static SqlExpression Part(ISqlExpressionFactory sqlExpressionFactory, string part, SqlExpression interval)
        => sqlExpressionFactory.Function(
            ExtractFunction,
            [sqlExpressionFactory.Fragment(part), interval],
            nullable: true,
            argumentsPropagateNullability: [false, true],
            typeof(double),
            typeMapping: null);

    private static SqlExpression Scaled(
        ISqlExpressionFactory sqlExpressionFactory,
        string part,
        SqlExpression interval,
        double factor)
        => sqlExpressionFactory.Multiply(
            sqlExpressionFactory.ApplyDefaultTypeMapping(Part(sqlExpressionFactory, part, interval))!,
            sqlExpressionFactory.Constant(factor));
}
