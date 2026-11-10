using Microsoft.EntityFrameworkCore.Query;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Translates method calls to SQL. String, date and time, and math translators are added in
/// roadmap phase P2; until then the relational defaults apply.
/// </summary>
internal sealed class DmMethodCallTranslatorProvider : RelationalMethodCallTranslatorProvider
{
    public DmMethodCallTranslatorProvider(RelationalMethodCallTranslatorProviderDependencies dependencies)
        : base(dependencies)
    {
    }
}
