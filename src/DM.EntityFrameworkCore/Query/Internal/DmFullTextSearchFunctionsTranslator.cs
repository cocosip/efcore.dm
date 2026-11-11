using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Dm.Extensions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates <see cref="DmDbFunctionsExtensions.Contains" /> to Dameng's full-text <c>CONTAINS</c>.
/// </summary>
/// <remarks>
/// The first argument has to be a column, because the server matches a full-text index and indexes are
/// built on columns; anything else is refused while the query is translated rather than sent to fail.
/// The predicate is complete on its own, so it is not compared with a constant: measured on 8.1.4.80, the
/// server rejects a boolean value but accepts <c>CONTAINS(column, condition)</c> as a search condition.
/// </remarks>
internal sealed class DmFullTextSearchFunctionsTranslator : IMethodCallTranslator
{
    private static readonly MethodInfo ContainsMethod = typeof(DmDbFunctionsExtensions).GetRuntimeMethod(
        nameof(DmDbFunctionsExtensions.Contains),
        [typeof(DbFunctions), typeof(string), typeof(string)])!;

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmFullTextSearchFunctionsTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method != ContainsMethod)
        {
            return null;
        }

        if (arguments[1] is not ColumnExpression column)
        {
            throw new InvalidOperationException(
                $"{nameof(DmDbFunctionsExtensions.Contains)} needs a mapped column as its first argument, because "
                + "Dameng matches a full-text index and an index is built on a column.");
        }

        return _sqlExpressionFactory.Function(
            "CONTAINS",
            [column, _sqlExpressionFactory.ApplyTypeMapping(arguments[2], column.TypeMapping)],
            nullable: true,
            argumentsPropagateNullability: [false, false],
            typeof(bool),
            typeMapping: null);
    }
}
