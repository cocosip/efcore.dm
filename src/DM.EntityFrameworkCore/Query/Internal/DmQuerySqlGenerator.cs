using Microsoft.EntityFrameworkCore.Query;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Generates SELECT statements.
/// </summary>
/// <remarks>
/// ponytail: the relational base generator is used unchanged, so paging emits the ANSI
/// <c>OFFSET ... FETCH NEXT</c> form. Dameng's supported paging shapes and its function set are
/// settled against a live server in roadmap phase P2; until then no syntax is guessed.
/// </remarks>
internal sealed class DmQuerySqlGenerator : QuerySqlGenerator
{
    public DmQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }
}
