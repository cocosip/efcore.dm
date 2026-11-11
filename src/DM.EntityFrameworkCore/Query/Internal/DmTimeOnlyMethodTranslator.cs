using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates the <see cref="TimeOnly" /> conversions and additions to Dameng SQL: a time of day is a
/// cast, one built from an elapsed time is that interval added to the epoch and cast, and an addition is
/// <c>TIMESTAMPADD</c> of the same number of seconds, cast back to a time of day.
/// </summary>
/// <remarks>
/// The addition is expressed in seconds rather than in the unit the method names, because a fractional
/// argument is real: <c>AddHours(1.5)</c> adds ninety minutes, and <c>TIMESTAMPADD</c> with an hour count
/// of 1.5 answered a whole hour on 8.1.4.80. A fraction of a second is lost, because the same measurement
/// truncated a second count of 1.5 to one second.
/// </remarks>
internal sealed class DmTimeOnlyMethodTranslator : IMethodCallTranslator
{
    private static readonly MethodInfo FromDateTimeMethod = typeof(TimeOnly).GetRuntimeMethod(
        nameof(TimeOnly.FromDateTime), [typeof(DateTime)])!;

    private static readonly MethodInfo FromTimeSpanMethod = typeof(TimeOnly).GetRuntimeMethod(
        nameof(TimeOnly.FromTimeSpan), [typeof(TimeSpan)])!;

    private static readonly MethodInfo AddMethod = typeof(TimeOnly).GetRuntimeMethod(
        nameof(TimeOnly.Add), [typeof(TimeSpan)])!;


    private static readonly MethodInfo AddHoursMethod = typeof(TimeOnly).GetRuntimeMethod(
        nameof(TimeOnly.AddHours), [typeof(double)])!;

    private static readonly MethodInfo AddMinutesMethod = typeof(TimeOnly).GetRuntimeMethod(
        nameof(TimeOnly.AddMinutes), [typeof(double)])!;

    private const string TimeStampAddFunction = "TIMESTAMPADD";

    private const string SecondUnit = "SQL_TSI_SECOND";

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

        if (instance is null)
        {
            return null;
        }

        if (method == AddHoursMethod)
        {
            return AddSeconds(instance, arguments[0], 3600.0);
        }

        if (method == AddMinutesMethod)
        {
            return AddSeconds(instance, arguments[0], 60.0);
        }


        return method == AddMethod
            ? AddSeconds(instance, DmIntervals.Seconds(_sqlExpressionFactory, arguments[0]))
            : null;
    }

    private SqlExpression AddSeconds(SqlExpression instance, SqlExpression count, double factor)
        => AddSeconds(
            instance,
            _sqlExpressionFactory.Multiply(
                _sqlExpressionFactory.ApplyDefaultTypeMapping(count)!,
                _sqlExpressionFactory.Constant(factor)));

    private SqlExpression AddSeconds(SqlExpression instance, SqlExpression seconds)
        => _sqlExpressionFactory.Convert(
            _sqlExpressionFactory.Function(
                TimeStampAddFunction,
                [_sqlExpressionFactory.Fragment(SecondUnit), seconds, instance],
                nullable: true,
                argumentsPropagateNullability: [false, true, true],
                typeof(DateTime),
                typeMapping: null),
            typeof(TimeOnly),
            _timeMapping);
}
