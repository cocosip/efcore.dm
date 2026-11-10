using System.Text;
using DM.EntityFrameworkCore.Dialect;

namespace DM.EntityFrameworkCore.Tests.Dialect;

public class DmIdentifierTests
{
    [Theory]
    [InlineData("Users", "\"Users\"")]
    [InlineData("we\"ird", "\"we\"\"ird\"")]
    public void Delimit_WrapsAndEscapesDelimiters(string input, string expected)
    {
        Assert.Equal(expected, DmIdentifier.Delimit(input));
    }

    [Fact]
    public void Delimit_AppendsWithoutAllocatingIntermediateString()
    {
        var builder = new StringBuilder("ORDER BY ");

        DmIdentifier.Delimit(builder, "a\"b");

        Assert.Equal("ORDER BY \"a\"\"b\"", builder.ToString());
    }

    [Fact]
    public void AppendEscaped_LeavesAlreadySafeIdentifierUnchanged()
    {
        var builder = new StringBuilder();

        DmIdentifier.AppendEscaped(builder, "Column1");

        Assert.Equal("Column1", builder.ToString());
    }

    [Theory]
    [InlineData("SCHEMA.TABLE", new[] { "SCHEMA", "TABLE" })]
    [InlineData("\"My.Schema\".\"My.Table\"", new[] { "My.Schema", "My.Table" })]
    [InlineData("TABLE", new[] { "TABLE" })]
    [InlineData("\"a\"\"b\".c", new[] { "a\"b", "c" })]
    public void Split_RespectsDelimiters(string input, string[] expected)
    {
        Assert.Equal(expected, DmIdentifier.Split(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Delimit_RejectsMissingIdentifier(string? input)
    {
        Assert.ThrowsAny<ArgumentException>(() => DmIdentifier.Delimit(input!));
    }
}
