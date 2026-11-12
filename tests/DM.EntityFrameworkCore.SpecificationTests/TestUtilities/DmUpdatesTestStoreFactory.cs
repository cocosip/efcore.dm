using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

/// <summary>
/// The store factory of the updates suite: its store owns <see cref="DmTestStore.UpdatesSchema" />,
/// because the model creates a view of its own and no other class may remove it mid-run.
/// </summary>
public sealed class DmUpdatesTestStoreFactory : RelationalTestStoreFactory
{
    public static DmUpdatesTestStoreFactory Instance { get; } = new();

    private DmUpdatesTestStoreFactory()
    {
    }

    public override TestStore Create(string storeName) => DmTestStore.CreateForUpdates(storeName);

    public override TestStore GetOrCreate(string storeName) => DmTestStore.CreateForUpdates(storeName);

    public override IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        => serviceCollection.AddEntityFrameworkDm();
}
