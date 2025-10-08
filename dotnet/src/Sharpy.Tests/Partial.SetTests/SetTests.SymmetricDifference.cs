using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_SymmetricDifference_WithAnotherSet_ReturnsCorrectSymmetricDifference()
    {
        // Given
        Set<int> set1 = [1, 2, 3, 4];
        Set<int> set2 = [3, 4, 5, 6];

        // When
        var result = set1.SymmetricDifference(set2);

        // Then
        result.Should().HaveCount(4);
        result.Should().Contain(1);
        result.Should().Contain(2);
        result.Should().Contain(5);
        result.Should().Contain(6);
        result.Should().NotContain(3);
        result.Should().NotContain(4);
    }

    [Fact]
    public void Set_SymmetricDifference_WithEmptySet_ReturnsOriginalSet()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [];

        // When
        var result = set1.SymmetricDifference(set2);

        // Then
        result.Should().HaveCount(3);
        result.Should().Contain(1);
        result.Should().Contain(2);
        result.Should().Contain(3);
    }

    [Fact]
    public void Set_SymmetricDifference_EmptyWithNonEmpty_ReturnsSecondSet()
    {
        // Given
        Set<int> set1 = [];
        Set<int> set2 = [1, 2, 3];

        // When
        var result = set1.SymmetricDifference(set2);

        // Then
        result.Should().HaveCount(3);
        result.Should().Contain(1);
        result.Should().Contain(2);
        result.Should().Contain(3);
    }

    [Fact]
    public void Set_SymmetricDifference_WithIdenticalSets_ReturnsEmptySet()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [1, 2, 3];

        // When
        var result = set1.SymmetricDifference(set2);

        // Then
        result.Should().BeEmpty();
    }

    [Fact]
    public void Set_SymmetricDifference_WithDisjointSets_ReturnsAllElements()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [4, 5, 6];

        // When
        var result = set1.SymmetricDifference(set2);

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
    public void Set_SymmetricDifference_UsingXorOperator_WorksCorrectly()
    {
        // Given
        Set<int> set1 = [1, 2, 3, 4];
        Set<int> set2 = [3, 4, 5, 6];

        // When
        var result = set1 ^ set2;

        // Then
        result.Should().HaveCount(4);
        result.Should().Contain(1);
        result.Should().Contain(2);
        result.Should().Contain(5);
        result.Should().Contain(6);
        result.Should().NotContain(3);
        result.Should().NotContain(4);
    }

    [Fact]
    public void Set_SymmetricDifference_DoesNotModifyOriginalSets()
    {
        // Given
        Set<int> set1 = [1, 2, 3, 4];
        Set<int> set2 = [3, 4, 5, 6];

        // When
        var result = set1.SymmetricDifference(set2);

        // Then - original sets should remain unchanged
        set1.Should().HaveCount(4);
        set1.Should().Contain(1);
        set1.Should().Contain(2);
        set1.Should().Contain(3);
        set1.Should().Contain(4);

        set2.Should().HaveCount(4);
        set2.Should().Contain(3);
        set2.Should().Contain(4);
        set2.Should().Contain(5);
        set2.Should().Contain(6);
    }

    [Fact]
    public void Set_SymmetricDifference_WithStrings_WorksCorrectly()
    {
        // Given
        Set<string> set1 = ["apple", "banana", "cherry"];
        Set<string> set2 = ["banana", "date", "elderberry"];

        // When
        var result = set1.SymmetricDifference(set2);

        // Then
        result.Should().HaveCount(4);
        result.Should().Contain("apple");
        result.Should().Contain("cherry");
        result.Should().Contain("date");
        result.Should().Contain("elderberry");
        result.Should().NotContain("banana");
    }

    [Fact]
    public void Set_SymmetricDifference_SingleElementOverlap_ReturnsCorrectResult()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [3, 4, 5];

        // When
        var result = set1.SymmetricDifference(set2);

        // Then
        result.Should().HaveCount(4);
        result.Should().Contain(1);
        result.Should().Contain(2);
        result.Should().Contain(4);
        result.Should().Contain(5);
        result.Should().NotContain(3);
    }
}
