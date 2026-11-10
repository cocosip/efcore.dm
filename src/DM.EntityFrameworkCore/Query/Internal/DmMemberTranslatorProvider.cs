using Microsoft.EntityFrameworkCore.Query;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates member access to SQL. Dameng specific members are added in roadmap phase P2;
/// until then the relational defaults apply.
/// </summary>
internal sealed class DmMemberTranslatorProvider : RelationalMemberTranslatorProvider
{
    public DmMemberTranslatorProvider(RelationalMemberTranslatorProviderDependencies dependencies)
        : base(dependencies)
    {
    }
}
