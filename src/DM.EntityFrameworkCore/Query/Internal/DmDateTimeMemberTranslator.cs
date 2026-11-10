using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates date and time members to Dameng SQL.
/// </summary>
/// <remarks>
/// Dameng's Oracle-compatible mode reads date parts with <c>EXTRACT(PART FROM value)</c>, which
/// is not a normal function call shape: the first argument is a keyword, not an expression.
/// <see cref="DmQuerySqlGenerator" /> renders that shape.
/// </remarks>
internal sealed class DmDateTimeMemberTranslator : IMemberTranslator
{
    private const string ExtractFunction = "EXTRACT";

    private static readonly Dictionary<string, string> DateParts = new(StringComparer.Ordinal)
    {
        [nameof(DateTime.Year)] = "YEAR",
        [nameof(DateTime.Month)] = "MONTH",
        [nameof(DateTime.Day)] = "DAY",
        [nameof(DateTime.Hour)] = "HOUR",
        [nameof(DateTime.Minute)] = "MINUTE",
        [nameof(DateTime.Second)] = "SECOND"
    };

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmDateTimeMemberTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MemberInfo member,
        Type returnType,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is null
            || (instance.Type != typeof(DateTime) && instance.Type != typeof(DateTimeOffset))
            || !DateParts.TryGetValue(member.Name, out var part))
        {
            return null;
        }

        return _sqlExpressionFactory.Function(
            ExtractFunction,
            [_sqlExpressionFactory.Fragment(part), instance],
            nullable: true,
            argumentsPropagateNullability: [false, true],
            returnType,
            typeMapping: null);
    }
}
