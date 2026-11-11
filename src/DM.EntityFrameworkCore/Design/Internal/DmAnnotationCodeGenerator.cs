using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using DM.EntityFrameworkCore.Metadata.Conventions;

namespace DM.EntityFrameworkCore.Design.Internal;

/// <summary>
/// Writes the identity call into scaffolded code.
/// </summary>
/// <remarks>
/// A column the catalog reports as store generated is recorded as <c>ValueGeneratedOnAdd</c>, which is
/// enough for a key, because a key is an identity column by convention. A column that is not a key would
/// be written as a plain column and the model would expect a value the store is supposed to produce, so
/// the call that names the strategy is emitted for it. The scaffolding factory cannot do this: it lives
/// in the design assembly, which the package does not depend on, while the annotation code generator is
/// part of the relational package and runs where the property is already known.
/// </remarks>
internal sealed class DmAnnotationCodeGenerator : AnnotationCodeGenerator
{
    public DmAnnotationCodeGenerator(AnnotationCodeGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }

    public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
        IProperty property,
        IDictionary<string, IAnnotation> annotations)
    {
        var calls = base.GenerateFluentApiCalls(property, annotations).ToList();

        if (property.ValueGenerated == ValueGenerated.OnAdd
            && !property.IsPrimaryKey()
            && DmValueGenerationStrategyConvention.IsIdentityType(
                property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType)
            && !calls.Any(call => call.Method == "UseIdentityColumn"))
        {
            calls.Add(new MethodCallCodeFragment("UseIdentityColumn"));
        }

        return calls;
    }
}
