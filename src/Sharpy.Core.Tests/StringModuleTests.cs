using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class StringModule_Tests
{
    [Fact]
    public void AsciiLowercase_MatchesPython()
    {
        Sharpy.String.ascii_lowercase.Should().Be("abcdefghijklmnopqrstuvwxyz");
    }

    [Fact]
    public void AsciiUppercase_MatchesPython()
    {
        Sharpy.String.ascii_uppercase.Should().Be("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
    }

    [Fact]
    public void AsciiLetters_IsConcatenationOfLowercaseAndUppercase()
    {
        Sharpy.String.ascii_letters.Should().Be(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
    }

    [Fact]
    public void Digits_MatchesPython()
    {
        Sharpy.String.digits.Should().Be("0123456789");
    }

    [Fact]
    public void Hexdigits_MatchesPython()
    {
        Sharpy.String.hexdigits.Should().Be("0123456789abcdefABCDEF");
    }

    [Fact]
    public void Octdigits_MatchesPython()
    {
        Sharpy.String.octdigits.Should().Be("01234567");
    }

    [Fact]
    public void Punctuation_MatchesPython()
    {
        // Python: '!"#$%&\'()*+,-./:;<=>?@[\\]^_`{|}~'
        Sharpy.String.punctuation.Should().Be("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~");
    }

    [Fact]
    public void Whitespace_MatchesPython()
    {
        // Python: ' \t\n\r\x0b\x0c'
        Sharpy.String.whitespace.Should().Be(" \t\n\r\x0b\x0c");
    }

    [Fact]
    public void Printable_MatchesPython()
    {
        // Python: digits + ascii_letters + punctuation + whitespace
        string expected = "0123456789"
            + "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
            + "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~"
            + " \t\n\r\x0b\x0c";
        Sharpy.String.printable.Should().Be(expected);
    }

    [Fact]
    public void Printable_HasCorrectLength()
    {
        // 10 digits + 52 letters + 32 punctuation + 6 whitespace = 100
        Sharpy.String.printable.Length.Should().Be(100);
    }

    [Fact]
    public void Punctuation_HasCorrectLength()
    {
        // Python: len(string.punctuation) == 32
        Sharpy.String.punctuation.Length.Should().Be(32);
    }

    [Fact]
    public void Whitespace_HasCorrectLength()
    {
        // Python: len(string.whitespace) == 6
        Sharpy.String.whitespace.Length.Should().Be(6);
    }
}
