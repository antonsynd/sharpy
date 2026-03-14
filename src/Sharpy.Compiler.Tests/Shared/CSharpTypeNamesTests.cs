using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Shared;

public class CSharpTypeNamesTests
{
    [Theory]
    [InlineData("list", "Sharpy.List")]
    [InlineData("dict", "Sharpy.Dict")]
    [InlineData("set", "Sharpy.Set")]
    public void FromSharpyName_MapsCollectionTypes(string sharpyName, string expected)
    {
        Assert.Equal(expected, CSharpTypeNames.FromSharpyName(sharpyName));
    }

    [Theory]
    [InlineData("int")]
    [InlineData("str")]
    [InlineData("bool")]
    [InlineData("float")]
    [InlineData("unknown")]
    [InlineData("")]
    public void FromSharpyName_ReturnsNull_ForNonCollectionTypes(string sharpyName)
    {
        Assert.Null(CSharpTypeNames.FromSharpyName(sharpyName));
    }

    [Fact]
    public void FromSharpyName_IsCaseSensitive()
    {
        Assert.Null(CSharpTypeNames.FromSharpyName("List"));
        Assert.Null(CSharpTypeNames.FromSharpyName("Dict"));
        Assert.Null(CSharpTypeNames.FromSharpyName("Set"));
    }

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        Assert.Equal("Sharpy.List", CSharpTypeNames.SharpyList);
        Assert.Equal("Sharpy.Dict", CSharpTypeNames.SharpyDict);
        Assert.Equal("Sharpy.Set", CSharpTypeNames.SharpySet);
        Assert.Equal("Sharpy.Optional", CSharpTypeNames.SharpyOptional);
        Assert.Equal("Sharpy.Result", CSharpTypeNames.SharpyResult);
        Assert.Equal("IEnumerable", CSharpTypeNames.IEnumerable);
        Assert.Equal("IAsyncEnumerable", CSharpTypeNames.IAsyncEnumerable);
    }
}
