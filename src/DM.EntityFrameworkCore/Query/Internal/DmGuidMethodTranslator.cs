using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates <see cref="Guid.NewGuid" /> to the function Dameng generates an identifier with. The
/// value is the 36-character form a <see cref="Guid" /> is stored as, which is what the mapping
/// declares.
/// </summary>
/// <remarks>
/// The reference provider translates the same method to the same function, although Dameng also has
/// a <c>GUID()</c> that answers a 32-character string without the hyphens. Whether <c>NEWID()</c>
/// answers the hyphenated form is unverified against a server.
/// </remarks>
internal sealed class DmGuidMethodTranslator : IMethodCallTranslator
{
    private const string NewIdFunction = "NEWID";

    private static readonly MethodInfo NewGuidMethod = typeof(Guid).GetRuntimeMethod(
        nameof(Guid.NewGuid), [])!;

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmGuidMethodTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        => method == NewGuidMethod
            ? _sqlExpressionFactory.Function(
                NewIdFunction,
                [],
                nullable: false,
                argumentsPropagateNullability: [],
                typeof(Guid),
                typeMapping: null)
            : null;
}
