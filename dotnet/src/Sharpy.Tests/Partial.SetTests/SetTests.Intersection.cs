using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Intersection_WithAnotherSet_ReturnsCorrectIntersection()
    {
        // Given
        Set<int> set1 = [1, 2, 3, 4];
        Set<int> set2 = [2, 3, 4, 5];

        // When
        var result = set1.Intersection(set2);

        // Then
        result.Should().HaveCount(3);
        result.Should().Contain(2);
        result.Should().Contain(3);
        result.Should().Contain(4);
    }

    [Fact]
    public void Set_Intersection_WithEmptySet_ReturnsEmptySet()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [];

        // When
        var result = set1.Intersection(set2);

        // Then
        result.Should().BeEmpty();
    }

    [Fact]
    public void Set_Intersection_EmptyWithNonEmpty_ReturnsEmptySet()
    {
        // Given
        Set<int> set1 = [];
        Set<int> set2 = [1, 2, 3];

        // When
        var result = set1.Intersection(set2);

        // Then
        result.Should().BeEmpty();
    }

    [Fact]
    public void Set_Intersection_WithIdenticalSets_ReturnsSetWithSameElements()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [1, 2, 3];

        // When
        var result = set1.Intersection(set2);

        // Then
        result.Should().HaveCount(3);
        result.Should().Contain(1);
        result.Should().Contain(2);
        result.Should().Contain(3);
    }

    [Fact]
    public void Set_Intersection_WithDisjointSets_ReturnsEmptySet()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [4, 5, 6];

        // When
        var result = set1.Intersection(set2);

        // Then
        result.Should().BeEmpty();
    }

    [Fact]
    public void Set_Intersection_UsingAndOperator_WorksCorrectly()
    {
        // Given
        Set<int> set1 = [1, 2, 3, 4];
        Set<int> set2 = [2, 3, 4, 5];

        // When
        var result = set1 & set2;

        // Then
        result.Should().HaveCount(3);
        result.Should().Contain(2);
        result.Should().Contain(3);
        result.Should().Contain(4);
    }

    [Fact]
    public void Set_Intersection_DoesNotModifyOriginalSets()
    {
        // Given
        Set<int> set1 = [1, 2, 3, 4];
        Set<int> set2 = [2, 3, 4, 5];

        // When
        var result = set1.Intersection(set2);

        // Then - original sets should remain unchanged
        set1.Should().HaveCount(4);
        set1.Should().Contain(1);
        set1.Should().Contain(2);
        set1.Should().Contain(3);
        set1.Should().Contain(4);

        set2.Should().HaveCount(4);
        set2.Should().Contain(2);
        set2.Should().Contain(3);
        set2.Should().Contain(4);
        set2.Should().Contain(5);
    }

    [Fact]
    public void Set_Intersection_WithStrings_WorksCorrectly()
    {
        // Given
        Set<string> set1 = ["apple", "banana", "cherry"];
        Set<string> set2 = ["banana", "cherry", "date"];

        // When
        var result = set1.Intersection(set2);

        // Then
        result.Should().HaveCount(2);
        result.Should().Contain("banana");
        result.Should().Contain("cherry");
    }

    [Fact]
    public void Set_Intersection_SingleElementOverlap_ReturnsCorrectResult()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [3, 4, 5];

        // When
        var result = set1.Intersection(set2);

        // Then
        result.Should().HaveCount(1);
        result.Should().Contain(3);
    }
}
