using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Union_WithAnotherSet_ReturnsCorrectUnion()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [3, 4, 5];

        // When
        var result = set1.Union(set2);

        // Then
        result.Should().HaveCount(5);
        result.Should().Contain(1);
        result.Should().Contain(2);
        result.Should().Contain(3);
        result.Should().Contain(4);
        result.Should().Contain(5);
    }

    [Fact]
    public void Set_Union_WithEmptySet_ReturnsOriginalSet()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [];

        // When
        var result = set1.Union(set2);

        // Then
        result.Should().HaveCount(3);
        result.Should().Contain(1);
        result.Should().Contain(2);
        result.Should().Contain(3);
    }

    [Fact]
    public void Set_Union_EmptyWithNonEmpty_ReturnsSecondSet()
    {
        // Given
        Set<int> set1 = [];
        Set<int> set2 = [1, 2, 3];

        // When
        var result = set1.Union(set2);

        // Then
        result.Should().HaveCount(3);
        result.Should().Contain(1);
        result.Should().Contain(2);
        result.Should().Contain(3);
    }

    [Fact]
    public void Set_Union_WithIdenticalSets_ReturnsSetWithSameElements()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [1, 2, 3];

        // When
        var result = set1.Union(set2);

        // Then
        result.Should().HaveCount(3);
        result.Should().Contain(1);
        result.Should().Contain(2);
        result.Should().Contain(3);
    }

    [Fact]
    public void Set_Union_WithDisjointSets_ReturnsAllElements()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [4, 5, 6];

        // When
        var result = set1.Union(set2);

        // Then
        result.Should().HaveCount(6);
        result.Should().Contain(1);
        result.Should().Contain(2);
        result.Should().Contain(3);
        result.Should().Contain(4);
        result.Should().Contain(5);
        result.Should().Contain(6);
    }

    [Fact]
    public void Set_Union_UsingOrOperator_WorksCorrectly()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [3, 4, 5];

        // When
        var result = set1 | set2;

        // Then
        result.Should().HaveCount(5);
        result.Should().Contain(1);
        result.Should().Contain(2);
        result.Should().Contain(3);
        result.Should().Contain(4);
        result.Should().Contain(5);
    }

    [Fact]
    public void Set_Union_DoesNotModifyOriginalSets()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [3, 4, 5];

        // When
        var result = set1.Union(set2);

        // Then - original sets should remain unchanged
        set1.Should().HaveCount(3);
        set1.Should().Contain(1);
        set1.Should().Contain(2);
        set1.Should().Contain(3);

        set2.Should().HaveCount(3);
        set2.Should().Contain(3);
        set2.Should().Contain(4);
        set2.Should().Contain(5);
    }

    [Fact]
    public void Set_Union_WithStrings_WorksCorrectly()
    {
        // Given
        Set<string> set1 = ["apple", "banana"];
        Set<string> set2 = ["banana", "cherry"];

        // When
        var result = set1.Union(set2);

        // Then
        result.Should().HaveCount(3);
        result.Should().Contain("apple");
        result.Should().Contain("banana");
        result.Should().Contain("cherry");
    }
}
