using System.Reflection;
using DM.EntityFrameworkCore.Design.Internal;
using DM.EntityFrameworkCore.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.Tests.Design;

/// <summary>
/// Pins the provider code the tooling writes into a generated context. Scaffolding and the compiled
/// model tool both go through this generator, so a wrong method name here produces code that does not
/// compile against the provider.
/// </summary>
public class DmProviderConfigurationCodeGeneratorTests
{
    [Fact]
    public void DesignTimeServices_RegisterThisProvidersGenerator()
    {
        using var services = ProviderDesignTimeServices.Build();
        var generator = services.GetRequiredService<IProviderConfigurationCodeGenerator>();

        Assert.IsType<DmProviderConfigurationCodeGenerator>(generator);
    }

    [Fact]
    public void GenerateUseProvider_EmitsTheEntryPointWithTheConnectionString()
    {
        using var services = ProviderDesignTimeServices.Build();
        var generator = services.GetRequiredService<IProviderConfigurationCodeGenerator>();

        var fragment = generator.GenerateUseProvider("SERVER=demandb");

        Assert.Equal("UseDm", fragment.Method);
        Assert.Equal(new object?[] { "SERVER=demandb" }, fragment.Arguments);
    }

    [Fact]
    public void GenerateUseProvider_IgnoresAProviderOptionsChain()
    {
        // The provider declares no options, so nothing is chained onto the call.
        using var services = ProviderDesignTimeServices.Build();
        var generator = services.GetRequiredService<IProviderConfigurationCodeGenerator>();

        var fragment = generator.GenerateUseProvider(
            "SERVER=demandb",
            new MethodCallCodeFragment("CommandTimeout", 30));

        Assert.Equal("UseDm", fragment.Method);
        Assert.Null(fragment.ChainedCall);
        Assert.Single(fragment.Arguments);
    }

    [Fact]
    public void GenerateProviderOptions_AndContextOptions_ReturnNothing()
    {
        using var services = ProviderDesignTimeServices.Build();
        var generator = services.GetRequiredService<IProviderConfigurationCodeGenerator>();

        Assert.Null(generator.GenerateProviderOptions());
        Assert.Null(generator.GenerateContextOptions());
    }

    /// <summary>
    /// Guards the emitted name against a rename of the entry point it names.
    /// </summary>
    [Fact]
    public void UseDm_IsAnExtensionMethodOnTheOptionsBuilder()
    {
        var method = typeof(DmDbContextOptionsExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(
                candidate =>
                {
                    if (candidate.Name != "UseDm" || candidate.IsGenericMethodDefinition)
                    {
                        return false;
                    }

                    var parameters = candidate.GetParameters();

                    return parameters.Length >= 2
                        && parameters[0].ParameterType == typeof(DbContextOptionsBuilder)
                        && parameters[1].ParameterType == typeof(string);
                });

        Assert.NotNull(method);
    }

}
