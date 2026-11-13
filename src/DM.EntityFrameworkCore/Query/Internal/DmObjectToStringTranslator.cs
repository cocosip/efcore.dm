using System.Reflection;
using DM.EntityFrameworkCore.Dialect;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates <see cref="object.ToString" /> on the types Dameng can format to a string.
/// </summary>
/// <remarks>
/// <c>TO_CHAR</c> formats a number or a date without a declared width, so no column size has to
/// be invented. Types it cannot format are left untranslated rather than rendered with a width
/// that would silently truncate them.
/// </remarks>
internal sealed class DmObjectToStringTranslator : IMethodCallTranslator
{
    private const string ToStringFunction = "TO_CHAR";

    private static readonly HashSet<Type> FormattableTypes =
    [
        typeof(byte),
        typeof(sbyte),
        typeof(short),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(DateOnly),
        typeof(TimeOnly),

        // A Guid is stored as the 36-character text it formats to, so reading it as a string is a
        // format of the column rather than a conversion of it.
        typeof(Guid)
    ];


    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmObjectToStringTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        // The type is read from the instance rather than the method: EF Core reports the parameterless
        // call on a Guid as object.ToString, so the declaring type does not name the value being
        // formatted.
        if (instance is null
            || method.Name != nameof(ToString)
            || method.GetParameters().Length != 0
            || !FormattableTypes.Contains(instance.Type))
        {
            return null;
        }

        return _sqlExpressionFactory.Function(
            ToStringFunction,
            [instance],
            nullable: true,
            argumentsPropagateNullability: [true],
            typeof(string),
            typeMapping: null);
    }
}
