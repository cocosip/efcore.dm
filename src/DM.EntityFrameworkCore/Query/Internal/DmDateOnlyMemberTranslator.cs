using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates the <see cref="DateOnly" /> members to Dameng SQL.
/// </summary>
/// <remarks>
/// <c>DayNumber</c> is the number of days since the first day the type can hold, which is a date
/// difference plus the count of the day it is measured from. The count is taken from 1970-01-01 rather
/// than from 0001-01-01 because the server's calendar skips the ten days of the Gregorian reform: measured
/// on 8.1.4.80, <c>DATEDIFF(DAY, DATE '1582-10-04', DATE '1582-10-15')</c> answered 1, so a count from
/// 0001-01-01 answered 739619 for 2026-01-02 where .NET answers 739617.
/// <para>
/// ponytail: exact for a date from the reform onwards, which is every date a model of a running system
/// holds; a date before 1582-10-15 differs, because the two calendars disagree there and no server
/// function reports the proleptic count. Add one when a model that reaches that far is written.
/// </para>
/// </remarks>
internal sealed class DmDateOnlyMemberTranslator : IMemberTranslator
{
    /// <summary>
    /// The day count of 1970-01-01 in the proleptic calendar .NET uses, which is the anchor of the
    /// difference below.
    /// </summary>
    private const int EpochDayNumber = 719162;

    private static readonly MemberInfo DayNumberMember = typeof(DateOnly).GetRuntimeProperty(
        nameof(DateOnly.DayNumber))!;

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmDateOnlyMemberTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MemberInfo member,
        Type returnType,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (member != DayNumberMember || instance is null)
        {
            return null;
        }

        var difference = _sqlExpressionFactory.Function(
            "DATEDIFF",
            [
                _sqlExpressionFactory.Fragment("DAY"),
                _sqlExpressionFactory.Fragment("DATE '1970-01-01'"),
                instance
            ],
            nullable: true,
            argumentsPropagateNullability: [false, false, true],
            typeof(int),
            typeMapping: null);

        return _sqlExpressionFactory.Add(
            difference,
            _sqlExpressionFactory.Constant(EpochDayNumber));
    }
}
