using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates <see cref="string" /> methods to Dameng SQL. Function names follow Dameng's
/// default Oracle-compatible mode.
/// </summary>
internal sealed class DmStringMethodTranslator : IMethodCallTranslator
{
    private const string LikeEscapeCharacter = "\\";

    private static readonly MethodInfo ContainsMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Contains), [typeof(string)])!;

    private static readonly MethodInfo StartsWithMethod = typeof(string).GetRuntimeMethod(
        nameof(string.StartsWith), [typeof(string)])!;

    private static readonly MethodInfo EndsWithMethod = typeof(string).GetRuntimeMethod(
        nameof(string.EndsWith), [typeof(string)])!;

    private static readonly MethodInfo ToUpperMethod = typeof(string).GetRuntimeMethod(
        nameof(string.ToUpper), Type.EmptyTypes)!;

    private static readonly MethodInfo ToLowerMethod = typeof(string).GetRuntimeMethod(
        nameof(string.ToLower), Type.EmptyTypes)!;

    private static readonly MethodInfo TrimMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Trim), Type.EmptyTypes)!;

    private static readonly MethodInfo TrimStartMethod = typeof(string).GetRuntimeMethod(
        nameof(string.TrimStart), Type.EmptyTypes)!;

    private static readonly MethodInfo TrimEndMethod = typeof(string).GetRuntimeMethod(
        nameof(string.TrimEnd), Type.EmptyTypes)!;

    private static readonly MethodInfo ReplaceMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Replace), [typeof(string), typeof(string)])!;

    private static readonly MethodInfo SubstringOneArgumentMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Substring), [typeof(int)])!;

    private static readonly MethodInfo SubstringTwoArgumentMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Substring), [typeof(int), typeof(int)])!;

    private static readonly MethodInfo PadLeftMethod = typeof(string).GetRuntimeMethod(
        nameof(string.PadLeft), [typeof(int)])!;

    private static readonly MethodInfo PadLeftWithCharacterMethod = typeof(string).GetRuntimeMethod(
        nameof(string.PadLeft), [typeof(int), typeof(char)])!;

    private static readonly MethodInfo PadRightMethod = typeof(string).GetRuntimeMethod(
        nameof(string.PadRight), [typeof(int)])!;

    private static readonly MethodInfo PadRightWithCharacterMethod = typeof(string).GetRuntimeMethod(
        nameof(string.PadRight), [typeof(int), typeof(char)])!;

    private static readonly MethodInfo IndexOfMethod = typeof(string).GetRuntimeMethod(
        nameof(string.IndexOf), [typeof(string)])!;

    private static readonly MethodInfo IndexOfCharacterMethod = typeof(string).GetRuntimeMethod(
        nameof(string.IndexOf), [typeof(char)])!;

    private static readonly MethodInfo TrimStartCharacterMethod = typeof(string).GetRuntimeMethod(
        nameof(string.TrimStart), [typeof(char)])!;

    private static readonly MethodInfo TrimEndCharacterMethod = typeof(string).GetRuntimeMethod(
        nameof(string.TrimEnd), [typeof(char)])!;

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmStringMethodTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is null)
        {
            return null;
        }

        if (method == ContainsMethod)
        {
            return Like(instance, arguments[0], "%", "%");
        }

        if (method == StartsWithMethod)
        {
            return Like(instance, arguments[0], string.Empty, "%");
        }

        if (method == EndsWithMethod)
        {
            return Like(instance, arguments[0], "%", string.Empty);
        }

        if (method == ToUpperMethod || method == ToLowerMethod)
        {
            return StringFunction(method == ToUpperMethod ? "UPPER" : "LOWER", instance, [instance]);
        }

        if (method == TrimMethod || method == TrimStartMethod || method == TrimEndMethod)
        {
            var name = method == TrimMethod ? "TRIM" : method == TrimStartMethod ? "LTRIM" : "RTRIM";

            return StringFunction(name, instance, [instance]);
        }

        if (method == ReplaceMethod)
        {
            return StringFunction("REPLACE", instance, [instance, arguments[0], arguments[1]]);
        }

        // Dameng's SUBSTR is one-based; .NET's Substring is zero-based.
        if (method == SubstringOneArgumentMethod)
        {
            return StringFunction("SUBSTR", instance, [instance, OneBasedStart(arguments[0])]);
        }

        if (method == SubstringTwoArgumentMethod)
        {
            return StringFunction("SUBSTR", instance, [instance, OneBasedStart(arguments[0]), arguments[1]]);
        }

        if (method == TrimStartCharacterMethod || method == TrimEndCharacterMethod)
        {
            return StringFunction(
                method == TrimStartCharacterMethod ? "LTRIM" : "RTRIM",
                instance,
                [instance, arguments[0]]);
        }

        if (method == PadLeftMethod || method == PadLeftWithCharacterMethod)
        {
            return Pad("LPAD", instance, arguments);
        }

        if (method == PadRightMethod || method == PadRightWithCharacterMethod)
        {
            return Pad("RPAD", instance, arguments);
        }

        // Dameng's INSTR is one-based and answers zero where .NET's IndexOf is zero-based and answers
        // -1, so subtracting one moves both ends at once.
        if (method == IndexOfMethod || method == IndexOfCharacterMethod)
        {
            return _sqlExpressionFactory.Subtract(
                _sqlExpressionFactory.Function(
                    "INSTR",
                    [instance, arguments[0]],
                    nullable: true,
                    argumentsPropagateNullability: [true, true],
                    typeof(int),
                    typeMapping: null),
                _sqlExpressionFactory.Constant(1));
        }

        return null;
    }

    private SqlExpression Pad(string name, SqlExpression instance, IReadOnlyList<SqlExpression> arguments)
        => arguments.Count == 1
            ? StringFunction(name, instance, [instance, arguments[0]])
            : StringFunction(name, instance, [instance, arguments[0], arguments[1]]);

    /// <summary>
    /// Shifts a zero-based start index to Dameng's one-based <c>SUBSTR</c> index, folding the
    /// shift into the constant when the index is known.
    /// </summary>
    private SqlExpression OneBasedStart(SqlExpression startIndex)
        => startIndex is SqlConstantExpression { Value: int start }
            ? _sqlExpressionFactory.Constant(start + 1)
            : _sqlExpressionFactory.Add(startIndex, _sqlExpressionFactory.Constant(1));

    private SqlExpression Like(SqlExpression instance, SqlExpression argument, string prefix, string suffix)
        => _sqlExpressionFactory.Like(
            instance,
            Wildcards(argument, prefix, suffix),
            _sqlExpressionFactory.Constant(LikeEscapeCharacter));

    private SqlExpression Wildcards(SqlExpression argument, string prefix, string suffix)
    {
        if (argument is SqlConstantExpression { Value: string value })
        {
            return _sqlExpressionFactory.Constant(prefix + Escape(value) + suffix);
        }

        // CONCAT is used rather than the || operator so that the pattern does not depend on
        // operator precedence inside the LIKE operand.
        SqlExpression Concat(SqlExpression left, SqlExpression right)
            => _sqlExpressionFactory.Function(
                "CONCAT",
                [left, right],
                nullable: true,
                argumentsPropagateNullability: [true, true],
                typeof(string),
                left.TypeMapping);

        return Concat(Concat(_sqlExpressionFactory.Constant(prefix), argument), _sqlExpressionFactory.Constant(suffix));
    }

    private static string Escape(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private SqlExpression StringFunction(string name, SqlExpression instance, IReadOnlyList<SqlExpression> arguments)
        => _sqlExpressionFactory.Function(
            name,
            arguments,
            nullable: true,
            argumentsPropagateNullability: arguments.Select(_ => true).ToArray(),
            typeof(string),
            instance.TypeMapping);
}
