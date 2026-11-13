using System.Linq.Expressions;
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

    private static readonly MethodInfo ReplaceCharacterMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Replace), [typeof(char), typeof(char)])!;

    private static readonly MethodInfo LastIndexOfMethod = typeof(string).GetRuntimeMethod(
        nameof(string.LastIndexOf), [typeof(string)])!;

    private static readonly MethodInfo LastIndexOfCharacterMethod = typeof(string).GetRuntimeMethod(
        nameof(string.LastIndexOf), [typeof(char)])!;

    private static readonly MethodInfo LastIndexOfWithComparisonMethod = typeof(string).GetRuntimeMethod(
        nameof(string.LastIndexOf), [typeof(string), typeof(StringComparison)])!;

    private static readonly MethodInfo LastIndexOfCharacterWithStartMethod = typeof(string).GetRuntimeMethod(
        nameof(string.LastIndexOf), [typeof(char), typeof(int)])!;

    private static readonly MethodInfo LastIndexOfWithStartMethod = typeof(string).GetRuntimeMethod(
        nameof(string.LastIndexOf), [typeof(string), typeof(int)])!;

    private static readonly MethodInfo IndexOfCharacterWithStartMethod = typeof(string).GetRuntimeMethod(
        nameof(string.IndexOf), [typeof(char), typeof(int)])!;

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

    private static readonly MethodInfo IsNullOrWhiteSpaceMethod = typeof(string).GetRuntimeMethod(
        nameof(string.IsNullOrWhiteSpace), [typeof(string)])!;

    private static readonly MethodInfo RemoveMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Remove), [typeof(int)])!;

    private static readonly MethodInfo RemoveCountMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Remove), [typeof(int), typeof(int)])!;

    private static readonly MethodInfo InsertMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Insert), [typeof(int), typeof(string)])!;

    private static readonly MethodInfo EqualsWithComparisonMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Equals), [typeof(string), typeof(StringComparison)])!;

    private static readonly MethodInfo StaticEqualsWithComparisonMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Equals), [typeof(string), typeof(string), typeof(StringComparison)])!;

    private static readonly MethodInfo ContainsWithComparisonMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Contains), [typeof(string), typeof(StringComparison)])!;

    private static readonly MethodInfo StartsWithWithComparisonMethod = typeof(string).GetRuntimeMethod(
        nameof(string.StartsWith), [typeof(string), typeof(StringComparison)])!;

    private static readonly MethodInfo EndsWithWithComparisonMethod = typeof(string).GetRuntimeMethod(
        nameof(string.EndsWith), [typeof(string), typeof(StringComparison)])!;

    private static readonly MethodInfo IndexOfWithComparisonMethod = typeof(string).GetRuntimeMethod(
        nameof(string.IndexOf), [typeof(string), typeof(StringComparison)])!;

    private static readonly MethodInfo ContainsCharacterMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Contains), [typeof(char)])!;

    private static readonly MethodInfo ToUpperInvariantMethod = typeof(string).GetRuntimeMethod(
        nameof(string.ToUpperInvariant), [])!;

    private static readonly MethodInfo ToLowerInvariantMethod = typeof(string).GetRuntimeMethod(
        nameof(string.ToLowerInvariant), [])!;

    private static readonly MethodInfo TrimStartCharactersMethod = typeof(string).GetRuntimeMethod(
        nameof(string.TrimStart), [typeof(char[])])!;

    private static readonly MethodInfo TrimEndCharactersMethod = typeof(string).GetRuntimeMethod(
        nameof(string.TrimEnd), [typeof(char[])])!;

    private static readonly MethodInfo TrimCharactersMethod = typeof(string).GetRuntimeMethod(
        nameof(string.Trim), [typeof(char[])])!;

    private static readonly MethodInfo IndexOfWithStartMethod = typeof(string).GetRuntimeMethod(
        nameof(string.IndexOf), [typeof(string), typeof(int)])!;

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmStringMethodTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method == IsNullOrWhiteSpaceMethod)
        {
            // An empty string is a value of its own in one Dameng mode and NULL in another, so both
            // are tested: the one that does not hold never matches.
            var trimmed = StringFunction("TRIM", arguments[0], [arguments[0]]);

            return _sqlExpressionFactory.OrElse(
                _sqlExpressionFactory.IsNull(arguments[0]),
                _sqlExpressionFactory.OrElse(
                    _sqlExpressionFactory.IsNull(trimmed),
                    _sqlExpressionFactory.Equal(trimmed, _sqlExpressionFactory.Constant(string.Empty))));
        }

        // A comparison argument decides whether the values are upper-cased before they are compared;
        // the culture-sensitive comparisons differ from that in ways a database collation cannot state.
        if (method == EqualsWithComparisonMethod || method == StaticEqualsWithComparisonMethod)
        {
            if (Comparison(arguments) is not { } comparison)
            {
                return null;
            }

            var left = instance ?? arguments[0];
            var right = instance is null ? arguments[1] : arguments[0];

            return _sqlExpressionFactory.Equal(Ignoring(left, comparison), Ignoring(right, comparison));
        }

        if (instance is null)
        {
            return null;
        }

        if (method == ContainsMethod)
        {
            return Like(instance, arguments[0], "%", "%");
        }

        if (method == ContainsCharacterMethod)
        {
            return _sqlExpressionFactory.GreaterThan(IndexOf(instance, arguments[0]), _sqlExpressionFactory.Constant(0));
        }

        if (method == ContainsWithComparisonMethod || method == StartsWithWithComparisonMethod
            || method == EndsWithWithComparisonMethod || method == IndexOfWithComparisonMethod)
        {
            if (Comparison(arguments) is not { } comparison)
            {
                return null;
            }

            var subject = Ignoring(instance, comparison);
            var value = Ignoring(arguments[0], comparison);

            if (method == IndexOfWithComparisonMethod)
            {
                return _sqlExpressionFactory.Subtract(IndexOf(subject, value), _sqlExpressionFactory.Constant(1));
            }

            return method == ContainsWithComparisonMethod
                ? Like(subject, value, "%", "%")
                : method == StartsWithWithComparisonMethod
                    ? Like(subject, value, string.Empty, "%")
                    : Like(subject, value, "%", string.Empty);
        }

        if (method == LastIndexOfWithComparisonMethod)
        {
            if (Comparison(arguments) is not { } comparison)
            {
                return null;
            }

            return LastIndexOf(Ignoring(instance, comparison), Ignoring(arguments[0], comparison), null);
        }

        if (method == StartsWithMethod)
        {
            return Like(instance, arguments[0], string.Empty, "%");
        }

        if (method == EndsWithMethod)
        {
            return Like(instance, arguments[0], "%", string.Empty);
        }

        if (method == ToUpperMethod || method == ToUpperInvariantMethod)
        {
            return StringFunction("UPPER", instance, [instance]);
        }

        if (method == ToLowerMethod || method == ToLowerInvariantMethod)
        {
            return StringFunction("LOWER", instance, [instance]);
        }

        if (method == TrimMethod || method == TrimStartMethod || method == TrimEndMethod)
        {
            var name = method == TrimMethod ? "TRIM" : method == TrimStartMethod ? "LTRIM" : "RTRIM";

            return StringFunction(name, instance, [instance]);
        }

        if (method == ReplaceMethod || method == ReplaceCharacterMethod)
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
            return _sqlExpressionFactory.Subtract(IndexOf(instance, arguments[0]), _sqlExpressionFactory.Constant(1));
        }

        if (method == TrimStartCharactersMethod || method == TrimEndCharactersMethod)
        {
            if (arguments[0] is not SqlConstantExpression { Value: char[] characters })
            {
                return null;
            }

            return StringFunction(
                method == TrimStartCharactersMethod ? "LTRIM" : "RTRIM",
                instance,
                [instance, _sqlExpressionFactory.Constant(new string(characters))]);
        }

        // TRIM takes one character, so a set of them is taken off from both ends by the pair that
        // takes a set.
        if (method == TrimCharactersMethod)
        {
            if (arguments[0] is not SqlConstantExpression { Value: char[] set })
            {
                return null;
            }

            var trimmed = StringFunction(
                "LTRIM",
                instance,
                [instance, _sqlExpressionFactory.Constant(new string(set))]);

            return StringFunction("RTRIM", instance, [trimmed, _sqlExpressionFactory.Constant(new string(set))]);
        }

        // The position .NET takes is zero-based, and the answer is still measured from the start of
        // the string, which is where INSTR starts counting when it is moved one place.
        if (method == IndexOfWithStartMethod || method == IndexOfCharacterWithStartMethod)
        {
            return _sqlExpressionFactory.Subtract(
                IndexOf(instance, arguments[0], OneBasedStart(arguments[1])),
                _sqlExpressionFactory.Constant(1));
        }

        if (method == LastIndexOfMethod || method == LastIndexOfCharacterMethod)
        {
            return LastIndexOf(instance, arguments[0], null);
        }

        if (method == LastIndexOfCharacterWithStartMethod || method == LastIndexOfWithStartMethod)
        {
            return LastIndexOf(instance, arguments[0], arguments[1]);
        }

        // Insert and Remove keep the part in front of the index and join the part behind it back on.
        if (method == RemoveMethod)
        {
            return Leading(instance, arguments[0]);
        }

        if (method == RemoveCountMethod)
        {
            return Concat(
                Leading(instance, arguments[0]),
                Trailing(instance, Sum(arguments[0], arguments[1])));
        }

        if (method == InsertMethod)
        {
            return Concat(Leading(instance, arguments[0]), arguments[1], Trailing(instance, arguments[0]));
        }

        return null;
    }

    /// <summary>
    /// <c>SUBSTR</c> is one-based and counts characters, so every index is shifted by one.
    /// </summary>
    private SqlExpression Leading(SqlExpression instance, SqlExpression count)
        => StringFunction("SUBSTR", instance, [instance, _sqlExpressionFactory.Constant(1), count]);

    private SqlExpression Trailing(SqlExpression instance, SqlExpression startIndex)
        => StringFunction("SUBSTR", instance, [instance, OneBasedStart(startIndex)]);

    /// <summary>
    /// A literal pair is added here, so the statement reads <c>SUBSTR(x, 6)</c> rather than
    /// <c>SUBSTR(x, (3 + 2) + 1)</c>.
    /// </summary>
    private SqlExpression Sum(SqlExpression left, SqlExpression right)
        => left is SqlConstantExpression { Value: int first } && right is SqlConstantExpression { Value: int second }
            ? _sqlExpressionFactory.Constant(first + second)
            : _sqlExpressionFactory.Add(left, right);

    private SqlExpression Concat(params SqlExpression[] parts)
    {
        var result = parts[0];

        for (var index = 1; index < parts.Length; index++)
        {
            result = new SqlBinaryExpression(
                ExpressionType.Add,
                result,
                _sqlExpressionFactory.ApplyDefaultTypeMapping(parts[index])!,
                typeof(string),
                result.TypeMapping);
        }

        return result;
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

    private SqlExpression IndexOf(SqlExpression subject, SqlExpression value, SqlExpression? position = null)
        => _sqlExpressionFactory.Function(
            "INSTR",
            position is null ? [subject, value] : [subject, value, position],
            nullable: true,
            argumentsPropagateNullability: position is null ? [true, true] : [true, true, true],
            typeof(int),
            typeMapping: null);

    /// <summary>
    /// A backwards search is the same <c>INSTR</c> given a negative position, which counts from the end
    /// of the string; a search that names a start position begins there and moves towards the front.
    /// The shift to the zero-based index is the one the forward search uses, so "not found" becomes -1
    /// here as well.
    /// </summary>
    private SqlExpression LastIndexOf(SqlExpression subject, SqlExpression value, SqlExpression? startIndex)
        => _sqlExpressionFactory.Subtract(
            IndexOf(subject, value, startIndex is null ? _sqlExpressionFactory.Constant(-1) : Negated(OneBasedStart(startIndex))),
            _sqlExpressionFactory.Constant(1));

    /// <summary>
    /// The sign is folded into a constant so the statement reads <c>INSTR(x, 'a', -3)</c> rather than
    /// <c>INSTR(x, 'a', -(2 + 1))</c>.
    /// </summary>
    private SqlExpression Negated(SqlExpression expression)
        => expression is SqlConstantExpression { Value: int value }
            ? _sqlExpressionFactory.Constant(-value)
            : _sqlExpressionFactory.Negate(expression);

    private static StringComparison? Comparison(IReadOnlyList<SqlExpression> arguments)
        => arguments[^1] is SqlConstantExpression { Value: StringComparison comparison }
            && comparison is StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase
                ? comparison
                : null;

    /// <summary>
    /// An ignore-case comparison upper-cases both sides, which is what a database can express. The
    /// culture-sensitive comparisons are left untranslated by <see cref="Comparison" />.
    /// </summary>
    private SqlExpression Ignoring(SqlExpression expression, StringComparison comparison)
        => comparison == StringComparison.OrdinalIgnoreCase
            ? StringFunction("UPPER", expression, [expression])
            : expression;

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
        // operator precedence inside the LIKE operand. A side that is empty is not concatenated.
        SqlExpression Concat(SqlExpression left, SqlExpression right)
            => _sqlExpressionFactory.Function(
                "CONCAT",
                [left, right],
                nullable: true,
                argumentsPropagateNullability: [true, true],
                typeof(string),
                left.TypeMapping);

        var withPrefix = prefix.Length == 0
            ? argument
            : Concat(_sqlExpressionFactory.Constant(prefix), argument);

        return suffix.Length == 0
            ? withPrefix
            : Concat(withPrefix, _sqlExpressionFactory.Constant(suffix));
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
