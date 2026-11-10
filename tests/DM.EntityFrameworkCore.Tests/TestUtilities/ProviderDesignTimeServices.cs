using DM.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.Tests.TestUtilities;

/// <summary>
/// Builds the service provider the design time tools build, so a test can ask for the same services
/// <c>dotnet ef</c> resolves.
/// </summary>
internal static class ProviderDesignTimeServices
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkDesignTimeServices();
        new DmDesignTimeServices().ConfigureDesignTimeServices(services);

        return services.BuildServiceProvider();
    }
}
