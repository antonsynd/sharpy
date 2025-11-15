using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_IsSuperset_TrueSuperset_ReturnsTrue()
    {
        // Given
        Set<int> superset = [1, 2, 3, 4, 5];
        Set<int> subset = [1, 2, 3];

        // When/Then
        superset.IsSuperset(subset).Should().BeTrue();
    }

    [Fact]
    public void Set_IsSuperset_IdenticalSets_ReturnsTrue()
    {
        // Given
        Set<int> set1 = [1, 2, 3];
        Set<int> set2 = [1, 2, 3];

        // When/Then
        set1.IsSuperset(set2).Should().BeTrue();
    }

    [Fact]
    public void Set_IsSuperset_NonEmptyOfEmpty_ReturnsTrue()
    {
        // Given
        Set<int> nonEmptySet = [1, 2, 3];
        Set<int> emptySet = [];

        // When/Then
        nonEmptySet.IsSuperset(emptySet).Should().BeTrue();
    }

    [Fact]
    public void Set_IsSuperset_EmptyOfNonEmpty_ReturnsFalse()
    {
        // Given
        Set<int> emptySet = [];
        Set<int> nonEmptySet = [1, 2, 3];

        // When/Then
        emptySet.IsSuperset(nonEmptySet).Should().BeFalse();
    }

    [Fact]
    public void Set_IsSuperset_NotSuperset_ReturnsFalse()
    {
        // Given
        Set<int> set1 = [1, 2, 3, 4];
        Set<int> set2 = [2, 3, 5, 6];

        // When/Then
        set1.IsSuperset(set2).Should().BeFalse();
    }

    [Fact]
    public void Set_IsSuperset_UsingGreaterOrEqualOperator_WorksCorrectly()
    {
        // Given
        Set<int> superset = [1, 2, 3, 4, 5];
        Set<int> subset = [1, 2, 3];

        // When/Then
        (superset >= subset).Should().BeTrue();
        (subset >= superset).Should().BeFalse();
    }

    [Fact]
    public void Set_IsSuperset_UsingGreaterOperator_ProperSuperset()
    {
        // Given
        Set<int> superset = [1, 2, 3, 4, 5];
        Set<int> subset = [1, 2, 3];
        Set<int> identicalSet = [1, 2, 3, 4, 5];

        // When/Then
        (superset > subset).Should().BeTrue();  // Proper superset
        (superset > identicalSet).Should().BeFalse();  // Not proper superset (equal)
    }

    [Fact]
    public void Set_IsSuperset_WithStrings_WorksCorrectly()
    {
        // Given
        Set<string> superset = ["apple", "banana", "cherry", "date"];
        Set<string> subset = ["apple", "banana"];

        // When/Then
        superset.IsSuperset(subset).Should().BeTrue();
    }

    [Fact]
    public void Set_IsSuperset_WithPartialOverlap_ReturnsFalse()
    {
        // Given
        Set<int> set1 = [1, 2, 3, 4];
        Set<int> set2 = [3, 4, 5, 6];

        // When/Then
        set1.IsSuperset(set2).Should().BeFalse();
        set2.IsSuperset(set1).Should().BeFalse();
    }
}
