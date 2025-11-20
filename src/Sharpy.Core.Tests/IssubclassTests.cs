using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Issubclass_Tests
{
    [Fact]
    public void Issubclass_DerivedClass_ReturnsTrue()
    {
        // Given
        Type derivedType = typeof(ArgumentException);
        Type baseType = typeof(Exception);

        // When
        var result = Issubclass(derivedType, baseType);

        // Then
        result.Should().BeTrue();
    }

    [Fact]
    public void Issubclass_SameClass_ReturnsTrue()
    {
        // Given
        Type type = typeof(string);

        // When
        var result = Issubclass(type, typeof(string));

        // Then
        result.Should().BeTrue();
    }

    [Fact]
    public void Issubclass_UnrelatedClass_ReturnsFalse()
    {
        // Given
        Type type1 = typeof(string);
        Type type2 = typeof(int);

        // When
        var result = Issubclass(type1, type2);

        // Then
        result.Should().BeFalse();
    }

    [Fact]
    public void Issubclass_MultipleTypes_MatchesOne_ReturnsTrue()
    {
        // Given
        Type derivedType = typeof(ArgumentException);

        // When
        var result = Issubclass(derivedType, typeof(string), typeof(Exception), typeof(int));

        // Then
        result.Should().BeTrue();
    }

    [Fact]
    public void Issubclass_MultipleTypes_MatchesNone_ReturnsFalse()
    {
        // Given
        Type type = typeof(double);

        // When
        var result = Issubclass(type, typeof(string), typeof(Exception), typeof(int));

        // Then
        result.Should().BeFalse();
    }

    [Fact]
    public void Issubclass_NullClass_ThrowsTypeError()
    {
        // When/Then
        FluentActions.Invoking(() => Issubclass(null!, typeof(string)))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Issubclass_NullClassInfo_ThrowsTypeError()
    {
        // When/Then
        FluentActions.Invoking(() => Issubclass(typeof(string), (Type)null!))
            .Should().Throw<TypeError>();
    }
}
