using System;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

public class NumpyManipulationTests
{
    private const double Tol = 1e-12;

    #region Concatenate

    [Fact]
    public void Concatenate_OneDimensional_JoinsEndToEnd()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var b = Numpy.Array(new[] { 4.0, 5.0 });

        var r = Numpy.Concatenate(new[] { a, b });

        r.Shape.Should().Equal(new[] { 5 });
        r[0].Should().BeApproximately(1.0, Tol);
        r[2].Should().BeApproximately(3.0, Tol);
        r[3].Should().BeApproximately(4.0, Tol);
        r[4].Should().BeApproximately(5.0, Tol);
    }

    [Fact]
    public void Concatenate_TwoDimensional_AlongAxis0_StacksRows()
    {
        // (2,3) + (1,3) along axis 0 -> (3,3)
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, new[] { 2, 3 });
        var b = new NdArray<double>(new[] { 7.0, 8.0, 9.0 }, new[] { 1, 3 });

        var r = Numpy.Concatenate(new[] { a, b }, 0);

        r.Shape.Should().Equal(new[] { 3, 3 });
        r[2, 0].Should().BeApproximately(7.0, Tol);
        r[2, 2].Should().BeApproximately(9.0, Tol);
    }

    [Fact]
    public void Concatenate_TwoDimensional_AlongAxis1_StacksColumns()
    {
        // (2,2) + (2,1) along axis 1 -> (2,3)
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });
        var b = new NdArray<double>(new[] { 5.0, 6.0 }, new[] { 2, 1 });

        var r = Numpy.Concatenate(new[] { a, b }, 1);

        r.Shape.Should().Equal(new[] { 2, 3 });
        r[0, 2].Should().BeApproximately(5.0, Tol);
        r[1, 2].Should().BeApproximately(6.0, Tol);
        r[0, 0].Should().BeApproximately(1.0, Tol);
    }

    [Fact]
    public void Concatenate_EmptyArray_Throws()
    {
        Action act = () => Numpy.Concatenate(System.Array.Empty<NdArray<double>>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Concatenate_MismatchedNonAxisDim_Throws()
    {
        var a = new NdArray<double>(new double[6], new[] { 2, 3 });
        var b = new NdArray<double>(new double[4], new[] { 2, 2 });

        Action act = () => Numpy.Concatenate(new[] { a, b }, 0);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Stack / Hstack / Vstack

    [Fact]
    public void Stack_OneDArrays_AddsNewLeadingAxis()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var b = Numpy.Array(new[] { 4.0, 5.0, 6.0 });

        var r = Numpy.Stack(new[] { a, b });

        r.Shape.Should().Equal(new[] { 2, 3 });
        r[0, 0].Should().BeApproximately(1.0, Tol);
        r[0, 2].Should().BeApproximately(3.0, Tol);
        r[1, 0].Should().BeApproximately(4.0, Tol);
        r[1, 2].Should().BeApproximately(6.0, Tol);
    }

    [Fact]
    public void Stack_OneDArrays_Axis1_AddsTrailingAxis()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var b = Numpy.Array(new[] { 4.0, 5.0, 6.0 });

        var r = Numpy.Stack(new[] { a, b }, 1);

        r.Shape.Should().Equal(new[] { 3, 2 });
        r[0, 0].Should().BeApproximately(1.0, Tol);
        r[0, 1].Should().BeApproximately(4.0, Tol);
        r[2, 1].Should().BeApproximately(6.0, Tol);
    }

    [Fact]
    public void Hstack_OneDArrays_ConcatenatesAlongAxis0()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0 });
        var b = Numpy.Array(new[] { 3.0, 4.0, 5.0 });

        var r = Numpy.Hstack(new[] { a, b });

        r.Shape.Should().Equal(new[] { 5 });
        r[0].Should().BeApproximately(1.0, Tol);
        r[4].Should().BeApproximately(5.0, Tol);
    }

    [Fact]
    public void Hstack_TwoDArrays_ConcatenatesAlongAxis1()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });
        var b = new NdArray<double>(new[] { 5.0, 6.0 }, new[] { 2, 1 });

        var r = Numpy.Hstack(new[] { a, b });

        r.Shape.Should().Equal(new[] { 2, 3 });
        r[0, 2].Should().BeApproximately(5.0, Tol);
        r[1, 2].Should().BeApproximately(6.0, Tol);
    }

    [Fact]
    public void Vstack_OneDArrays_Promotes_To_RowVectors()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var b = Numpy.Array(new[] { 4.0, 5.0, 6.0 });

        var r = Numpy.Vstack(new[] { a, b });

        r.Shape.Should().Equal(new[] { 2, 3 });
        r[0, 0].Should().BeApproximately(1.0, Tol);
        r[1, 2].Should().BeApproximately(6.0, Tol);
    }

    [Fact]
    public void Vstack_TwoDArrays_ConcatenatesAlongAxis0()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });
        var b = new NdArray<double>(new[] { 5.0, 6.0 }, new[] { 1, 2 });

        var r = Numpy.Vstack(new[] { a, b });

        r.Shape.Should().Equal(new[] { 3, 2 });
        r[2, 0].Should().BeApproximately(5.0, Tol);
        r[2, 1].Should().BeApproximately(6.0, Tol);
    }

    #endregion

    #region Split

    [Fact]
    public void Split_OneDimensional_AtSingleIndex()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });

        var parts = Numpy.Split(a, new[] { 2 });

        parts.Length.Should().Be(2);
        parts[0].Shape.Should().Equal(new[] { 2 });
        parts[0][0].Should().BeApproximately(1.0, Tol);
        parts[0][1].Should().BeApproximately(2.0, Tol);
        parts[1].Shape.Should().Equal(new[] { 3 });
        parts[1][0].Should().BeApproximately(3.0, Tol);
        parts[1][2].Should().BeApproximately(5.0, Tol);
    }

    [Fact]
    public void Split_OneDimensional_AtMultipleIndices()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 });

        var parts = Numpy.Split(a, new[] { 2, 4 });

        parts.Length.Should().Be(3);
        parts[0].Shape.Should().Equal(new[] { 2 });
        parts[1].Shape.Should().Equal(new[] { 2 });
        parts[2].Shape.Should().Equal(new[] { 2 });
        parts[0][0].Should().BeApproximately(1.0, Tol);
        parts[1][0].Should().BeApproximately(3.0, Tol);
        parts[2][1].Should().BeApproximately(6.0, Tol);
    }

    [Fact]
    public void Split_TwoDimensional_AlongAxis1()
    {
        // (2,4) split at index 2 along axis 1 -> two (2,2) arrays
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0 }, new[] { 2, 4 });

        var parts = Numpy.Split(a, new[] { 2 }, 1);

        parts.Length.Should().Be(2);
        parts[0].Shape.Should().Equal(new[] { 2, 2 });
        parts[1].Shape.Should().Equal(new[] { 2, 2 });
        parts[0][0, 0].Should().BeApproximately(1.0, Tol);
        parts[0][1, 1].Should().BeApproximately(6.0, Tol);
        parts[1][0, 0].Should().BeApproximately(3.0, Tol);
        parts[1][1, 1].Should().BeApproximately(8.0, Tol);
    }

    [Fact]
    public void Split_EmptyIndices_ReturnsWholeArray()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });

        var parts = Numpy.Split(a, System.Array.Empty<int>());

        parts.Length.Should().Be(1);
        parts[0].Shape.Should().Equal(new[] { 3 });
    }

    #endregion

    #region Where

    [Fact]
    public void Where_SelectsBetweenTwoArrays()
    {
        var cond = new NdArray<bool>(new[] { true, false, true, false }, new[] { 4 });
        var x = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0 });
        var y = Numpy.Array(new[] { 10.0, 20.0, 30.0, 40.0 });

        var r = Numpy.Where(cond, x, y);

        r[0].Should().BeApproximately(1.0, Tol);
        r[1].Should().BeApproximately(20.0, Tol);
        r[2].Should().BeApproximately(3.0, Tol);
        r[3].Should().BeApproximately(40.0, Tol);
    }

    [Fact]
    public void Where_BroadcastsScalarBranches()
    {
        // condition (3,) with scalar-shaped x (1,) and y (1,).
        var cond = new NdArray<bool>(new[] { true, false, true }, new[] { 3 });
        var x = new NdArray<double>(new[] { 100.0 }, new[] { 1 });
        var y = new NdArray<double>(new[] { 0.0 }, new[] { 1 });

        var r = Numpy.Where(cond, x, y);

        r.Shape.Should().Equal(new[] { 3 });
        r[0].Should().BeApproximately(100.0, Tol);
        r[1].Should().BeApproximately(0.0, Tol);
        r[2].Should().BeApproximately(100.0, Tol);
    }

    [Fact]
    public void Where_DerivedFromComparison()
    {
        // r[i] = a[i] if a[i] > 0 else 0
        var a = Numpy.Array(new[] { -1.0, 2.0, -3.0, 4.0 });
        var zero = new NdArray<double>(new[] { 0.0 }, new[] { 1 });

        var pos = Numpy.Greater(a, zero);
        var r = Numpy.Where(pos, a, zero);

        r[0].Should().BeApproximately(0.0, Tol);
        r[1].Should().BeApproximately(2.0, Tol);
        r[2].Should().BeApproximately(0.0, Tol);
        r[3].Should().BeApproximately(4.0, Tol);
    }

    #endregion

    #region Clip

    [Fact]
    public void Clip_ClampsBetweenMinAndMax()
    {
        var a = Numpy.Array(new[] { -2.0, -1.0, 0.0, 1.0, 2.0, 3.0 });

        var r = Numpy.Clip(a, 0.0, 2.0);

        r[0].Should().BeApproximately(0.0, Tol);
        r[1].Should().BeApproximately(0.0, Tol);
        r[2].Should().BeApproximately(0.0, Tol);
        r[3].Should().BeApproximately(1.0, Tol);
        r[4].Should().BeApproximately(2.0, Tol);
        r[5].Should().BeApproximately(2.0, Tol);
    }

    [Fact]
    public void Clip_AllAboveMax_AllPinnedToMax()
    {
        var a = Numpy.Array(new[] { 5.0, 6.0, 7.0 });

        var r = Numpy.Clip(a, 0.0, 1.0);

        r[0].Should().BeApproximately(1.0, Tol);
        r[1].Should().BeApproximately(1.0, Tol);
        r[2].Should().BeApproximately(1.0, Tol);
    }

    [Fact]
    public void Clip_PreservesShape()
    {
        var a = new NdArray<double>(new[] { -5.0, 0.0, 5.0, 10.0 }, new[] { 2, 2 });

        var r = Numpy.Clip(a, 0.0, 5.0);

        r.Shape.Should().Equal(new[] { 2, 2 });
        r[0, 0].Should().BeApproximately(0.0, Tol);
        r[1, 1].Should().BeApproximately(5.0, Tol);
    }

    [Fact]
    public void Clip_MinGreaterThanMax_Throws()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0 });

        Action act = () => Numpy.Clip(a, 5.0, 2.0);

        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
