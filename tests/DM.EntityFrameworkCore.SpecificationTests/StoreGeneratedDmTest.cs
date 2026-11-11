using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The store-generated value cases of the specification suite.
/// </summary>
/// <remarks>
/// The model is configured the way Dameng can answer it: an integer key the store generates as an
/// identity column, a column default the store fills when the model does not, and the converted key
/// types the base model declares. The cases that do not pass are recorded in <c>Skipped.md</c>.
/// </remarks>
public class StoreGeneratedDmTest : StoreGeneratedTestBase<StoreGeneratedDmTest.StoreGeneratedDmFixture>
{
    public StoreGeneratedDmTest(StoreGeneratedDmFixture fixture)
        : base(fixture)
    {
    }

    // The suite begins the transaction a case runs in and hands it to the context, which is the shape
    // the provider supports for an externally owned transaction.
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseTransaction(transaction.GetDbTransaction());

    public class StoreGeneratedDmFixture : StoreGeneratedFixtureBase
    {
        protected override string StoreName
            => "StoreGeneratedTest";

        protected override ITestStoreFactory TestStoreFactory
            => DmTestStoreFactory.Instance;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => builder
                .EnableSensitiveDataLogging()
                .ConfigureWarnings(
                    b => b.Default(WarningBehavior.Throw)
                        .Ignore(CoreEventId.SensitiveDataLoggingEnabledWarning)
                        .Ignore(RelationalEventId.BoolWithDefaultWarning)
                        // The suite loads three collections in one query, and the warning is guidance about
                        // splitting rather than a defect of this provider.
                        .Ignore(RelationalEventId.MultipleCollectionIncludeWarning));

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            // The identity column is what a Dameng integer key becomes by convention; naming it keeps the
            // cases that ask for an explicit value honest about the column they write.
            modelBuilder.Entity<Gumball>(b =>
            {
                b.Property(e => e.Id).UseIdentityColumn();
                b.Property(e => e.Identity).HasDefaultValue("Banana Joe");
                b.Property(e => e.IdentityReadOnlyBeforeSave).HasDefaultValue("Doughnut Sheriff");
                b.Property(e => e.IdentityReadOnlyAfterSave).HasDefaultValue("Anton");
                b.Property(e => e.AlwaysIdentity).HasDefaultValue("Banana Joe");
                b.Property(e => e.AlwaysIdentityReadOnlyBeforeSave).HasDefaultValue("Doughnut Sheriff");
                b.Property(e => e.AlwaysIdentityReadOnlyAfterSave).HasDefaultValue("Anton");
                b.Property(e => e.Computed).HasDefaultValue("Alan");
                b.Property(e => e.ComputedReadOnlyBeforeSave).HasDefaultValue("Carmen");
                b.Property(e => e.ComputedReadOnlyAfterSave).HasDefaultValue("Tina Rex");
                b.Property(e => e.AlwaysComputed).HasDefaultValue("Alan");
                b.Property(e => e.AlwaysComputedReadOnlyBeforeSave).HasDefaultValue("Carmen");
                b.Property(e => e.AlwaysComputedReadOnlyAfterSave).HasDefaultValue("Tina Rex");
            });

            modelBuilder.Entity<Anais>(b =>
            {
                b.Property(e => e.OnAdd).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddUseBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddIgnoreBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddThrowBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddUseBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddIgnoreBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddThrowBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddUseBeforeThrowAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddIgnoreBeforeThrowAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddThrowBeforeThrowAfter).HasDefaultValue("Rabbit");

                b.Property(e => e.OnAddOrUpdate).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateUseBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateIgnoreBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateThrowBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateUseBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateIgnoreBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateThrowBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateUseBeforeThrowAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateIgnoreBeforeThrowAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateThrowBeforeThrowAfter).HasDefaultValue("Rabbit");

                b.Property(e => e.OnUpdate).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateUseBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateIgnoreBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateThrowBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateUseBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateIgnoreBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateThrowBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateUseBeforeThrowAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateIgnoreBeforeThrowAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateThrowBeforeThrowAfter).HasDefaultValue("Rabbit");
            });

            // A default value is what the store fills here rather than a computed column: this table's only
            // other columns generate, so the insert names no column at all and the server expands it to every
            // column. Measured on 8.1.4.80, that expansion includes a virtual column and answers
            // `不允许对虚拟列执行INSERT操作`, whether written as `DEFAULT VALUES` or with the column named.
            // A default keeps the case about the field a generated value lands in, and the provider's
            // computed-column support is covered by its own tests.
            modelBuilder.Entity<WithBackingFields>(b =>
            {
                b.Property(e => e.NullableAsNonNullable).HasDefaultValue(1);
                b.Property(e => e.NonNullableAsNullable).HasDefaultValue(1);
            });

            modelBuilder.Entity<WithNoBackingFields>(b =>
            {
                b.Property(e => e.TrueDefault).HasDefaultValue(true);
                b.Property(e => e.NonZeroDefault).HasDefaultValue(-1);
                b.Property(e => e.FalseDefault).HasDefaultValue(false);
                b.Property(e => e.ZeroDefault).HasDefaultValue(0);
            });

            modelBuilder.Entity<WithNullableBackingFields>(b =>
            {
                b.Property(e => e.NullableBackedBoolTrueDefault).HasDefaultValue(true);
                b.Property(e => e.NullableBackedIntNonZeroDefault).HasDefaultValue(-1);
                b.Property(e => e.NullableBackedBoolFalseDefault).HasDefaultValue(false);
                b.Property(e => e.NullableBackedIntZeroDefault).HasDefaultValue(0);
            });

            modelBuilder.Entity<WithObjectBackingFields>(b =>
            {
                b.Property(e => e.NullableBackedBoolTrueDefault).HasDefaultValue(true);
                b.Property(e => e.NullableBackedIntNonZeroDefault).HasDefaultValue(-1);
                b.Property(e => e.NullableBackedBoolFalseDefault).HasDefaultValue(false);
                b.Property(e => e.NullableBackedIntZeroDefault).HasDefaultValue(0);
            });

            modelBuilder.Entity<NonStoreGenDependent>().Property(e => e.HasTemp).HasDefaultValue(777);

            modelBuilder.Entity<CompositePrincipal>().Property(e => e.Id).UseIdentityColumn();

            base.OnModelCreating(modelBuilder, context);
        }
    }
}
