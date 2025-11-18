using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Isinstance_Tests
{
    [Fact]
    public void IsInstance_Generic_CorrectType_ReturnsTrue()
    {
        // Given
        object obj = "hello";

        // When
        var result = Isinstance<string>(obj);

        // Then
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInstance_Generic_WrongType_ReturnsFalse()
    {
        // Given
        object obj = 42;

        // When
        var result = Isinstance<string>(obj);

        // Then
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInstance_RuntimeType_CorrectType_ReturnsTrue()
    {
        // Given
        object obj = 42;

        // When
        var result = Isinstance(obj, typeof(int));

        // Then
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInstance_RuntimeType_WrongType_ReturnsFalse()
    {
        // Given
        object obj = "hello";

        // When
        var result = Isinstance(obj, typeof(int));

        // Then
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInstance_MultipleTypes_MatchesOne_ReturnsTrue()
    {
        // Given
        object obj = "hello";

        // When
        var result = Isinstance(obj, typeof(int), typeof(string), typeof(bool));

        // Then
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInstance_MultipleTypes_MatchesNone_ReturnsFalse()
    {
        // Given
        object obj = 3.14;

        // When
        var result = Isinstance(obj, typeof(int), typeof(string), typeof(bool));

        // Then
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInstance_NullObject_ReturnsFalse()
    {
        // When
        var result = Isinstance(null, typeof(string));

        // Then
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInstance_NullType_ThrowsTypeError()
    {
        // Given
        object obj = "hello";

        // When/Then
        FluentActions.Invoking(() => Isinstance(obj, (Type)null!))
            .Should().Throw<TypeError>();
    }
}
