using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

/// <summary>
/// The service and options helpers the migrations suite asks the fixture for.
/// </summary>
/// <remarks>
/// The models the migrations cases build are compiled in memory and applied through the provider's own
/// services, so this helper is what puts the provider in reach of the base class rather than the store.
/// </remarks>
public sealed class DmTestHelpers : RelationalTestHelpers
{
    public static DmTestHelpers Instance { get; } = new();

    private DmTestHelpers()
    {
    }

    public override DbContextOptionsBuilder UseProviderOptions(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDm(DmTestConnection.ConnectionString);

    public override IServiceCollection AddProviderServices(IServiceCollection services)
        => services.AddEntityFrameworkDm();
}
