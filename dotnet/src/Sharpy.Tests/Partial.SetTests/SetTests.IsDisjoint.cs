using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_IsDisjoint_WithNoOverlap_ReturnsTrue()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [4, 5, 6];

        // When/Then
        set1.IsDisjoint(set2).Should().BeTrue();
        set2.IsDisjoint(set1).Should().BeTrue();
    }

    [Fact]
    public void Set_IsDisjoint_WithOverlap_ReturnsFalse()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [3, 4, 5];

        // When/Then
        set1.IsDisjoint(set2).Should().BeFalse();
        set2.IsDisjoint(set1).Should().BeFalse();
    }

    [Fact]
    public void Set_IsDisjoint_WithIdenticalSets_ReturnsFalse()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [1, 2, 3];

        // When/Then
        set1.IsDisjoint(set2).Should().BeFalse();
    }

    [Fact]
    public void Set_IsDisjoint_WithEmptySet_ReturnsTrue()
    {
        // Given
        Set<int> nonEmptySet = [1, 2, 3];
        Set<int> emptySet = [];

        // When/Then
        nonEmptySet.IsDisjoint(emptySet).Should().BeTrue();
        emptySet.IsDisjoint(nonEmptySet).Should().BeTrue();
    }

    [Fact]
    public void Set_IsDisjoint_BothEmpty_ReturnsTrue()
    {
        // Given
        Set<int> emptySet1 = [];
        Set<int> emptySet2 = [];

        // When/Then
        emptySet1.IsDisjoint(emptySet2).Should().BeTrue();
    }

    [Fact]
    public void Set_IsDisjoint_WithSingleElementOverlap_ReturnsFalse()
    {
        // Given
        Set<int> set1 = [1, 2, 3, 4];
        Set<int> set2 = [4, 5, 6, 7];

        // When/Then
        set1.IsDisjoint(set2).Should().BeFalse();
    }

    [Fact]
    public void Set_IsDisjoint_WithStrings_WorksCorrectly()
    {
        // Given
        Set<string> set1 = ["apple", "banana"];
        Set<string> set2 = ["cherry", "date"];
        Set<string> set3 = ["banana", "elderberry"];

        // When/Then
        set1.IsDisjoint(set2).Should().BeTrue();
        set1.IsDisjoint(set3).Should().BeFalse();
    }

    [Fact]
    public void Set_IsDisjoint_WithCompleteOverlap_ReturnsFalse()
    {
        // Given
        Set<int> subset = [1, 2];
        Set<int> superset = [1, 2, 3, 4];

        // When/Then
        subset.IsDisjoint(superset).Should().BeFalse();
        superset.IsDisjoint(subset).Should().BeFalse();
    }
}
