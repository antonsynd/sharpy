using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public class All_Tests
{
    [Fact]
    public void All_EmptyList_ReturnsTrue()
    {
        // Given
        var list = new List<bool>();

        // When
        var result = All(list);

        // Then
        result.Should().BeTrue();
    }

    [Fact]
    public void All_AllTrue_ReturnsTrue()
    {
        // Given
        List<bool> list = [true, true, true];

        // When
        var result = All(list);

        // Then
        result.Should().BeTrue();
    }

    [Fact]
    public void All_OneFalse_ReturnsFalse()
    {
        // Given
        List<bool> list = [true, false, true];

        // When
        var result = All(list);

        // Then
        result.Should().BeFalse();
    }

    [Fact]
    public void All_AllIntegers_ReturnsFalseIfZero()
    {
        // Given
        List<int> list = [1, 2, 0, 4];

        // When
        var result = All(list);

        // Then
        result.Should().BeFalse();
    }

    [Fact]
    public void All_NonZeroIntegers_ReturnsTrue()
    {
        // Given
        List<int> list = [1, 2, 3, 4];

        // When
        var result = All(list);

        // Then
        result.Should().BeTrue();
    }

    [Fact]
    public void All_NullIterable_ThrowsTypeError()
    {
        // When/Then
        FluentActions.Invoking(() => All<bool>(null!))
            .Should().Throw<TypeError>();
    }
}
