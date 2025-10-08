using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_IsSubset_TrueSubset_ReturnsTrue()
    {
        // Given
        Set<int> subset = [1, 2, 3];
        Set<int> superset = [1, 2, 3, 4, 5];

        // When/Then
        subset.IsSubset(superset).Should().BeTrue();
    }

    [Fact]
    public void Set_IsSubset_IdenticalSets_ReturnsTrue()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [1, 2, 3];

        // When/Then
        set1.IsSubset(set2).Should().BeTrue();
    }

    [Fact]
    public void Set_IsSubset_EmptySetOfNonEmpty_ReturnsTrue()
    {
        // Given
        Set<int> emptySet = [];
        Set<int> nonEmptySet = [1, 2, 3];

        // When/Then
        emptySet.IsSubset(nonEmptySet).Should().BeTrue();
    }

    [Fact]
    public void Set_IsSubset_NonEmptyOfEmpty_ReturnsFalse()
    {
        // Given
        Set<int> nonEmptySet = [1, 2, 3];
        Set<int> emptySet = [];

        // When/Then
        nonEmptySet.IsSubset(emptySet).Should().BeFalse();
    }

    [Fact]
    public void Set_IsSubset_NotSubset_ReturnsFalse()
    {
        // Given
        Set<int> set1 = [1, 2, 3, 4];
        Set<int> set2 = [2, 3, 5, 6];

        // When/Then
        set1.IsSubset(set2).Should().BeFalse();
    }

    [Fact]
    public void Set_IsSubset_UsingLessOrEqualOperator_WorksCorrectly()
    {
        // Given
        Set<int> subset = [1, 2, 3];
        Set<int> superset = [1, 2, 3, 4, 5];

        // When/Then
        (subset <= superset).Should().BeTrue();
        (superset <= subset).Should().BeFalse();
    }

    [Fact]
    public void Set_IsSubset_UsingLessOperator_ProperSubset()
    {
        // Given
        Set<int> subset = [1, 2, 3];
        Set<int> superset = [1, 2, 3, 4, 5];
        Set<int> identicalSet = [1, 2, 3];

        // When/Then
        (subset < superset).Should().BeTrue();  // Proper subset
        (subset < identicalSet).Should().BeFalse();  // Not proper subset (equal)
    }

    [Fact]
    public void Set_IsSubset_WithStrings_WorksCorrectly()
    {
        // Given
        Set<string> subset = ["apple", "banana"];
        Set<string> superset = ["apple", "banana", "cherry", "date"];

        // When/Then
        subset.IsSubset(superset).Should().BeTrue();
    }
}
