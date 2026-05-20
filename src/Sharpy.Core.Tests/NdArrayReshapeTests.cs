using System;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class NdArrayReshapeTests
{
    #region Reshape

    [Fact]
    public void Reshape_1Dto2D_Works()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 6 });
        var reshaped = arr.Reshape(2, 3);

        reshaped.Shape.Should().Equal(new[] { 2, 3 });
        reshaped[0, 0].Should().Be(1);
        reshaped[0, 1].Should().Be(2);
        reshaped[0, 2].Should().Be(3);
        reshaped[1, 0].Should().Be(4);
        reshaped[1, 2].Should().Be(6);
    }

    [Fact]
    public void Reshape_2Dto1D_Works()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        var reshaped = arr.Reshape(4);

        reshaped.Shape.Should().Equal(new[] { 4 });
        reshaped[0].Should().Be(1);
        reshaped[3].Should().Be(4);
    }

    [Fact]
    public void Reshape_WithInferredDim_Works()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 6 });
        var reshaped = arr.Reshape(-1, 3);

        reshaped.Shape.Should().Equal(new[] { 2, 3 });
    }

    [Fact]
    public void Reshape_WithInferredDim_OtherPosition()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 6 });
        var reshaped = arr.Reshape(2, -1);

        reshaped.Shape.Should().Equal(new[] { 2, 3 });
    }

    [Fact]
    public void Reshape_IncompatibleShape_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 4 });
        Action act = () => arr.Reshape(3, 2);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reshape_MultipleInferredDims_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 4 });
        Action act = () => arr.Reshape(-1, -1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reshape_InferredDimNotEvenlyDivisible_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5 }, new[] { 5 });
        Action act = () => arr.Reshape(-1, 2);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reshape_Null_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2 }, new[] { 2 });
        Action act = () => arr.Reshape(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Reshape_Contiguous_ReturnsView()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 4 });
        var reshaped = arr.Reshape(2, 2);

        // Mutating the reshape should affect the original (view semantics).
        reshaped[0, 0] = 99;
        arr[0].Should().Be(99);
    }

    #endregion

    #region Transpose

    [Fact]
    public void Transpose_2D_ReversesDimensions()
    {
        // 2x3:
        // 1 2 3
        // 4 5 6
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        var t = arr.Transpose();

        t.Shape.Should().Equal(new[] { 3, 2 });
        t[0, 0].Should().Be(1);
        t[0, 1].Should().Be(4);
        t[1, 0].Should().Be(2);
        t[1, 1].Should().Be(5);
        t[2, 0].Should().Be(3);
        t[2, 1].Should().Be(6);
    }

    [Fact]
    public void Transpose_ReturnsView()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        var t = arr.Transpose();
        t[0, 1] = 99;
        // Element at (0,1) of transpose corresponds to (1,0) of arr.
        arr[1, 0].Should().Be(99);
    }

    [Fact]
    public void Transpose_3D_ReversesAllAxes()
    {
        var data = new int[24];
        for (int i = 0; i < 24; i++)
        {
            data[i] = i;
        }
        var arr = new NdArray<int>(data, new[] { 2, 3, 4 });
        var t = arr.Transpose();

        t.Shape.Should().Equal(new[] { 4, 3, 2 });
    }

    [Fact]
    public void Transpose_1D_NoChange()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });
        var t = arr.Transpose();
        t.Shape.Should().Equal(new[] { 3 });
        t[0].Should().Be(1);
        t[2].Should().Be(3);
    }

    #endregion

    #region Flatten

    [Fact]
    public void Flatten_2D_Returns1D()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        var flat = arr.Flatten();

        flat.Ndim.Should().Be(1);
        flat.Shape.Should().Equal(new[] { 6 });
        for (int i = 0; i < 6; i++)
        {
            flat[i].Should().Be(i + 1);
        }
    }

    [Fact]
    public void Flatten_ReturnsCopy_NotView()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        var flat = arr.Flatten();
        flat[0] = 99;
        // Modifying the flatten result must not affect the original.
        arr[0, 0].Should().Be(1);
    }

    [Fact]
    public void Flatten_OfTranspose_TraversesInRowMajor()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        var t = arr.Transpose();
        var flat = t.Flatten();

        // Transposed shape (3,2) traversed in row-major:
        // (0,0)=1 (0,1)=4 (1,0)=2 (1,1)=5 (2,0)=3 (2,1)=6
        flat.Shape.Should().Equal(new[] { 6 });
        flat[0].Should().Be(1);
        flat[1].Should().Be(4);
        flat[2].Should().Be(2);
        flat[3].Should().Be(5);
        flat[4].Should().Be(3);
        flat[5].Should().Be(6);
    }

    #endregion

    #region Ravel

    [Fact]
    public void Ravel_Contiguous_ReturnsView()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        var rav = arr.Ravel();

        rav.Ndim.Should().Be(1);
        rav.Shape.Should().Equal(new[] { 4 });
        // View — mutating should propagate
        rav[0] = 99;
        arr[0, 0].Should().Be(99);
    }

    [Fact]
    public void Ravel_NonContiguous_ReturnsCopy()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        var rav = arr.Transpose().Ravel();

        rav.Shape.Should().Equal(new[] { 6 });
        // Non-contiguous; should be a copy. Mutating ravel won't affect arr.
        rav[0] = 99;
        arr[0, 0].Should().Be(1);
    }

    #endregion

    #region Copy

    [Fact]
    public void Copy_SameShape_IndependentBuffer()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        var c = arr.Copy();

        c.Shape.Should().Equal(arr.Shape);
        c[0, 0].Should().Be(1);

        c[0, 0] = 99;
        arr[0, 0].Should().Be(1);
    }

    [Fact]
    public void Copy_OfView_IsContiguousCopy()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        var t = arr.Transpose();
        var c = t.Copy();

        c.Shape.Should().Equal(new[] { 3, 2 });
        c[0, 0].Should().Be(1);
        c[0, 1].Should().Be(4);
        c[2, 1].Should().Be(6);

        // Independent of original
        c[0, 0] = 99;
        arr[0, 0].Should().Be(1);
    }

    #endregion
}
