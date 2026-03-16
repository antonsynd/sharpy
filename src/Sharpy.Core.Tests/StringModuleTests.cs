using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class StringModule_Tests
{
    [Fact]
    public void AsciiLowercase_MatchesPython()
    {
        Sharpy.StringModule.ascii_lowercase.Should().Be("abcdefghijklmnopqrstuvwxyz");
    }

    [Fact]
    public void AsciiUppercase_MatchesPython()
    {
        Sharpy.StringModule.ascii_uppercase.Should().Be("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
    }

    [Fact]
    public void AsciiLetters_IsConcatenationOfLowercaseAndUppercase()
    {
        Sharpy.StringModule.ascii_letters.Should().Be(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
    }

    [Fact]
    public void Digits_MatchesPython()
    {
        Sharpy.StringModule.digits.Should().Be("0123456789");
    }

    [Fact]
    public void Hexdigits_MatchesPython()
    {
        Sharpy.StringModule.hexdigits.Should().Be("0123456789abcdefABCDEF");
    }

    [Fact]
    public void Octdigits_MatchesPython()
    {
        Sharpy.StringModule.octdigits.Should().Be("01234567");
    }

    [Fact]
    public void Punctuation_MatchesPython()
    {
        // Python: '!"#$%&\'()*+,-./:;<=>?@[\\]^_`{|}~'
        Sharpy.StringModule.punctuation.Should().Be("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~");
    }

    [Fact]
    public void Whitespace_MatchesPython()
    {
        // Python: ' \t\n\r\x0b\x0c'
        Sharpy.StringModule.whitespace.Should().Be(" \t\n\r\x0b\x0c");
    }

    [Fact]
    public void Printable_MatchesPython()
    {
        // Python: digits + ascii_letters + punctuation + whitespace
        string expected = "0123456789"
            + "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
            + "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~"
            + " \t\n\r\x0b\x0c";
        Sharpy.StringModule.printable.Should().Be(expected);
    }

    [Fact]
    public void Printable_HasCorrectLength()
    {
        // 10 digits + 52 letters + 32 punctuation + 6 whitespace = 100
        Sharpy.StringModule.printable.Length.Should().Be(100);
    }

    [Fact]
    public void Punctuation_HasCorrectLength()
    {
        // Python: len(string.punctuation) == 32
        Sharpy.StringModule.punctuation.Length.Should().Be(32);
    }

    [Fact]
    public void Whitespace_HasCorrectLength()
    {
        // Python: len(string.whitespace) == 6
        Sharpy.StringModule.whitespace.Length.Should().Be(6);
    }
}
