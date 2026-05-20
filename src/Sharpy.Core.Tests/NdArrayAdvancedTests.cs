using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for advanced NdArray features added in Phase 5: boolean/fancy indexing,
/// Numpy utilities (sort/argsort/unique/searchsorted/allclose/isnan), and the
/// IEnumerable/ISized/structural-equality interfaces.
/// </summary>
public class NdArrayAdvancedTests
{
    // ---- Boolean mask indexing ----------------------------------------------

    [Fact]
    public void GetMasked_OneDim_ReturnsSelectedElements()
    {
        var arr = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }, new[] { 5 });
        var mask = new NdArray<bool>(new[] { true, false, true, false, true }, new[] { 5 });

        var result = arr.GetMasked(mask);

        result.Shape.Should().Equal(new[] { 3 });
        result.ToArray().Should().Equal(new[] { 1.0, 3.0, 5.0 });
    }

    [Fact]
    public void GetMasked_AllFalse_ReturnsEmpty()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });
        var mask = new NdArray<bool>(new[] { false, false, false }, new[] { 3 });

        var result = arr.GetMasked(mask);

        result.Size.Should().Be(0);
        result.Shape.Should().Equal(new[] { 0 });
    }

    [Fact]
    public void GetMasked_TwoDim_ReturnsFlatSelection()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        var mask = new NdArray<bool>(
            new[] { true, false, true, false, true, false },
            new[] { 2, 3 });

        var result = arr.GetMasked(mask);

        result.Shape.Should().Equal(new[] { 3 });
        result.ToArray().Should().Equal(new[] { 1, 3, 5 });
    }

    [Fact]
    public void SetMasked_Scalar_AssignsToTruePositions()
    {
        var arr = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 4 });
        var mask = new NdArray<bool>(new[] { true, false, true, false }, new[] { 4 });

        arr.SetMasked(mask, -1.0);

        arr.ToArray().Should().Equal(new[] { -1.0, 2.0, -1.0, 4.0 });
    }

    [Fact]
    public void SetMasked_FromArray_AssignsCorrespondingValues()
    {
        var arr = new NdArray<int>(new[] { 0, 0, 0, 0, 0 }, new[] { 5 });
        var mask = new NdArray<bool>(new[] { true, false, true, true, false }, new[] { 5 });
        var values = new NdArray<int>(new[] { 7, 8, 9 }, new[] { 3 });

        arr.SetMasked(mask, values);

        arr.ToArray().Should().Equal(new[] { 7, 0, 8, 9, 0 });
    }

    // ---- Fancy (integer-array) indexing -------------------------------------

    [Fact]
    public void Take_OneDim_SelectsIndices()
    {
        var arr = new NdArray<double>(new[] { 10.0, 20.0, 30.0, 40.0, 50.0 }, new[] { 5 });

        var result = arr.Take(new[] { 0, 2, 4 });

        result.Shape.Should().Equal(new[] { 3 });
        result.ToArray().Should().Equal(new[] { 10.0, 30.0, 50.0 });
    }

    [Fact]
    public void Take_OneDim_AllowsNegativeAndRepeatedIndices()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5 }, new[] { 5 });

        var result = arr.Take(new[] { -1, -2, 0, 0 });

        result.ToArray().Should().Equal(new[] { 5, 4, 1, 1 });
    }

    [Fact]
    public void Take_TwoDim_AxisZero_SelectsRows()
    {
        // 3x2 array: [[1, 2], [3, 4], [5, 6]]
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 3, 2 });

        var result = arr.Take(new[] { 0, 2 }, axis: 0);

        result.Shape.Should().Equal(new[] { 2, 2 });
        result.ToArray().Should().Equal(new[] { 1, 2, 5, 6 });
    }

    [Fact]
    public void Take_TwoDim_AxisOne_SelectsColumns()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });

        var result = arr.Take(new[] { 0, 2 }, axis: 1);

        result.Shape.Should().Equal(new[] { 2, 2 });
        result.ToArray().Should().Equal(new[] { 1, 3, 4, 6 });
    }

    [Fact]
    public void Take_OutOfBounds_ThrowsIndexError()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });

        var act = () => arr.Take(new[] { 5 });

        act.Should().Throw<IndexError>();
    }

    [Fact]
    public void Put_OneDim_AssignsToIndices()
    {
        var arr = new NdArray<int>(new[] { 0, 0, 0, 0, 0 }, new[] { 5 });
        var values = new NdArray<int>(new[] { 10, 20 }, new[] { 2 });

        arr.Put(new[] { 0, 4 }, values);

        arr.ToArray().Should().Equal(new[] { 10, 0, 0, 0, 20 });
    }

    // ---- Sorting and utilities ----------------------------------------------

    [Fact]
    public void Sort_ReturnsAscendingCopy()
    {
        var arr = new NdArray<double>(new[] { 3.0, 1.0, 4.0, 1.0, 5.0, 9.0 }, new[] { 6 });

        var result = Numpy.Sort(arr);

        result.ToArray().Should().Equal(new[] { 1.0, 1.0, 3.0, 4.0, 5.0, 9.0 });
        // Verify the original is untouched.
        arr.ToArray().Should().Equal(new[] { 3.0, 1.0, 4.0, 1.0, 5.0, 9.0 });
    }

    [Fact]
    public void Argsort_ReturnsIndicesThatWouldSort()
    {
        var arr = new NdArray<double>(new[] { 30.0, 10.0, 20.0 }, new[] { 3 });

        var indices = Numpy.Argsort(arr);

        indices.ToArray().Should().Equal(new[] { 1L, 2L, 0L });
        // Verify reapplying the indices reproduces a sorted array.
        var idx = indices.ToArray().Select(x => (int)x).ToArray();
        arr.Take(idx).ToArray().Should().Equal(new[] { 10.0, 20.0, 30.0 });
    }

    [Fact]
    public void Unique_ReturnsSortedDistinctElements()
    {
        var arr = new NdArray<double>(new[] { 3.0, 1.0, 2.0, 1.0, 3.0, 2.0 }, new[] { 6 });

        var result = Numpy.Unique(arr);

        result.ToArray().Should().Equal(new[] { 1.0, 2.0, 3.0 });
    }

    [Fact]
    public void Searchsorted_ReturnsLeftInsertionIndices()
    {
        var sorted = new NdArray<double>(new[] { 1.0, 2.0, 4.0, 5.0 }, new[] { 4 });
        var values = new NdArray<double>(new[] { 0.0, 1.0, 3.0, 6.0 }, new[] { 4 });

        var result = Numpy.Searchsorted(sorted, values);

        // 0 -> before 1, 1 -> equal to 1 (left side gives 0), 3 -> between 2 and 4, 6 -> after 5
        result.ToArray().Should().Equal(new[] { 0L, 0L, 2L, 4L });
    }

    [Fact]
    public void Allclose_NearbyValues_ReturnsTrue()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0 }, new[] { 3 });
        var b = new NdArray<double>(new[] { 1.0 + 1e-9, 2.0 - 1e-9, 3.0 }, new[] { 3 });

        Numpy.Allclose(a, b).Should().BeTrue();
    }

    [Fact]
    public void Allclose_DivergentValues_ReturnsFalse()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0 }, new[] { 3 });
        var b = new NdArray<double>(new[] { 1.0, 2.5, 3.0 }, new[] { 3 });

        Numpy.Allclose(a, b).Should().BeFalse();
    }

    [Fact]
    public void Allclose_BothNan_ReturnsTrue()
    {
        // Two arrays of all NaN should be allclose because IsClose treats matched NaN as equal.
        var a = new NdArray<double>(new[] { double.NaN }, new[] { 1 });
        var b = new NdArray<double>(new[] { double.NaN }, new[] { 1 });

        Numpy.Allclose(a, b).Should().BeTrue();
    }

    [Fact]
    public void Allclose_WithBroadcasting_Works()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0 }, new[] { 3 });
        var scalar = new NdArray<double>(new[] { 1.0 }, new[] { 1 });

        // (3,) vs (1,) — broadcasts. Only equal at index 0, so not allclose.
        Numpy.Allclose(a, scalar).Should().BeFalse();
    }

    [Fact]
    public void Isnan_FlagsNaNValues()
    {
        var arr = new NdArray<double>(
            new[] { 1.0, double.NaN, 3.0, double.NaN },
            new[] { 4 });

        var result = Numpy.Isnan(arr);

        result.ToArray().Should().Equal(new[] { false, true, false, true });
    }

    [Fact]
    public void Isinf_FlagsInfinities()
    {
        var arr = new NdArray<double>(
            new[] { 1.0, double.PositiveInfinity, double.NegativeInfinity, 4.0 },
            new[] { 4 });

        var result = Numpy.Isinf(arr);

        result.ToArray().Should().Equal(new[] { false, true, true, false });
    }

    [Fact]
    public void Isfinite_FlagsFiniteValues()
    {
        var arr = new NdArray<double>(
            new[] { 1.0, double.NaN, double.PositiveInfinity, 4.0 },
            new[] { 4 });

        var result = Numpy.Isfinite(arr);

        result.ToArray().Should().Equal(new[] { true, false, false, true });
    }

    // ---- IEnumerable & ISized ----------------------------------------------

    [Fact]
    public void Enumerable_OneDim_YieldsAllElements()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 4 });

        var collected = new List<int>();
        foreach (var v in arr)
        {
            collected.Add(v);
        }

        collected.Should().Equal(new[] { 1, 2, 3, 4 });
    }

    [Fact]
    public void Enumerable_TwoDim_YieldsRowMajor()
    {
        // 2x3: [[1, 2, 3], [4, 5, 6]]
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });

        arr.ToArray().Should().Equal(new[] { 1, 2, 3, 4, 5, 6 });
    }

    [Fact]
    public void Enumerable_Empty_YieldsNothing()
    {
        var arr = new NdArray<int>(new int[0], new[] { 0 });

        arr.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void Count_TwoDim_ReturnsFirstAxisLength()
    {
        var arr = new NdArray<int>(new int[6], new[] { 3, 2 });

        ((ISized)arr).Count.Should().Be(3);
    }

    [Fact]
    public void Count_OneDim_ReturnsLength()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5 }, new[] { 5 });

        ((ISized)arr).Count.Should().Be(5);
    }

    // ---- Structural equality ------------------------------------------------

    [Fact]
    public void Equals_SameShapeAndElements_ReturnsTrue()
    {
        var a = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        var b = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentShape_ReturnsFalse()
    {
        var a = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        var b = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 4 });

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var a = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });
        var b = new NdArray<int>(new[] { 1, 2, 4 }, new[] { 3 });

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Tolist_OneDim_ReturnsList()
    {
        var arr = new NdArray<int>(new[] { 10, 20, 30 }, new[] { 3 });

        var result = arr.Tolist();

        // Fully qualify because `List<T>` would otherwise resolve to Sharpy.List<T>.
        result.Should().BeOfType<System.Collections.Generic.List<int>>();
        ((System.Collections.Generic.List<int>)result).Should().Equal(new[] { 10, 20, 30 });
    }

    [Fact]
    public void Tolist_TwoDim_ReturnsNestedList()
    {
        var arr = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });

        var result = arr.Tolist();

        var outer = result.Should().BeOfType<System.Collections.Generic.List<object>>().Subject;
        outer.Should().HaveCount(2);
        outer[0].Should().BeOfType<System.Collections.Generic.List<double>>();
        ((System.Collections.Generic.List<double>)outer[0]).Should().Equal(new[] { 1.0, 2.0 });
        ((System.Collections.Generic.List<double>)outer[1]).Should().Equal(new[] { 3.0, 4.0 });
    }
}
