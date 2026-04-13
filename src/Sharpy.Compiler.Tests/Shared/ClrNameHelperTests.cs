using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Shared;

public class ClrNameHelperTests
{
    [Theory]
    [InlineData("List", "List")]
    [InlineData("Dictionary", "Dictionary")]
    [InlineData("Int32", "Int32")]
    public void StripArity_ReturnsUnchanged_WhenNoBacktick(string input, string expected)
    {
        Assert.Equal(expected, ClrNameHelper.StripArity(input));
    }

    [Theory]
    [InlineData("List`1", "List")]
    [InlineData("Nullable`1", "Nullable")]
    public void StripArity_StripsArity_ForGenericArity1(string input, string expected)
    {
        Assert.Equal(expected, ClrNameHelper.StripArity(input));
    }

    [Theory]
    [InlineData("DefaultDict`2", "DefaultDict")]
    [InlineData("Dictionary`2", "Dictionary")]
    public void StripArity_StripsArity_ForGenericArity2(string input, string expected)
    {
        Assert.Equal(expected, ClrNameHelper.StripArity(input));
    }

    [Theory]
    [InlineData("Sharpy.DefaultDict`2", "Sharpy.DefaultDict")]
    [InlineData("System.Collections.Generic.Dictionary`2", "System.Collections.Generic.Dictionary")]
    public void StripArity_StripsArity_ForFullyQualifiedNames(string input, string expected)
    {
        Assert.Equal(expected, ClrNameHelper.StripArity(input));
    }

    [Theory]
    [InlineData("Outer+Inner`1", "Outer+Inner")]
    [InlineData("Namespace.Outer+Inner`2", "Namespace.Outer+Inner")]
    public void StripArity_StripsArity_ForNestedTypes(string input, string expected)
    {
        Assert.Equal(expected, ClrNameHelper.StripArity(input));
    }

    [Fact]
    public void StripArity_ReturnsEmpty_ForEmptyString()
    {
        Assert.Equal("", ClrNameHelper.StripArity(""));
    }

    [Fact]
    public void StripArity_StripsTrailingBacktick_WithNoDigits()
    {
        Assert.Equal("Foo", ClrNameHelper.StripArity("Foo`"));
    }
}
