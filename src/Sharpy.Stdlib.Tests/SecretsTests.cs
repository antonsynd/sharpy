using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class SecretsTests
{
    [Fact]
    public void TokenBytes_ReturnsCorrectLength()
    {
        var result = Sharpy.SecretsModule.TokenBytes(16);
        result.Length.Should().Be(16);
    }

    [Fact]
    public void TokenBytes_DefaultLength32()
    {
        var result = Sharpy.SecretsModule.TokenBytes();
        result.Length.Should().Be(32);
    }

    [Fact]
    public void TokenBytes_ZeroReturnsEmpty()
    {
        var result = Sharpy.SecretsModule.TokenBytes(0);
        result.Length.Should().Be(0);
    }

    [Fact]
    public void TokenBytes_NegativeThrows()
    {
        var act = () => Sharpy.SecretsModule.TokenBytes(-1);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void TokenHex_ReturnsCorrectLength()
    {
        var result = Sharpy.SecretsModule.TokenHex(16);
        result.Should().HaveLength(32);
    }

    [Fact]
    public void TokenHex_ContainsOnlyHexChars()
    {
        var result = Sharpy.SecretsModule.TokenHex(32);
        result.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void TokenHex_ZeroReturnsEmpty()
    {
        Sharpy.SecretsModule.TokenHex(0).Should().BeEmpty();
    }

    [Fact]
    public void TokenUrlsafe_ReturnsUrlSafeChars()
    {
        var result = Sharpy.SecretsModule.TokenUrlsafe(32);
        result.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public void TokenUrlsafe_ZeroReturnsEmpty()
    {
        Sharpy.SecretsModule.TokenUrlsafe(0).Should().BeEmpty();
    }

    [Fact]
    public void Randbelow_ReturnsInRange()
    {
        for (int i = 0; i < 100; i++)
        {
            var val = Sharpy.SecretsModule.Randbelow(10);
            val.Should().BeGreaterThanOrEqualTo(0);
            val.Should().BeLessThan(10);
        }
    }

    [Fact]
    public void Randbelow_OneAlwaysReturnsZero()
    {
        for (int i = 0; i < 10; i++)
        {
            Sharpy.SecretsModule.Randbelow(1).Should().Be(0);
        }
    }

    [Fact]
    public void Randbelow_ZeroThrows()
    {
        var act = () => Sharpy.SecretsModule.Randbelow(0);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Randbelow_NegativeThrows()
    {
        var act = () => Sharpy.SecretsModule.Randbelow(-5);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Choice_ReturnsElementFromList()
    {
        var items = new List<int>(new[] { 10, 20, 30 });
        var result = Sharpy.SecretsModule.Choice(items);
        items.Should().Contain(result);
    }

    [Fact]
    public void Choice_EmptyListThrows()
    {
        var act = () => Sharpy.SecretsModule.Choice(new List<int>());
        act.Should().Throw<IndexError>();
    }

    [Fact]
    public void CompareDigest_MatchingStrings()
    {
        Sharpy.SecretsModule.CompareDigest("hello", "hello").Should().BeTrue();
    }

    [Fact]
    public void CompareDigest_NonMatchingStrings()
    {
        Sharpy.SecretsModule.CompareDigest("hello", "world").Should().BeFalse();
    }

    [Fact]
    public void CompareDigest_DifferentLengths()
    {
        Sharpy.SecretsModule.CompareDigest("short", "longer string").Should().BeFalse();
    }

    [Fact]
    public void CompareDigest_EmptyStrings()
    {
        Sharpy.SecretsModule.CompareDigest("", "").Should().BeTrue();
    }

    [Fact]
    public void CompareDigest_MatchingBytes()
    {
        var a = new Bytes(new byte[] { 1, 2, 3 });
        var b = new Bytes(new byte[] { 1, 2, 3 });
        Sharpy.SecretsModule.CompareDigest(a, b).Should().BeTrue();
    }

    [Fact]
    public void CompareDigest_NonMatchingBytes()
    {
        var a = new Bytes(new byte[] { 1, 2, 3 });
        var b = new Bytes(new byte[] { 4, 5, 6 });
        Sharpy.SecretsModule.CompareDigest(a, b).Should().BeFalse();
    }
}
