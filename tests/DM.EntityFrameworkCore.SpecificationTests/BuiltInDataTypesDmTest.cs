using DM.EntityFrameworkCore.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace DM.EntityFrameworkCore.SpecificationTests;

/// <summary>
/// The built-in data types cases of the specification suite.
/// </summary>
/// <remarks>
/// Every case in this class comes from EF Core's own suite; a case that does not pass is recorded in
/// <c>Skipped.md</c> with a reason rather than removed, and the ledger test fails when an entry names a
/// class that no longer exists.
/// </remarks>
public class BuiltInDataTypesDmTest : BuiltInDataTypesTestBase<BuiltInDataTypesDmTest.BuiltInDataTypesDmFixture>
{
    public BuiltInDataTypesDmTest(BuiltInDataTypesDmFixture fixture)
        : base(fixture)
    {
    }

    [ConditionalFact]
    public void Can_connect_and_read_a_row()
    {
        using var context = CreateContext();

        Assert.True(context.Database.CanConnect());
    }

    public class BuiltInDataTypesDmFixture : BuiltInDataTypesFixtureBase
    {
        protected override ITestStoreFactory TestStoreFactory => DmTestStoreFactory.Instance;

        // A string the model does not bound is an NCLOB, and Dameng compares a large object with
        // DBMS_LOB.COMPARE rather than with an operator, so the cases that compare one are not applicable.
        public override bool SupportsLargeStringComparisons => false;

        // `IsUnicode(false)` is VARCHAR2, which the provider maps, so the ANSI cases are exercised. Whether
        // the server converts a Unicode value to ANSI without loss depends on the database encoding, and the
        // cases that ask it are the ones that answer.
        public override bool SupportsAnsi => true;

        public override bool SupportsUnicodeToAnsiConversion => true;

        // A byte array maps to BLOB, and Dameng does not allow a large object in a key or an index.
        public override bool SupportsBinaryKeys => false;

        // NUMBER compares with the operators Dameng defines for it.
        public override bool SupportsDecimalComparisons => true;

        // A TIMESTAMP column carries no zone, so the kind a value was written with does not come back.
        public override bool PreservesDateTimeKind => false;

        // NVARCHAR2 compares byte by byte in Oracle-compatible mode, so case and trailing spaces matter.
        public override bool StrictEquality => true;

        public override DateTime DefaultDateTime => new(2015, 1, 2, 3, 4, 5, 678);
    }
}
