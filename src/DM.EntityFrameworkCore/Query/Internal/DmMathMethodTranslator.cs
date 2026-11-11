using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates <see cref="Math" /> calls to Dameng SQL. Function names follow Dameng's default
/// Oracle-compatible mode.
/// </summary>
internal sealed class DmMathMethodTranslator : IMethodCallTranslator
{
    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    private readonly IRelationalTypeMappingSource _typeMappingSource;

    public DmMathMethodTranslator(
        ISqlExpressionFactory sqlExpressionFactory,
        IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _typeMappingSource = typeMappingSource;
    }

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is not null || method.DeclaringType != typeof(Math))
        {
            return null;
        }

        return method.Name switch
        {
            nameof(Math.Abs) => Unary("ABS", arguments, method),
            nameof(Math.Acos) => Unary("ACOS", arguments, method),
            nameof(Math.Asin) => Unary("ASIN", arguments, method),
            nameof(Math.Atan) => Unary("ATAN", arguments, method),
            nameof(Math.Ceiling) => Unary("CEIL", arguments, method),
            nameof(Math.Cos) => Unary("COS", arguments, method),
            nameof(Math.Exp) => Unary("EXP", arguments, method),
            nameof(Math.Floor) => Unary("FLOOR", arguments, method),
            nameof(Math.Sign) => Unary("SIGN", arguments, method),
            nameof(Math.Sin) => Unary("SIN", arguments, method),
            nameof(Math.Sqrt) => Unary("SQRT", arguments, method),
            nameof(Math.Tan) => Unary("TAN", arguments, method),
            nameof(Math.Truncate) => Unary("TRUNC", arguments, method),

            nameof(Math.Pow) => Function("POWER", arguments, method),
            nameof(Math.Atan2) => Function("ATAN2", arguments, method),
            nameof(Math.Clamp) => Function(
                "GREATEST",
                [Function("LEAST", [arguments[0], arguments[2]], method), arguments[1]],
                method),
            nameof(Math.Round) => Round(arguments, method),
            nameof(Math.Log) => Log(arguments, method),
            nameof(Math.Log10) => Function("LOG", [Constant(10), arguments[0]], method),
            _ => null
        };
    }

    /// <summary>
    /// Dameng rounds with <c>ROUND(value)</c> or <c>ROUND(value, digits)</c>, and it rounds halves
    /// away from zero, which is the midpoint strategy of that name. Rounding to even has no single
    /// function, so it is left untranslated rather than answered with the other strategy.
    /// </summary>
    private SqlExpression? Round(IReadOnlyList<SqlExpression> arguments, MethodInfo method)
    {
        var rounding = arguments.Count == 1
            ? null
            : arguments[1].Type == typeof(MidpointRounding)
                ? arguments[1]
                : arguments.Count == 3
                    ? arguments[2]
                    : null;

        if (rounding is not null && rounding is not SqlConstantExpression { Value: MidpointRounding.AwayFromZero })
        {
            return null;
        }

        return arguments.Count >= 2 && arguments[1].Type == typeof(int)
            ? Function("ROUND", [arguments[0], arguments[1]], method)
            : Unary("ROUND", [arguments[0]], method);
    }

    /// <summary>
    /// Dameng's natural logarithm is <c>LN</c>, and a logarithm to a base is
    /// <c>LOG(base, value)</c> — the base comes first, unlike <see cref="Math.Log(double, double)" />.
    /// </summary>
    private SqlExpression Log(IReadOnlyList<SqlExpression> arguments, MethodInfo method)
        => arguments.Count == 1
            ? Unary("LN", arguments, method)
            : Function("LOG", [arguments[1], arguments[0]], method);

    private SqlExpression Unary(string name, IReadOnlyList<SqlExpression> arguments, MethodInfo method)
        => Function(name, [arguments[0]], method);


    /// <summary>
    /// The function carries the mapping of the type the method returns, so an argument that is a
    /// parameter cannot leave the function without one.
    /// </summary>
    private SqlExpression Function(string name, IReadOnlyList<SqlExpression> arguments, MethodInfo method)
        => _sqlExpressionFactory.Function(
            name,
            arguments,
            nullable: true,
            argumentsPropagateNullability: arguments.Select(_ => true).ToArray(),
            method.ReturnType,
            _typeMappingSource.FindMapping(method.ReturnType));

    private SqlExpression Constant(int value) => _sqlExpressionFactory.Constant(value);
}
