using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates the members of a byte array to Dameng SQL.
/// </summary>
/// <remarks>
/// The framework reaches a byte array column through the collection methods it normalizes the member
/// into: <c>Payload.Length > 0</c> and <c>Payload.Any()</c> arrive as <c>Enumerable.Any</c>, and
/// <c>Payload.Count()</c> arrives as <c>Enumerable.Count</c>. The member itself does not reach a member
/// translator, which is why this is a method translator.
/// <para>
/// <c>LENGTHB</c> rather than <c>LENGTH</c>: measured on 8.1.4.80, `LENGTH` of a `VARBINARY(64)` holding
/// four bytes answered 8, the length of the hexadecimal form the server stores, and the byte count only
/// for a `BLOB`, while `LENGTHB` answered 4 for both.
/// </para>
/// </remarks>
internal sealed class DmByteArrayMethodTranslator : IMethodCallTranslator
{
    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmByteArrayMethodTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        // A byte array that is a column uses a byte array type mapping; one the model declared as a
        // primitive collection uses the string mapping its JSON column carries, and means something else.
        if (arguments.Count != 1
            || arguments[0].TypeMapping is not ByteArrayTypeMapping
            || method.Name is not (nameof(Enumerable.Any) or nameof(Enumerable.Count)))
        {
            return null;
        }

        var length = _sqlExpressionFactory.Function(
            "LENGTHB",
            [arguments[0]],
            nullable: true,
            argumentsPropagateNullability: [true],
            typeof(int),
            typeMapping: null);

        return method.Name == nameof(Enumerable.Any)
            ? _sqlExpressionFactory.GreaterThan(length, _sqlExpressionFactory.Constant(0))
            : length;
    }
}
