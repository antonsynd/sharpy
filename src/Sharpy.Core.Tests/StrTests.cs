using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class StrTests
{
    [Fact]
    public void Str_Char_ReturnsCharAsString()
    {
        Builtins.Str('h').Should().Be("h");
    }

    [Fact]
    public void Str_Char_DoesNotReturnAsciiCode()
    {
        Builtins.Str('h').Should().NotBe("104");
    }
}
