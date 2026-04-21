using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class SetOperatorTests
{
    // ===== | operator (union) =====

    [Fact]
    public void UnionOperator_TwoDisjointSets_ReturnsAll()
    {
        Set<int> s1 = [1, 2, 3];
        Set<int> s2 = [4, 5, 6];

        var result = s1 | s2;

        result.Should().HaveCount(6);
        result.Contains(1).Should().BeTrue();
        result.Contains(6).Should().BeTrue();
    }

    [Fact]
    public void UnionOperator_OverlappingSets_DeduplicatesElements()
    {
        Set<int> s1 = [1, 2, 3];
        Set<int> s2 = [3, 4, 5];

        var result = s1 | s2;

        result.Should().HaveCount(5);
    }

    [Fact]
    public void UnionOperator_WithEmptySet_ReturnsCopyOfNonEmpty()
    {
        Set<int> s1 = [1, 2, 3];
        Set<int> empty = [];

        var result = s1 | empty;

        result.Should().HaveCount(3);
        result.Contains(1).Should().BeTrue();
    }

    [Fact]
    public void UnionOperator_BothEmpty_ReturnsEmpty()
    {
        Set<int> s1 = [];
        Set<int> s2 = [];

        var result = s1 | s2;

        result.Should().BeEmpty();
    }

    // ===== & operator (intersection) =====

    [Fact]
    public void IntersectionOperator_CommonElements_ReturnsCommon()
    {
        Set<int> s1 = [1, 2, 3, 4];
        Set<int> s2 = [3, 4, 5, 6];

        var result = s1 & s2;

        result.Should().HaveCount(2);
        result.Contains(3).Should().BeTrue();
        result.Contains(4).Should().BeTrue();
    }

    [Fact]
    public void IntersectionOperator_DisjointSets_ReturnsEmpty()
    {
        Set<int> s1 = [1, 2, 3];
        Set<int> s2 = [4, 5, 6];

        var result = s1 & s2;

        result.Should().BeEmpty();
    }

    [Fact]
    public void IntersectionOperator_IdenticalSets_ReturnsAllElements()
    {
        Set<int> s1 = [1, 2, 3];
        Set<int> s2 = [1, 2, 3];

        var result = s1 & s2;

        result.Should().HaveCount(3);
    }

    // ===== - operator (difference) =====

    [Fact]
    public void DifferenceOperator_ReturnsLeftMinusRight()
    {
        Set<int> s1 = [1, 2, 3, 4];
        Set<int> s2 = [3, 4, 5];

        var result = s1 - s2;

        result.Should().HaveCount(2);
        result.Contains(1).Should().BeTrue();
        result.Contains(2).Should().BeTrue();
        result.Contains(3).Should().BeFalse();
    }

    [Fact]
    public void DifferenceOperator_DisjointSets_ReturnsLeftUnchanged()
    {
        Set<int> s1 = [1, 2, 3];
        Set<int> s2 = [4, 5, 6];

        var result = s1 - s2;

        result.Should().HaveCount(3);
        result.Contains(1).Should().BeTrue();
    }

    [Fact]
    public void DifferenceOperator_SubtractFromEmpty_ReturnsEmpty()
    {
        Set<int> empty = [];
        Set<int> s2 = [1, 2, 3];

        var result = empty - s2;

        result.Should().BeEmpty();
    }

    // ===== ^ operator (symmetric difference) =====

    [Fact]
    public void SymmetricDifferenceOperator_ReturnsElementsInExactlyOne()
    {
        Set<int> s1 = [1, 2, 3];
        Set<int> s2 = [2, 3, 4];

        var result = s1 ^ s2;

        result.Should().HaveCount(2);
        result.Contains(1).Should().BeTrue();
        result.Contains(4).Should().BeTrue();
        result.Contains(2).Should().BeFalse();
        result.Contains(3).Should().BeFalse();
    }

    [Fact]
    public void SymmetricDifferenceOperator_IdenticalSets_ReturnsEmpty()
    {
        Set<int> s1 = [1, 2, 3];
        Set<int> s2 = [1, 2, 3];

        var result = s1 ^ s2;

        result.Should().BeEmpty();
    }

    // ===== operator true / false (truthiness) =====

    [Fact]
    public void OperatorTrue_EmptySet_IsFalsy()
    {
        Set<int> empty = [];
        bool result = false;
        if (empty)
            result = true;
        result.Should().BeFalse();
    }

    [Fact]
    public void OperatorTrue_NonEmptySet_IsTruthy()
    {
        Set<int> s = [42];
        bool result = false;
        if (s)
            result = true;
        result.Should().BeTrue();
    }

    [Fact]
    public void OperatorFalse_EmptySet_ReportsAsFalsy()
    {
        Set<int> empty = [];
        // operator false: empty set is falsy — verify via if-else branching
        bool enteredElse = false;
        if (empty)
        {
            // should not enter here
        }
        else
        {
            enteredElse = true;
        }
        enteredElse.Should().BeTrue();
    }

    // ===== IsProperSubset / IsProperSuperset =====

    [Fact]
    public void IsProperSubset_ProperSubset_ReturnsTrue()
    {
        Set<int> s = [1, 2];
        Set<int> other = [1, 2, 3];
        s.IsProperSubset(other).Should().BeTrue();
    }

    [Fact]
    public void IsProperSubset_EqualSets_ReturnsFalse()
    {
        Set<int> s = [1, 2, 3];
        Set<int> other = [1, 2, 3];
        s.IsProperSubset(other).Should().BeFalse();
    }

    [Fact]
    public void IsProperSubset_EmptyVsNonEmpty_ReturnsTrue()
    {
        Set<int> empty = [];
        Set<int> nonEmpty = [1];
        empty.IsProperSubset(nonEmpty).Should().BeTrue();
    }

    [Fact]
    public void IsProperSuperset_ProperSuperset_ReturnsTrue()
    {
        Set<int> s = [1, 2, 3];
        Set<int> other = [1, 2];
        s.IsProperSuperset(other).Should().BeTrue();
    }

    [Fact]
    public void IsProperSuperset_EqualSets_ReturnsFalse()
    {
        Set<int> s = [1, 2, 3];
        Set<int> other = [1, 2, 3];
        s.IsProperSuperset(other).Should().BeFalse();
    }

    // ===== Overlaps =====

    [Fact]
    public void Overlaps_SharedElements_ReturnsTrue()
    {
        Set<int> s = [1, 2, 3];
        s.Overlaps(new[] { 3, 4, 5 }).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_NoSharedElements_ReturnsFalse()
    {
        Set<int> s = [1, 2, 3];
        s.Overlaps(new[] { 4, 5, 6 }).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_EmptyEnumerable_ReturnsFalse()
    {
        Set<int> s = [1, 2, 3];
        s.Overlaps(Array.Empty<int>()).Should().BeFalse();
    }

    // ===== SetEquals =====

    [Fact]
    public void SetEquals_SameElements_ReturnsTrue()
    {
        Set<int> s = [1, 2, 3];
        s.SetEquals(new[] { 3, 1, 2 }).Should().BeTrue();
    }

    [Fact]
    public void SetEquals_DifferentElements_ReturnsFalse()
    {
        Set<int> s = [1, 2, 3];
        s.SetEquals(new[] { 1, 2, 4 }).Should().BeFalse();
    }

    [Fact]
    public void SetEquals_Subset_ReturnsFalse()
    {
        Set<int> s = [1, 2, 3];
        s.SetEquals(new[] { 1, 2 }).Should().BeFalse();
    }

    [Fact]
    public void SetEquals_EmptyWithEmpty_ReturnsTrue()
    {
        Set<int> s = [];
        s.SetEquals(Array.Empty<int>()).Should().BeTrue();
    }

    // ===== Operators do not mutate operands =====

    [Fact]
    public void AllOperators_DoNotMutateOperands()
    {
        Set<int> s1 = [1, 2, 3];
        Set<int> s2 = [2, 3, 4];

        var _ = s1 | s2;
        var __ = s1 & s2;
        var ___ = s1 - s2;
        var ____ = s1 ^ s2;

        s1.Should().HaveCount(3);
        s1.Contains(1).Should().BeTrue();
        s1.Contains(2).Should().BeTrue();
        s1.Contains(3).Should().BeTrue();
        s2.Should().HaveCount(3);
    }
}
