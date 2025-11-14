using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public class Any_Tests
{
    [Fact]
    public void Any_EmptyList_ReturnsFalse()
    {
        // Given
        var list = new List<bool>();

        // When
        var result = Any(list);

        // Then
        result.Should().BeFalse();
    }

    [Fact]
    public void Any_AllFalse_ReturnsFalse()
    {
        // Given
        List<bool> list = [false, false, false];

        // When
        var result = Any(list);

        // Then
        result.Should().BeFalse();
    }

    [Fact]
    public void Any_OneTrue_ReturnsTrue()
    {
        // Given
        List<bool> list = [false, true, false];

        // When
        var result = Any(list);

        // Then
        result.Should().BeTrue();
    }

    [Fact]
    public void Any_AllZeroIntegers_ReturnsFalse()
    {
        // Given
        List<int> list = [0, 0, 0];

        // When
        var result = Any(list);

        // Then
        result.Should().BeFalse();
    }

    [Fact]
    public void Any_OneNonZeroInteger_ReturnsTrue()
    {
        // Given
        List<int> list = [0, 1, 0];

        // When
        var result = Any(list);

        // Then
        result.Should().BeTrue();
    }

    [Fact]
    public void Any_NullIterable_ThrowsTypeError()
    {
        // When/Then
        FluentActions.Invoking(() => Any<bool>(null!))
            .Should().Throw<TypeError>();
    }
}
