using System;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class NdArraySlicingTests
{
    #region SliceSpec

    [Fact]
    public void SliceSpec_DefaultIsAll()
    {
        var s = SliceSpec.All;
        s.Start.Should().BeNull();
        s.Stop.Should().BeNull();
        s.Step.Should().BeNull();
    }

    [Fact]
    public void SliceSpec_ZeroStep_Throws()
    {
        Action act = () => new SliceSpec(0, 5, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SliceSpec_Equality()
    {
        var a = new SliceSpec(0, 5, 2);
        var b = new SliceSpec(0, 5, 2);
        var c = new SliceSpec(0, 5, 3);

        (a == b).Should().BeTrue();
        (a != c).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
    }

    #endregion

    #region Basic slicing

    [Fact]
    public void Slice_1D_Basic_ReturnsView()
    {
        var arr = new NdArray<int>(new[] { 0, 1, 2, 3, 4 }, new[] { 5 });
        var view = arr.Slice(new SliceSpec(1, 4));

        view.Shape.Should().Equal(new[] { 3 });
        view[0].Should().Be(1);
        view[1].Should().Be(2);
        view[2].Should().Be(3);
    }

    [Fact]
    public void Slice_1D_ViewSharesBuffer_ModifyingViewModifiesOriginal()
    {
        var arr = new NdArray<int>(new[] { 0, 1, 2, 3, 4 }, new[] { 5 });
        var view = arr.Slice(new SliceSpec(1, 4));

        view[0] = 99;
        arr[1].Should().Be(99);
    }

    [Fact]
    public void Slice_1D_NullStartStop_FullRange()
    {
        var arr = new NdArray<int>(new[] { 0, 1, 2, 3, 4 }, new[] { 5 });
        var view = arr.Slice(SliceSpec.All);

        view.Shape.Should().Equal(new[] { 5 });
        view[0].Should().Be(0);
        view[4].Should().Be(4);
    }

    [Fact]
    public void Slice_1D_Step_TakesEveryNth()
    {
        var arr = new NdArray<int>(new[] { 0, 1, 2, 3, 4, 5 }, new[] { 6 });
        var view = arr.Slice(new SliceSpec(0, 6, 2));

        view.Shape.Should().Equal(new[] { 3 });
        view[0].Should().Be(0);
        view[1].Should().Be(2);
        view[2].Should().Be(4);
    }

    [Fact]
    public void Slice_1D_NegativeStep_Reverses()
    {
        var arr = new NdArray<int>(new[] { 0, 1, 2, 3, 4 }, new[] { 5 });
        var view = arr.Slice(new SliceSpec(null, null, -1));

        view.Shape.Should().Equal(new[] { 5 });
        view[0].Should().Be(4);
        view[1].Should().Be(3);
        view[4].Should().Be(0);
    }

    [Fact]
    public void Slice_WrongSliceCount_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        Action act = () => arr.Slice(SliceSpec.All);
        act.Should().Throw<IndexError>();
    }

    [Fact]
    public void Slice_Null_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });
        Action act = () => arr.Slice(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region 2-D slicing

    [Fact]
    public void Slice_2D_BothAxes_ReturnsSubMatrix()
    {
        // 3x3:
        // 0 1 2
        // 3 4 5
        // 6 7 8
        var arr = new NdArray<int>(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new[] { 3, 3 });

        var view = arr.Slice(new SliceSpec(0, 2), new SliceSpec(1, 3));

        view.Shape.Should().Equal(new[] { 2, 2 });
        view[0, 0].Should().Be(1);
        view[0, 1].Should().Be(2);
        view[1, 0].Should().Be(4);
        view[1, 1].Should().Be(5);
    }

    #endregion

    #region GetRow / GetColumn

    [Fact]
    public void GetRow_ReturnsView()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        var row = arr.GetRow(1);

        row.Ndim.Should().Be(1);
        row.Shape.Should().Equal(new[] { 3 });
        row[0].Should().Be(4);
        row[1].Should().Be(5);
        row[2].Should().Be(6);
    }

    [Fact]
    public void GetRow_Negative_CountsFromEnd()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        var row = arr.GetRow(-1);
        row[0].Should().Be(4);
        row[2].Should().Be(6);
    }

    [Fact]
    public void GetRow_ModifiesOriginal()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        var row = arr.GetRow(0);
        row[0] = 99;
        arr[0, 0].Should().Be(99);
    }

    [Fact]
    public void GetRow_NotTwoDimensional_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });
        Action act = () => arr.GetRow(0);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetRow_OutOfRange_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        Action act = () => arr.GetRow(5);
        act.Should().Throw<IndexError>();
    }

    [Fact]
    public void GetColumn_ReturnsView()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        var col = arr.GetColumn(1);

        col.Ndim.Should().Be(1);
        col.Shape.Should().Equal(new[] { 2 });
        col[0].Should().Be(2);
        col[1].Should().Be(5);
    }

    [Fact]
    public void GetColumn_Negative_CountsFromEnd()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        var col = arr.GetColumn(-1);
        col[0].Should().Be(3);
        col[1].Should().Be(6);
    }

    [Fact]
    public void GetColumn_ModifiesOriginal()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        var col = arr.GetColumn(2);
        col[1] = 99;
        arr[1, 2].Should().Be(99);
    }

    [Fact]
    public void GetColumn_NotTwoDimensional_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });
        Action act = () => arr.GetColumn(0);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetColumn_OutOfRange_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        Action act = () => arr.GetColumn(5);
        act.Should().Throw<IndexError>();
    }

    #endregion

    #region View strides

    [Fact]
    public void Slice_View_HasUpdatedStrides()
    {
        var arr = new NdArray<int>(new[] { 0, 1, 2, 3, 4, 5 }, new[] { 6 });
        var view = arr.Slice(new SliceSpec(0, 6, 2));
        // Step doubles original stride
        view.Strides.Should().Equal(new[] { 2 });
    }

    #endregion
}
