using Sharpy.Compiler.Utilities;
using Xunit;

namespace Sharpy.Compiler.Tests.Utilities;

public class EditDistanceTests
{
    [Theory]
    [InlineData("print", "prnt", 1)]
    [InlineData("class", "clss", 1)]
    [InlineData("hello", "world", 4)]
    [InlineData("abc", "abc", 0)]
    [InlineData("", "", 0)]
    [InlineData("abc", "", 3)]
    [InlineData("", "abc", 3)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("my_function", "my_functon", 1)]
    public void Compute_ReturnsExpectedDistance(string a, string b, int expected)
    {
        Assert.Equal(expected, EditDistance.Compute(a, b));
    }

    [Fact]
    public void Compute_NullInput_ReturnsMaxValue()
    {
        Assert.Equal(int.MaxValue, EditDistance.Compute(null, "test"));
        Assert.Equal(int.MaxValue, EditDistance.Compute("test", null));
        Assert.Equal(int.MaxValue, EditDistance.Compute(null, null));
    }

    [Fact]
    public void Compute_IsCaseInsensitive()
    {
        Assert.Equal(0, EditDistance.Compute("Hello", "hello"));
        Assert.Equal(0, EditDistance.Compute("ABC", "abc"));
    }

    [Fact]
    public void FindClosestMatch_FindsBestCandidate()
    {
        var candidates = new[] { "print", "input", "int", "float", "str" };
        Assert.Equal("print", EditDistance.FindClosestMatch("prnt", candidates));
    }

    [Fact]
    public void FindClosestMatch_ReturnsNull_WhenNoCloseMatch()
    {
        var candidates = new[] { "print", "input", "float" };
        Assert.Null(EditDistance.FindClosestMatch("xyz", candidates));
    }

    [Fact]
    public void FindClosestMatch_ReturnsNull_ForShortNames()
    {
        var candidates = new[] { "x", "y", "z", "a", "b" };
        // Names of length <= 2 should not get suggestions
        Assert.Null(EditDistance.FindClosestMatch("xx", candidates));
    }

    [Fact]
    public void FindClosestMatch_SkipsExactMatch()
    {
        var candidates = new[] { "print", "prnt" };
        Assert.Equal("print", EditDistance.FindClosestMatch("prnt", candidates));
    }

    [Fact]
    public void FindClosestMatch_ReturnsOriginalCasing()
    {
        var candidates = new[] { "MyFunction", "other_func" };
        Assert.Equal("MyFunction", EditDistance.FindClosestMatch("myfunction", candidates));
    }

    [Fact]
    public void FindClosestMatch_WithMultipleTiedCandidates_ReturnsFirst()
    {
        // "ab" is distance 1 from both "abc" and "abd"
        var candidates = new[] { "abc", "abd" };
        var result = EditDistance.FindClosestMatch("abx", candidates);
        Assert.Equal("abc", result);
    }

    [Fact]
    public void FindClosestMatch_RespectsMaxDistance()
    {
        var candidates = new[] { "completely_different" };
        Assert.Null(EditDistance.FindClosestMatch("print", candidates, maxDistance: 2));
    }
}
