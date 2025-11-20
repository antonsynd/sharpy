using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Format_Tests
{
    [Fact]
    public void Format_SimpleString_ReturnsString()
    {
        // Given
        string value = "hello";

        // When
        var result = Format(value);

        // Then
        result.Should().Be("hello");
    }

    [Fact]
    public void Format_Integer_ReturnsString()
    {
        // Given
        int value = 42;

        // When
        var result = Format(value);

        // Then
        result.Should().Be("42");
    }

    [Fact]
    public void Format_IntegerWithSpec_ReturnsFormattedString()
    {
        // Given
        int value = 42;

        // When
        var result = Format(value, "X");

        // Then
        result.Should().Be("2A"); // Hexadecimal
    }

    [Fact]
    public void Format_DoubleWithSpec_ReturnsFormattedString()
    {
        // Given
        double value = 3.14159;

        // When
        var result = Format(value, "F2");

        // Then
        result.Should().Be("3.14");
    }

    [Fact]
    public void Format_Null_ReturnsNone()
    {
        // When
        var result = Format((object?)null);

        // Then
        result.Should().Be("None");
    }
}
