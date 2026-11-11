using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

/// <summary>
/// The fixture every specification test class derives from: it supplies the store and the options, and
/// the base class supplies the model, the seeding and the test methods.
/// </summary>
public abstract class DmFixture<TContext> : SharedStoreFixtureBase<TContext>
    where TContext : DbContext
{
    protected override ITestStoreFactory TestStoreFactory => DmTestStoreFactory.Instance;
}
