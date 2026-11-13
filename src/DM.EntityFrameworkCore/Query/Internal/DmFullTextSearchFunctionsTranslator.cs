using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Dm.Extensions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates the full-text methods of <see cref="DmDbFunctionsExtensions" /> to Dameng's <c>CONTAINS</c>
/// and <c>FREETEXT</c>.
/// </summary>
/// <remarks>
/// The first argument has to be a column, because the server matches a full-text index and indexes are
/// built on columns; anything else is refused while the query is translated rather than sent to fail.
/// The predicate is complete on its own, so it is not compared with a constant: measured on 8.1.4.80, the
/// server rejects a boolean value but accepts <c>CONTAINS(column, condition)</c> as a search condition.
/// <para>
/// <c>FREETEXT</c> and the language term are written for parity with the reference provider. Measured on
/// 8.1.4.80, <c>FREETEXT</c> is not resolved at all, so those forms need a server version that provides
/// them; a language term is written as a fragment, which is why it has to be a constant.
/// </para>
/// </remarks>
internal sealed class DmFullTextSearchFunctionsTranslator : IMethodCallTranslator
{
    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmFullTextSearchFunctionsTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        // The translator is asked about every method call, including List<T>.Contains and the LINQ
        // operators, which share the names below. Only this provider's extensions are full-text calls.
        if (method.DeclaringType != typeof(DmDbFunctionsExtensions))
        {
            return null;
        }

        var function = method.Name switch
        {
            nameof(DmDbFunctionsExtensions.Contains) => "CONTAINS",
            nameof(DmDbFunctionsExtensions.FreeText) => "FREETEXT",
            _ => null
        };

        if (function is null)
        {
            return null;
        }

        if (arguments[1] is not ColumnExpression column)
        {
            throw new InvalidOperationException(
                $"{nameof(DmDbFunctionsExtensions)}.{method.Name} needs a mapped column as its first argument, "
                + "because Dameng matches a full-text index and an index is built on a column.");
        }

        var functionArguments = new List<SqlExpression>
        {
            column,
            _sqlExpressionFactory.ApplyTypeMapping(arguments[2], column.TypeMapping)
        };

        if (arguments.Count == 4)
        {
            if (arguments[3] is not SqlConstantExpression language)
            {
                throw new InvalidOperationException(
                    $"The language term of {nameof(DmDbFunctionsExtensions)}.{method.Name} has to be a constant, "
                    + "because the server writes it into the statement instead of binding it as a parameter.");
            }

            functionArguments.Add(_sqlExpressionFactory.Fragment($"LANGUAGE {language.Value}"));
        }

        return _sqlExpressionFactory.Function(
            function,
            functionArguments,
            nullable: true,
            argumentsPropagateNullability: functionArguments.Select(_ => false),
            typeof(bool),
            typeMapping: null);
    }
}
