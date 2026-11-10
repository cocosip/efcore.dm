using Microsoft.EntityFrameworkCore.Query;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Supplies the Dameng member translators on top of the relational defaults.
/// </summary>
/// <remarks>
/// <c>AddTranslators</c> is called here rather than in an override: since EF Core 10 the
/// relational base class no longer calls it from its constructor.
/// </remarks>
internal sealed class DmMemberTranslatorProvider : RelationalMemberTranslatorProvider
{
    public DmMemberTranslatorProvider(RelationalMemberTranslatorProviderDependencies dependencies)
        : base(dependencies)
        => AddTranslators(
        [
            new DmStringMemberTranslator(dependencies.SqlExpressionFactory),
            new DmDateTimeMemberTranslator(dependencies.SqlExpressionFactory)
        ]);
}
