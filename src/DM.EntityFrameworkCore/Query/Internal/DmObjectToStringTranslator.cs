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
        typeof(int),
        typeof(long),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(DateOnly),
        typeof(TimeOnly)
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
        if (instance is null
            || method.Name != nameof(ToString)
            || method.GetParameters().Length != 0
            || !FormattableTypes.Contains(method.DeclaringType ?? instance.Type))
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
