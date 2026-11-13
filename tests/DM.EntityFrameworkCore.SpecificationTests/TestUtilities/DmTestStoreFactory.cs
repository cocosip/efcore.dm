using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

public sealed class DmTestStoreFactory : RelationalTestStoreFactory
{
    public static DmTestStoreFactory Instance { get; } = new(primitiveCollections: false);

    public static DmTestStoreFactory PrimitiveCollectionsInstance { get; } = new(primitiveCollections: true);

    private readonly bool _primitiveCollections;

    private DmTestStoreFactory(bool primitiveCollections)
    {
        _primitiveCollections = primitiveCollections;
    }

    public override TestStore Create(string storeName)
        => _primitiveCollections
            ? DmTestStore.CreateForPrimitiveCollections(storeName)
            : DmTestStore.Create(storeName);

    public override TestStore GetOrCreate(string storeName)
        => _primitiveCollections
            ? DmTestStore.GetOrCreateForPrimitiveCollections(storeName)
            : DmTestStore.GetOrCreate(storeName);

    public override IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        => serviceCollection.AddEntityFrameworkDm();
}
