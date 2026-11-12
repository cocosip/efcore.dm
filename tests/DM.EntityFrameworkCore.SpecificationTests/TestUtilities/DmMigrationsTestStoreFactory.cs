using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

/// <summary>
/// The store factory of the migrations suite: its store owns the schema <see cref="DmTestStore.MigrationsSchema" />.
/// </summary>
public sealed class DmMigrationsTestStoreFactory : RelationalTestStoreFactory
{
    public static DmMigrationsTestStoreFactory Instance { get; } = new();

    private DmMigrationsTestStoreFactory()
    {
    }

    public override TestStore Create(string storeName) => DmTestStore.CreateForMigrations(storeName);

    public override TestStore GetOrCreate(string storeName) => DmTestStore.CreateForMigrations(storeName);

    public override IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        => serviceCollection.AddEntityFrameworkDm();
}
