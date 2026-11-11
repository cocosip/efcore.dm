using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// The parts of a function call every translator needs.
/// </summary>
internal static class DmSqlFunctions
{
    /// <summary>
    /// Which arguments can make the function null, for the factory's
    /// <c>argumentsPropagateNullability</c>.
    /// </summary>
    /// <remarks>
    /// A fragment does not propagate: it is a keyword or a literal the dialect wrote (<c>MONTH</c> of an
    /// <c>EXTRACT</c>, <c>'SECOND'</c> of a <c>NUMTODSINTERVAL</c>, the type of a <c>CAST</c>) rather than
    /// a value, so it can never be null. Letting it propagate made the framework rewrite a null-safe
    /// comparison of such a function into a disjunction that includes the keyword, which the server
    /// rejects — <c>MONTH IS NULL</c> answered <c>无效的列名[MONTH]</c>, measured on 8.1.4.80.
    /// </remarks>
    public static bool[] PropagatesNullability(IReadOnlyList<SqlExpression> arguments)
        => arguments.Select(argument => argument is not SqlFragmentExpression).ToArray();
}
