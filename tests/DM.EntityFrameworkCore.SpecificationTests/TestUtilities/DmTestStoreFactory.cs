using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

public sealed class DmTestStoreFactory : RelationalTestStoreFactory
{
    public static DmTestStoreFactory Instance { get; } = new();

    private DmTestStoreFactory()
    {
    }

    public override TestStore Create(string storeName) => DmTestStore.Create(storeName);

    public override TestStore GetOrCreate(string storeName) => DmTestStore.GetOrCreate(storeName);

    public override IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        => serviceCollection.AddEntityFrameworkDm();
}
