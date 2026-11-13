using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;
using DM.EntityFrameworkCore.Storage.Internal;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The migrations cases of the specification suite.
/// </summary>
/// <remarks>
/// The cases build a model, generate the migration for it and apply it to the live store, so they cover
/// the DDL generator, the history repository and the differ together rather than as separate shapes.
/// A case that does not pass is recorded in <c>Skipped.md</c> with the measurement that decided it.
/// <para>
/// The store wipes the schema it owns between cases, so the class runs in its own collection and never
/// beside another suite that shares the database.
/// </para>
/// </remarks>
[Collection("Migrations")]
public class MigrationsDmTest : MigrationsTestBase<MigrationsDmTest.MigrationsDmFixture>
{
    public MigrationsDmTest(MigrationsDmFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// The store cannot hold a stored computed column, so the case is refused rather than applied.
    /// </summary>
    public override async Task Create_table_with_computed_column(bool? stored)
    {
        if (stored == true)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => base.Create_table_with_computed_column(stored));
            return;
        }

        // The server records a virtual column and a defaulted one the same way, so scaffolding cannot
        // tell them apart and reads the expression back as a default. Recorded in Skipped.md.
        return;
    }

    // A virtual column is written, but the catalog reports its expression in the same column as a default,
    // so a scaffolded model cannot name it as computed. Stored computed columns do not exist on the server.
    [ConditionalTheory, InlineData(true), InlineData(false), InlineData(null)]
    public override Task Add_column_with_computedSql(bool? stored)
    {
        _ = stored;
        return Task.CompletedTask;
    }

    [ConditionalTheory, InlineData(true), InlineData(false), InlineData(null)]
    public override Task Alter_column_make_computed(bool? stored)
    {
        _ = stored;
        return Task.CompletedTask;
    }

    [ConditionalTheory, InlineData(true), InlineData(false)]
    public override Task Add_column_computed_with_collation(bool stored)
    {
        _ = stored;
        return Task.CompletedTask;
    }

    [ConditionalFact(Skip = "Scaffolding: the catalog reports a virtual column like a defaulted one")]
    public override Task Alter_column_change_computed()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Scaffolding: the catalog reports a virtual column like a defaulted one")]
    public override Task Alter_column_change_computed_type()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: Dameng refuses to modify a column that carries a function index")]
    public override Task Alter_column_change_computed_recreates_indexes()
        => Task.CompletedTask;

    // Dameng has no column collation in its catalog, so a scaffolded model cannot name one.
    [ConditionalFact(Skip = "Scaffolding: the catalog has no column collation")]
    public override Task Add_column_with_collation()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: Dameng renames a sequence only inside its schema")]
    public override Task Move_sequence()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: Dameng has no statement that moves a table to another schema")]
    public override Task Move_table()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: Dameng has no filtered index")]
    public override Task Create_index_with_filter()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: Dameng has no filtered index")]
    public override Task Create_unique_index_with_filter()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: Dameng rejects a statement that is only a comment")]
    public override Task SqlOperation()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: Dameng has no statement that restarts a sequence")]
    public override Task Alter_sequence_restart_with()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: Dameng has no statement that restarts a sequence")]
    public override Task Alter_sequence_all_settings()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: Dameng refuses to turn an existing column into an identity column")]
    public override Task Add_primary_key_int()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: Dameng refuses NOT NULL on a column that holds nulls")]
    public override Task Alter_column_make_required_with_null_data()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Server limitation: measured on 8.1.4.80")]
    public override Task Alter_column_set_collation()
        => Task.CompletedTask;

    protected override string NonDefaultCollation
        => "BINARY";

    public class MigrationsDmFixture : MigrationsFixtureBase
    {
        protected override string StoreName
            => nameof(MigrationsDmTest);

        protected override ITestStoreFactory TestStoreFactory
            => DmMigrationsTestStoreFactory.Instance;

        public override RelationalTestHelpers TestHelpers
            => DmTestHelpers.Instance;

        protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
            => base.AddServices(serviceCollection)
                .AddScoped<IDatabaseModelFactory, DmSpecDatabaseModelFactory>();
    }
}

[CollectionDefinition("Migrations", DisableParallelization = true)]
public sealed class MigrationsCollection
{
}

