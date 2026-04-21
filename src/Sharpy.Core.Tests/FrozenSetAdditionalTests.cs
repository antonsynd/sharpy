using Xunit;
using FluentAssertions;
using Sharpy;

namespace Sharpy.Core.Tests;

public class FrozenSetAdditionalTests
{
    // ===== Immutability: FrozenSet exposes no mutating interface =====

    [Fact]
    public void FrozenSet_TypeDoesNotImplementICollection()
    {
        var fsType = typeof(FrozenSet<int>);
        var iCollectionType = typeof(System.Collections.Generic.ICollection<int>);
        // FrozenSet<T> must NOT implement ICollection<T> (which would expose Add/Remove/Clear)
        iCollectionType.IsAssignableFrom(fsType).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_TypeDoesNotImplementISet()
    {
        var fsType = typeof(FrozenSet<int>);
        var iSetType = typeof(System.Collections.Generic.ISet<int>);
        // FrozenSet<T> must NOT implement ISet<T> (which would expose Add/Remove/etc.)
        iSetType.IsAssignableFrom(fsType).Should().BeFalse();
    }

    // ===== IsProperSubset / IsProperSuperset via < > operators =====

    [Fact]
    public void LessThan_ProperSubset_ReturnsTrue()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2, 3 });
        (fs1 < fs2).Should().BeTrue();
    }

    [Fact]
    public void LessThan_SupersetVsSubset_ReturnsFalse()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2 });
        (fs1 < fs2).Should().BeFalse();
    }

    [Fact]
    public void GreaterThan_ProperSuperset_ReturnsTrue()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2 });
        (fs1 > fs2).Should().BeTrue();
    }

    [Fact]
    public void GreaterThan_SubsetVsSuperset_ReturnsFalse()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2, 3 });
        (fs1 > fs2).Should().BeFalse();
    }

    // ===== Edge cases: operators with empty sets =====

    [Fact]
    public void Union_WithEmptySet_ReturnsNonEmpty()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        var empty = new FrozenSet<int>();

        var result = fs | empty;

        result.Count.Should().Be(3);
        result.Contains(1).Should().BeTrue();
    }

    [Fact]
    public void Intersection_WithEmptySet_ReturnsEmpty()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        var empty = new FrozenSet<int>();

        var result = fs & empty;

        result.Count.Should().Be(0);
    }

    [Fact]
    public void Difference_EmptyMinusNonEmpty_ReturnsEmpty()
    {
        var empty = new FrozenSet<int>();
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });

        var result = empty - fs;

        result.Count.Should().Be(0);
    }

    [Fact]
    public void SymmetricDifference_WithEmptySet_ReturnsSelf()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        var empty = new FrozenSet<int>();

        var result = fs ^ empty;

        result.Count.Should().Be(3);
        result.Contains(1).Should().BeTrue();
    }

    // ===== IsDisjoint FrozenSet overload =====

    [Fact]
    public void IsDisjoint_WithFrozenSet_NoCommonElements_ReturnsTrue()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2 });
        var fs2 = new FrozenSet<int>(new[] { 3, 4 });
        fs1.IsDisjoint(fs2).Should().BeTrue();
    }

    [Fact]
    public void IsDisjoint_WithFrozenSet_CommonElements_ReturnsFalse()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2 });
        var fs2 = new FrozenSet<int>(new[] { 2, 3 });
        fs1.IsDisjoint(fs2).Should().BeFalse();
    }

    // ===== Copy produces a distinct but equal instance =====

    [Fact]
    public void Copy_IsDistinctInstance()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        var copy = fs.Copy();
        copy.Should().NotBeSameAs(fs);
    }

    [Fact]
    public void Copy_ContainsSameElements()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        var copy = fs.Copy();
        (copy == fs).Should().BeTrue();
        copy.Count.Should().Be(3);
        copy.Contains(1).Should().BeTrue();
        copy.Contains(2).Should().BeTrue();
        copy.Contains(3).Should().BeTrue();
    }

    [Fact]
    public void Copy_OfEmpty_IsEmpty()
    {
        var fs = new FrozenSet<int>();
        var copy = fs.Copy();
        copy.Count.Should().Be(0);
        (copy == fs).Should().BeTrue();
    }

    // ===== GetHashCode for sets with same elements in different order =====

    [Fact]
    public void GetHashCode_SetsWithSameElementsDifferentOrder_AreEqual()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 3, 1, 2 });
        fs1.GetHashCode().Should().Be(fs2.GetHashCode());
    }

    // ===== Results of operators are also FrozenSets (immutable) =====

    [Fact]
    public void OperatorResults_AreNewFrozenSetInstances()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 3, 4, 5 });

        var union = fs1 | fs2;
        var intersection = fs1 & fs2;
        var difference = fs1 - fs2;
        var symDiff = fs1 ^ fs2;

        union.Should().NotBeSameAs(fs1);
        union.Should().NotBeSameAs(fs2);
        intersection.Should().NotBeSameAs(fs1);
        difference.Should().NotBeSameAs(fs1);
        symDiff.Should().NotBeSameAs(fs1);
    }
}
