using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Dm.Extensions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates <see cref="DmDbFunctionsExtensions.IsDate" /> to Dameng's <c>ISDATE</c>.
/// </summary>
/// <remarks>
/// <c>ISDATE</c> answers with a flag rather than a predicate, and Dameng has no boolean column type, so
/// the call is compared with one instead of being used as a condition on its own.
/// </remarks>
internal sealed class DmIsDateFunctionTranslator : IMethodCallTranslator
{
    private static readonly MethodInfo IsDateMethod = typeof(DmDbFunctionsExtensions).GetRuntimeMethod(
        nameof(DmDbFunctionsExtensions.IsDate), [typeof(DbFunctions), typeof(string)])!;

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmIsDateFunctionTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        => method == IsDateMethod
            ? _sqlExpressionFactory.Equal(
                _sqlExpressionFactory.Function(
                    "ISDATE",
                    [arguments[1]],
                    nullable: true,
                    argumentsPropagateNullability: [true],
                    typeof(int),
                    typeMapping: null),
                _sqlExpressionFactory.Constant(1))
            : null;
}
