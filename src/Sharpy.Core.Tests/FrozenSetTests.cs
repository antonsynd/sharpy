using Xunit;
using FluentAssertions;
using Sharpy.Core;

namespace Sharpy.Core.Tests;

public class FrozenSetTests
{
    // ===== Constructor Tests =====

    [Fact]
    public void FrozenSet_EmptyConstructor_CreatesEmptySet()
    {
        var fs = new FrozenSet<int>();
        fs.Count.Should().Be(0);
    }

    [Fact]
    public void FrozenSet_FromEnumerable_CreatesSetWithElements()
    {
        var source = new[] { 1, 2, 3, 2, 1 };
        var fs = new FrozenSet<int>(source);

        fs.Count.Should().Be(3);
        fs.Contains(1).Should().BeTrue();
        fs.Contains(2).Should().BeTrue();
        fs.Contains(3).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_FromNull_ThrowsTypeError()
    {
        var act = () => new FrozenSet<int>(null!);
        act.Should().Throw<TypeError>();
    }

    // ===== Count/Contains Tests =====

    [Fact]
    public void FrozenSet_Count_ReturnsCorrectCount()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        fs.Count.Should().Be(3);
    }

    [Fact]
    public void FrozenSet_Contains_ReturnsTrueForExistingElement()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        fs.Contains(2).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_Contains_ReturnsFalseForMissingElement()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        fs.Contains(5).Should().BeFalse();
    }

    // ===== Equality Tests =====

    [Fact]
    public void FrozenSet_Equality_SameElements_AreEqual()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 3, 2, 1 }); // Different order

        (fs1 == fs2).Should().BeTrue();
        fs1.Equals(fs2).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_Equality_DifferentElements_AreNotEqual()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2, 4 });

        (fs1 == fs2).Should().BeFalse();
        fs1.Equals(fs2).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_Equality_WithNull_ReturnsFalse()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });

        (fs == null).Should().BeFalse();
        fs.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_Inequality_DifferentElements_ReturnsTrue()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2, 4 });

        (fs1 != fs2).Should().BeTrue();
    }

    // ===== GetHashCode Tests =====

    [Fact]
    public void FrozenSet_GetHashCode_EqualSets_HaveSameHash()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 3, 2, 1 });

        fs1.GetHashCode().Should().Be(fs2.GetHashCode());
    }

    [Fact]
    public void FrozenSet_GetHashCode_EmptySets_HaveSameHash()
    {
        var fs1 = new FrozenSet<int>();
        var fs2 = new FrozenSet<int>();

        fs1.GetHashCode().Should().Be(fs2.GetHashCode());
    }

    // ===== Dict Key Tests (Crucial Use Case) =====

    [Fact]
    public void FrozenSet_CanBeUsedAsDictKey()
    {
        var key1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var key2 = new FrozenSet<int>(new[] { 3, 2, 1 }); // Same elements, different order

        var dict = new Dict<FrozenSet<int>, string>();
        dict[key1] = "value1";

        // key2 has same elements, should retrieve the same value
        dict[key2].Should().Be("value1");
    }

    [Fact]
    public void FrozenSet_DifferentSets_AreDifferentKeys()
    {
        var key1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var key2 = new FrozenSet<int>(new[] { 1, 2, 4 });

        var dict = new Dict<FrozenSet<int>, string>();
        dict[key1] = "value1";
        dict[key2] = "value2";

        dict[key1].Should().Be("value1");
        dict[key2].Should().Be("value2");
        dict.Count.Should().Be(2);
    }

    // ===== Set Operator Tests =====

    [Fact]
    public void FrozenSet_Union_Operator_ReturnsCorrectUnion()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 3, 4, 5 });

        var result = fs1 | fs2;

        result.Count.Should().Be(5);
        result.Contains(1).Should().BeTrue();
        result.Contains(2).Should().BeTrue();
        result.Contains(3).Should().BeTrue();
        result.Contains(4).Should().BeTrue();
        result.Contains(5).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_Intersection_Operator_ReturnsCorrectIntersection()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 2, 3, 4 });

        var result = fs1 & fs2;

        result.Count.Should().Be(2);
        result.Contains(2).Should().BeTrue();
        result.Contains(3).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_Difference_Operator_ReturnsCorrectDifference()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 2, 3, 4 });

        var result = fs1 - fs2;

        result.Count.Should().Be(1);
        result.Contains(1).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_SymmetricDifference_Operator_ReturnsCorrectSymmetricDifference()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 2, 3, 4 });

        var result = fs1 ^ fs2;

        result.Count.Should().Be(2);
        result.Contains(1).Should().BeTrue();
        result.Contains(4).Should().BeTrue();
    }

    // ===== Comparison Operator Tests =====

    [Fact]
    public void FrozenSet_LessThan_ProperSubset_ReturnsTrue()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2, 3 });

        (fs1 < fs2).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_LessThan_EqualSets_ReturnsFalse()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2, 3 });

        (fs1 < fs2).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_LessThanOrEqual_Subset_ReturnsTrue()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2, 3 });

        (fs1 <= fs2).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_LessThanOrEqual_EqualSets_ReturnsTrue()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2, 3 });

        (fs1 <= fs2).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_GreaterThan_ProperSuperset_ReturnsTrue()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2 });

        (fs1 > fs2).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_GreaterThanOrEqual_Superset_ReturnsTrue()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2 });

        (fs1 >= fs2).Should().BeTrue();
    }

    // ===== Truthiness Tests =====

    [Fact]
    public void FrozenSet_EmptySet_IsFalsy()
    {
        var fs = new FrozenSet<int>();

        // Test using if statement which invokes operator true/false
        bool result = false;
        if (fs)
            result = true;

        result.Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_NonEmptySet_IsTruthy()
    {
        var fs = new FrozenSet<int>(new[] { 1 });

        bool result = false;
        if (fs)
            result = true;

        result.Should().BeTrue();
    }

    // ===== Iteration Tests =====

    [Fact]
    public void FrozenSet_Iteration_YieldsAllElements()
    {
        var source = new[] { 1, 2, 3 };
        var fs = new FrozenSet<int>(source);

        var elements = new System.Collections.Generic.List<int>();
        foreach (var item in fs)
        {
            elements.Add(item);
        }

        elements.Should().HaveCount(3);
        elements.Should().Contain(1);
        elements.Should().Contain(2);
        elements.Should().Contain(3);
    }

    // ===== Python-style Method Tests =====

    [Fact]
    public void FrozenSet_Union_Method_ReturnsCorrectUnion()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 3, 4, 5 });

        var result = fs1.Union(fs2);

        result.Count.Should().Be(5);
    }

    [Fact]
    public void FrozenSet_Union_WithIEnumerable_ReturnsCorrectUnion()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        var enumerable = new[] { 3, 4, 5 };

        var result = fs.Union(enumerable);

        result.Count.Should().Be(5);
    }

    [Fact]
    public void FrozenSet_Intersection_Method_ReturnsCorrectIntersection()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 2, 3, 4 });

        var result = fs1.Intersection(fs2);

        result.Count.Should().Be(2);
    }

    [Fact]
    public void FrozenSet_Intersection_WithIEnumerable_ReturnsCorrectIntersection()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        var enumerable = new[] { 2, 3, 4 };

        var result = fs.Intersection(enumerable);

        result.Count.Should().Be(2);
    }

    [Fact]
    public void FrozenSet_Difference_Method_ReturnsCorrectDifference()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 2, 3, 4 });

        var result = fs1.Difference(fs2);

        result.Count.Should().Be(1);
        result.Contains(1).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_Difference_WithIEnumerable_ReturnsCorrectDifference()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        var enumerable = new[] { 2, 3, 4 };

        var result = fs.Difference(enumerable);

        result.Count.Should().Be(1);
        result.Contains(1).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_SymmetricDifference_Method_ReturnsCorrectResult()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 2, 3, 4 });

        var result = fs1.SymmetricDifference(fs2);

        result.Count.Should().Be(2);
        result.Contains(1).Should().BeTrue();
        result.Contains(4).Should().BeTrue();
    }

    [Fact]
    public void FrozenSet_SymmetricDifference_WithIEnumerable_ReturnsCorrectResult()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        var enumerable = new[] { 2, 3, 4 };

        var result = fs.SymmetricDifference(enumerable);

        result.Count.Should().Be(2);
    }

    [Fact]
    public void FrozenSet_IsSubset_ReturnsCorrectResult()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2, 3 });

        fs1.IsSubset(fs2).Should().BeTrue();
        fs2.IsSubset(fs1).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_IsSuperset_ReturnsCorrectResult()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2, 3 });
        var fs2 = new FrozenSet<int>(new[] { 1, 2 });

        fs1.IsSuperset(fs2).Should().BeTrue();
        fs2.IsSuperset(fs1).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_IsDisjoint_WithFrozenSet_ReturnsCorrectResult()
    {
        var fs1 = new FrozenSet<int>(new[] { 1, 2 });
        var fs2 = new FrozenSet<int>(new[] { 3, 4 });
        var fs3 = new FrozenSet<int>(new[] { 2, 3 });

        fs1.IsDisjoint(fs2).Should().BeTrue();
        fs1.IsDisjoint(fs3).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_IsDisjoint_WithIEnumerable_ReturnsCorrectResult()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2 });

        fs.IsDisjoint(new[] { 3, 4 }).Should().BeTrue();
        fs.IsDisjoint(new[] { 2, 3 }).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_Copy_ReturnsEqualButDistinctInstance()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });
        var copy = fs.Copy();

        copy.Should().NotBeSameAs(fs);
        (copy == fs).Should().BeTrue();
    }

    // ===== ToString/Repr Tests =====

    [Fact]
    public void FrozenSet_ToString_Empty_ReturnsFrozensetParens()
    {
        var fs = new FrozenSet<int>();

        fs.ToString().Should().Be("frozenset()");
    }

    [Fact]
    public void FrozenSet_ToString_NonEmpty_ReturnsFormattedString()
    {
        var fs = new FrozenSet<int>(new[] { 1 });

        // Should be "frozenset({1})"
        fs.ToString().Should().StartWith("frozenset({");
        fs.ToString().Should().EndWith("})");
        fs.ToString().Should().Contain("1");
    }

    [Fact]
    public void FrozenSet_ToString_WithStrings_ContainsElements()
    {
        var fs = new FrozenSet<string>(new[] { "hello" });

        // Should contain the string element (uses ToString(), not Python-style repr)
        fs.ToString().Should().Contain("hello");
        fs.ToString().Should().StartWith("frozenset({");
        fs.ToString().Should().EndWith("})");
    }

    // ===== Set Query Method Tests =====

    [Fact]
    public void FrozenSet_IsProperSubsetOf_ReturnsCorrectResult()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2 });

        fs.IsProperSubsetOf(new[] { 1, 2, 3 }).Should().BeTrue();
        fs.IsProperSubsetOf(new[] { 1, 2 }).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_IsProperSupersetOf_ReturnsCorrectResult()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });

        fs.IsProperSupersetOf(new[] { 1, 2 }).Should().BeTrue();
        fs.IsProperSupersetOf(new[] { 1, 2, 3 }).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_IsSubsetOf_ReturnsCorrectResult()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2 });

        fs.IsSubsetOf(new[] { 1, 2, 3 }).Should().BeTrue();
        fs.IsSubsetOf(new[] { 1, 2 }).Should().BeTrue();
        fs.IsSubsetOf(new[] { 1 }).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_IsSupersetOf_ReturnsCorrectResult()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });

        fs.IsSupersetOf(new[] { 1, 2 }).Should().BeTrue();
        fs.IsSupersetOf(new[] { 1, 2, 3 }).Should().BeTrue();
        fs.IsSupersetOf(new[] { 1, 2, 3, 4 }).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_Overlaps_ReturnsCorrectResult()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });

        fs.Overlaps(new[] { 3, 4, 5 }).Should().BeTrue();
        fs.Overlaps(new[] { 4, 5, 6 }).Should().BeFalse();
    }

    [Fact]
    public void FrozenSet_SetEquals_ReturnsCorrectResult()
    {
        var fs = new FrozenSet<int>(new[] { 1, 2, 3 });

        fs.SetEquals(new[] { 3, 2, 1 }).Should().BeTrue();
        fs.SetEquals(new[] { 1, 2 }).Should().BeFalse();
    }
}
