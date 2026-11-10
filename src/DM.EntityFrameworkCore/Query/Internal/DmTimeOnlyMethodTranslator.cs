using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates the <see cref="TimeOnly" /> conversions to Dameng SQL: a time of day is a cast, and one
/// built from an elapsed time is that interval added to the epoch and cast.
/// </summary>
internal sealed class DmTimeOnlyMethodTranslator : IMethodCallTranslator
{
    private static readonly MethodInfo FromDateTimeMethod = typeof(TimeOnly).GetRuntimeMethod(
        nameof(TimeOnly.FromDateTime), [typeof(DateTime)])!;

    private static readonly MethodInfo FromTimeSpanMethod = typeof(TimeOnly).GetRuntimeMethod(
        nameof(TimeOnly.FromTimeSpan), [typeof(TimeSpan)])!;

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    private readonly RelationalTypeMapping _timeMapping;

    public DmTimeOnlyMethodTranslator(
        ISqlExpressionFactory sqlExpressionFactory,
        IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _timeMapping = typeMappingSource.FindMapping(typeof(TimeOnly))!;
    }

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method == FromDateTimeMethod)
        {
            return _sqlExpressionFactory.Convert(arguments[0], typeof(TimeOnly), _timeMapping);
        }

        if (method == FromTimeSpanMethod)
        {
            // A TimeSpan is stored as INTERVAL DAY TO SECOND, so the time of day it describes is that
            // interval added to midnight.
            var midnight = _sqlExpressionFactory.Fragment("TIMESTAMP '1970-01-01 00:00:00'");

            return _sqlExpressionFactory.Convert(
                _sqlExpressionFactory.Add(midnight, arguments[0]),
                typeof(TimeOnly),
                _timeMapping);
        }

        return null;
    }
}
