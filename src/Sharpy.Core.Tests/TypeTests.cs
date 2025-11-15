using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Type_Tests
{
    [Fact]
    public void Type_Integer_ReturnsIntType()
    {
        // Given
        object obj = 42;

        // When
        var result = Type(obj);

        // Then
        result.Should().Be(typeof(int));
    }

    [Fact]
    public void Type_String_ReturnsStringType()
    {
        // Given
        object obj = "hello";

        // When
        var result = Type(obj);

        // Then
        result.Should().Be(typeof(string));
    }

    [Fact]
    public void Type_List_ReturnsListType()
    {
        // Given
        var obj = new List<int>();

        // When
        var result = Type(obj);

        // Then
        result.Name.Should().Contain("List");
    }

    [Fact]
    public void Type_Null_ReturnsObjectType()
    {
        // When
        var result = Type(null);

        // Then
        result.Should().Be(typeof(object));
    }
}
