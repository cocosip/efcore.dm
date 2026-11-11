using System.Reflection;
using DM.EntityFrameworkCore.Dialect;

namespace DM.EntityFrameworkCore.Tests.Dialect;

public class DmDialectConstantsTests
{
    [Fact]
    public void BatchTerminator_IsSlashOnItsOwnLine()
    {
        // The line feed after the slash is part of the terminator, because the script writer appends
        // the next statement directly after it.
        Assert.Equal("/\n", DmBatch.Terminator);
    }

    [Fact]
    public void StoreTypes_AreUpperInvariantAndUnique()
    {
        var values = typeof(DmStoreTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        Assert.NotEmpty(values);
        Assert.All(values, v => Assert.Equal(v.ToUpperInvariant(), v));
        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TypeSizes_ProduceParenthesisedSuffixes()
    {
        Assert.Equal("(29,4)", DmTypeSizes.FormatDecimal(29, 4));
        Assert.Equal("(255)", DmTypeSizes.FormatLength(255));
        Assert.Equal("(6)", DmTypeSizes.FormatPrecision(6));
    }

    [Fact]
    public void TypeSizes_AreConsistentWithDefaults()
    {
        Assert.InRange(DmTypeSizes.DefaultDecimalScale, 0, DmTypeSizes.DefaultDecimalPrecision);
        Assert.True(DmTypeSizes.MaxCharacterLength > 0);
    }
}
