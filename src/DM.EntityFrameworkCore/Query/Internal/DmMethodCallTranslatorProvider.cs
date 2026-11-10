using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace DM.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Supplies the Dameng method call translators on top of the relational defaults.
/// </summary>
/// <remarks>
/// <c>AddTranslators</c> is called here rather than in an override: since EF Core 10 the
/// relational base class no longer calls it from its constructor.
/// </remarks>
internal sealed class DmMethodCallTranslatorProvider : RelationalMethodCallTranslatorProvider
{
    public DmMethodCallTranslatorProvider(
        RelationalMethodCallTranslatorProviderDependencies dependencies,
        IRelationalTypeMappingSource typeMappingSource)
        : base(dependencies)
        => AddTranslators(
        [
            new DmStringMethodTranslator(dependencies.SqlExpressionFactory),
            new DmMathMethodTranslator(dependencies.SqlExpressionFactory),
            new DmConvertTranslator(dependencies.SqlExpressionFactory),
            new DmObjectToStringTranslator(dependencies.SqlExpressionFactory),
            new DmDateTimeMethodTranslator(dependencies.SqlExpressionFactory, typeMappingSource),
            new DmDateDiffFunctionsTranslator(dependencies.SqlExpressionFactory),
            new DmIsDateFunctionTranslator(dependencies.SqlExpressionFactory),
            new DmTimeOnlyMethodTranslator(dependencies.SqlExpressionFactory, typeMappingSource),
            new DmDateOnlyMethodTranslator(dependencies.SqlExpressionFactory, typeMappingSource)
        ]);
}
