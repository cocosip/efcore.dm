using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;

namespace DM.EntityFrameworkCore.Design.Internal;

/// <summary>
/// Emits the provider call that generated code uses, which is what makes scaffolding and the
/// compiled model tool produce a context that compiles against this provider.
/// </summary>
/// <remarks>
/// The provider has no options beyond the connection, so both option methods return
/// <see langword="null" /> rather than an empty fragment: the code generator then emits no call at
/// all instead of a call this provider would reject.
/// </remarks>
internal sealed class DmProviderConfigurationCodeGenerator : ProviderCodeGenerator
{
    /// <summary>
    /// The name of the entry point, which is the extension method this provider declares.
    /// </summary>
    private const string UseDmMethod = "UseDm";

    public DmProviderConfigurationCodeGenerator(ProviderCodeGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }

    public override MethodCallCodeFragment GenerateUseProvider(string connectionString)
        => new(UseDmMethod, connectionString);

    /// <remarks>
    /// The provider has no options to chain: <see cref="GenerateProviderOptions" /> returns
    /// <see langword="null" />, so a chain passed in here did not come from this provider.
    /// </remarks>
    public override MethodCallCodeFragment GenerateUseProvider(
        string connectionString,
        MethodCallCodeFragment? providerOptions)
        => GenerateUseProvider(connectionString);

    public override MethodCallCodeFragment? GenerateProviderOptions() => null;

    public override MethodCallCodeFragment? GenerateContextOptions() => null;
}
