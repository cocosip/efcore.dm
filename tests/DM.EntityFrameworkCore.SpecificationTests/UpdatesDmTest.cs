using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.TestModels.UpdatesModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Update;
using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The save-side <c>Updates</c> cases of the specification suite.
/// </summary>
/// <remarks>
/// The base model, the seed data and the expected results come from the shipped package; what this
/// project supplies is the store and the provider-specific model. A case that does not pass is
/// recorded in <c>Skipped.md</c> with the measurement that decided it.
/// </remarks>
public class UpdatesDmTest : UpdatesRelationalTestBase<UpdatesDmTest.UpdatesDmFixture>
{
    private const string LongName =
        "LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkingCorrectly";

    private const string LongDetailsName = LongName + "Details";

    private const string LongPropertyName =
        "ExtraPropertyWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkingCorrectly";

    private const string CollidingPropertyName = LongPropertyName + "WhenTruncatedNamesCollide";

    public UpdatesDmTest(UpdatesDmFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// Dameng limits an identifier to 128 characters. The provider declares that limit through the
    /// relational max identifier length convention, so the framework truncates a generated name and
    /// appends a uniquifier. This case pins the result, including two properties whose truncated names
    /// would otherwise collide.
    /// </summary>
    public override void Identifiers_are_generated_correctly()
    {
        using var context = Fixture.CreateContext();

        var entityType = context.Model.FindEntityType(
            typeof(LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkingCorrectly))!;
        var detailsType = context.Model.FindEntityType(
            typeof(LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkingCorrectlyDetails))!;

        Assert.Equal(LongName[..127] + "~", entityType.GetTableName());
        Assert.Equal("PK_" + LongName[..124] + "~", entityType.GetKeys().Single().GetName());
        Assert.Equal("FK_" + LongName[..124] + "~", entityType.GetForeignKeys().Single().GetConstraintName());
        Assert.Equal("IX_" + LongName[..124] + "~", entityType.GetIndexes().Single().GetDatabaseName());

        Assert.Equal(LongDetailsName[..126] + "~1", detailsType.GetTableName());
        Assert.Equal("PK_LoginDetails", detailsType.GetKeys().Single().GetName());
        Assert.Equal("IX_" + LongDetailsName[..123] + "~1", detailsType.GetIndexes().Single().GetDatabaseName());

        var table = StoreObjectIdentifier.Table(detailsType.GetTableName()!);
        Assert.Equal(LongPropertyName[..127] + "~", detailsType.FindProperty(LongPropertyName)!.GetColumnName(table));
        Assert.Equal(CollidingPropertyName[..126] + "~1", detailsType.FindProperty(CollidingPropertyName)!.GetColumnName(table));
    }

    /// <summary>
    /// Dameng has no filtered index: measured on 8.1.4.80, <c>CREATE INDEX ... WHERE</c> is rejected with
    /// <c>语法分析出错</c>. The case swaps two values under a filtered unique index, so it cannot run here.
    /// </summary>
    public override Task Swap_filtered_unique_index_values()
        => Task.CompletedTask;

    public class UpdatesDmFixture : UpdatesRelationalFixture
    {
        protected override ITestStoreFactory TestStoreFactory
            => DmUpdatesTestStoreFactory.Instance;

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            // Product and ProductWithBytes are siblings under an abstract base, and the shared foreign key
            // case needs the base mapped so both are rows of one table. The SQL Server fixture maps it for
            // the same reason; without it the dependent's foreign key resolves to a table the inserted row
            // is not in.
            modelBuilder.Entity<ProductBase>();
        }
    }
}
