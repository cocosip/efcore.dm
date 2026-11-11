using System.Reflection;
using DM.EntityFrameworkCore.Dialect;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates the single argument <see cref="Convert" /> methods to Dameng <c>CAST</c>.
/// </summary>
/// <remarks>
/// Dameng spells conversion as <c>CAST(value AS type)</c>, which is not a function call shape:
/// <see cref="DmQuerySqlGenerator" /> renders it. The store type names come from the dialect
/// layer, so a conversion and a column of the same type cannot drift apart.
/// </remarks>
internal sealed class DmConvertTranslator : IMethodCallTranslator
{
    /// <summary>
    /// The function name the SQL generator recognises as a cast. It is not valid SQL on its own.
    /// </summary>
    public const string CastFunction = "CAST";

    private const string ToStringFunction = "TO_CHAR";

    private static readonly Dictionary<string, string> CastTypes = new(StringComparer.Ordinal)
    {
        [nameof(Convert.ToSByte)] = DmStoreTypes.TinyInt,
        [nameof(Convert.ToByte)] = DmStoreTypes.SmallInt,
        [nameof(Convert.ToInt16)] = DmStoreTypes.SmallInt,
        [nameof(Convert.ToInt32)] = DmStoreTypes.Int,
        [nameof(Convert.ToInt64)] = DmStoreTypes.BigInt,
        [nameof(Convert.ToSingle)] = DmStoreTypes.Real,
        [nameof(Convert.ToDouble)] = DmStoreTypes.Double,
        [nameof(Convert.ToDecimal)] = DmStoreTypes.Decimal
            + DmTypeSizes.FormatDecimal(DmTypeSizes.DefaultDecimalPrecision, DmTypeSizes.DefaultDecimalScale),
        [nameof(Convert.ToBoolean)] = DmStoreTypes.Bit,
        [nameof(Convert.ToDateTime)] = DmStoreTypes.Timestamp
    };

    private static readonly HashSet<MethodInfo> SupportedMethods = typeof(Convert)
        .GetTypeInfo()
        .DeclaredMethods
        .Where(m => m.GetParameters() is [{ ParameterType: var parameter }] && IsConvertible(parameter))
        .Where(m => m.Name == nameof(Convert.ToString) || CastTypes.ContainsKey(m.Name))
        .ToHashSet();

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmConvertTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is not null || !SupportedMethods.Contains(method))
        {
            return null;
        }

        // String conversion has no target type to cast to, so it formats instead, which needs no
        // declared width.
        if (method.Name == nameof(Convert.ToString))
        {
            return Function(ToStringFunction, [arguments[0]], method);
        }

        return Function(CastFunction, [arguments[0], _sqlExpressionFactory.Fragment(CastTypes[method.Name])], method);
    }

    private static bool IsConvertible(Type type)
        => type == typeof(bool)
            || type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(int)
            || type == typeof(long)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal)
            || type == typeof(string)
            || type == typeof(DateTime);

    private SqlExpression Function(string name, IReadOnlyList<SqlExpression> arguments, MethodInfo method)
        => _sqlExpressionFactory.Function(
            name,
            arguments,
            nullable: true,
            argumentsPropagateNullability: DmSqlFunctions.PropagatesNullability(arguments),
            method.ReturnType,
            typeMapping: null);
}
