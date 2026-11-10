using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates <see cref="string" /> members to Dameng SQL.
/// </summary>
internal sealed class DmStringMemberTranslator : IMemberTranslator
{
    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public DmStringMemberTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public SqlExpression? Translate(
        SqlExpression? instance,
        MemberInfo member,
        Type returnType,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is null || member.Name != nameof(string.Length) || instance.Type != typeof(string))
        {
            return null;
        }

        return _sqlExpressionFactory.Function(
            "LENGTH",
            [instance],
            nullable: true,
            argumentsPropagateNullability: [true],
            returnType,
            typeMapping: null);
    }
}
