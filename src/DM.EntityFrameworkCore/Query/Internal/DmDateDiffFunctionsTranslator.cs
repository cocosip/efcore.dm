using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Dm.Extensions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates the date difference methods of <see cref="DmDbFunctionsExtensions" /> to Dameng's
/// <c>DATEDIFF(datepart, date1, date2)</c>, which returns the boundaries of the named part crossed
/// between its two arguments.
/// </summary>
internal sealed class DmDateDiffFunctionsTranslator : IMethodCallTranslator
{
    private static readonly Dictionary<MethodInfo, string> DateParts = BuildDateParts();

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmDateDiffFunctionsTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (!DateParts.TryGetValue(method, out var datePart))
        {
            return null;
        }

        // The two dates are given a type mapping before the call is built: one of them can be a
        // parameter, and the server needs the type of both sides of the difference.
        return _sqlExpressionFactory.Function(
            "DATEDIFF",
            [
                _sqlExpressionFactory.Fragment(datePart),
                _sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[1])!,
                _sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[2])!
            ],
            nullable: true,
            argumentsPropagateNullability: [false, true, true],
            typeof(int),
            typeMapping: null);
    }

    private static Dictionary<MethodInfo, string> BuildDateParts()
    {
        (string Method, string DatePart)[] mapping =
        [
            (nameof(DmDbFunctionsExtensions.DateDiffYear), "YEAR"),
            (nameof(DmDbFunctionsExtensions.DateDiffMonth), "MONTH"),
            (nameof(DmDbFunctionsExtensions.DateDiffDay), "DAY"),
            (nameof(DmDbFunctionsExtensions.DateDiffHour), "HOUR"),
            (nameof(DmDbFunctionsExtensions.DateDiffMinute), "MINUTE"),
            (nameof(DmDbFunctionsExtensions.DateDiffSecond), "SECOND"),
            (nameof(DmDbFunctionsExtensions.DateDiffMillisecond), "MILLISECOND")
        ];

        var dateParts = new Dictionary<MethodInfo, string>();

        // Every overload of a unit differs only in the CLR type of its arguments, so the mapping is
        // resolved by name and covers all four at once.
        foreach (var (method, datePart) in mapping)
        {
            foreach (var overload in typeof(DmDbFunctionsExtensions).GetMethods().Where(m => m.Name == method))
            {
                dateParts[overload] = datePart;
            }
        }

        return dateParts;
    }
}
