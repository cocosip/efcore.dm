using System.Text;
using DM.EntityFrameworkCore.Dialect;

namespace DM.EntityFrameworkCore.Tests.Dialect;

public class DmParametersTests
{
    [Theory]
    [InlineData("p0", ":p0")]
    [InlineData(":p0", ":p0")]
    public void ToName_AddsPrefixOnce(string input, string expected)
    {
        Assert.Equal(expected, DmParameters.ToName(input));
    }

    [Theory]
    [InlineData(":p0", "p0")]
    [InlineData("p0", "p0")]
    public void ToBareName_RemovesPrefixOnce(string input, string expected)
    {
        Assert.Equal(expected, DmParameters.ToBareName(input));
    }

    [Fact]
    public void Append_WritesPrefixAndName()
    {
        var builder = new StringBuilder("WHERE x = ");

        DmParameters.Append(builder, "p0");

        Assert.Equal("WHERE x = :p0", builder.ToString());
    }

    [Fact]
    public void ToName_IsIdempotent()
    {
        Assert.Equal(DmParameters.ToName("p1"), DmParameters.ToName(DmParameters.ToName("p1")));
    }
}
