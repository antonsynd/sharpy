using System;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

public class NdArrayOperatorTests
{
    private const double Tol = 1e-12;

    #region Same-shape arithmetic

    [Fact]
    public void Add_SameShape1D_AddsElementwise()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var b = Numpy.Array(new[] { 4.0, 5.0, 6.0 });

        var r = a + b;

        r.Shape.Should().Equal(new[] { 3 });
        r[0].Should().BeApproximately(5.0, Tol);
        r[1].Should().BeApproximately(7.0, Tol);
        r[2].Should().BeApproximately(9.0, Tol);
    }

    [Fact]
    public void Subtract_SameShape2D_SubtractsElementwise()
    {
        var a = new NdArray<double>(new[] { 10.0, 20.0, 30.0, 40.0 }, new[] { 2, 2 });
        var b = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });

        var r = a - b;

        r.Shape.Should().Equal(new[] { 2, 2 });
        r[0, 0].Should().BeApproximately(9.0, Tol);
        r[0, 1].Should().BeApproximately(18.0, Tol);
        r[1, 0].Should().BeApproximately(27.0, Tol);
        r[1, 1].Should().BeApproximately(36.0, Tol);
    }

    [Fact]
    public void Multiply_SameShape_MultipliesElementwise()
    {
        var a = Numpy.Array(new[] { 2.0, 3.0, 4.0 });
        var b = Numpy.Array(new[] { 5.0, 6.0, 7.0 });

        var r = a * b;

        r[0].Should().BeApproximately(10.0, Tol);
        r[1].Should().BeApproximately(18.0, Tol);
        r[2].Should().BeApproximately(28.0, Tol);
    }

    [Fact]
    public void Divide_SameShape_DividesElementwise()
    {
        var a = Numpy.Array(new[] { 10.0, 20.0, 30.0 });
        var b = Numpy.Array(new[] { 2.0, 4.0, 5.0 });

        var r = a / b;

        r[0].Should().BeApproximately(5.0, Tol);
        r[1].Should().BeApproximately(5.0, Tol);
        r[2].Should().BeApproximately(6.0, Tol);
    }

    [Fact]
    public void Add_IntArrays_AddsElementwise()
    {
        var a = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });
        var b = new NdArray<int>(new[] { 10, 20, 30 }, new[] { 3 });

        var r = a + b;

        r[0].Should().Be(11);
        r[1].Should().Be(22);
        r[2].Should().Be(33);
    }

    #endregion

    #region Scalar broadcast

    [Fact]
    public void Add_ScalarRight_BroadcastsAcrossArray()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });

        var r = a + 10.0;

        r[0].Should().BeApproximately(11.0, Tol);
        r[1].Should().BeApproximately(12.0, Tol);
        r[2].Should().BeApproximately(13.0, Tol);
    }

    [Fact]
    public void Add_ScalarLeft_BroadcastsCommutatively()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });

        var r = 10.0 + a;

        r[0].Should().BeApproximately(11.0, Tol);
        r[2].Should().BeApproximately(13.0, Tol);
    }

    [Fact]
    public void Multiply_ScalarRight_ScalesArray()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0 });

        var r = a * 2.0;

        r[0].Should().BeApproximately(2.0, Tol);
        r[1].Should().BeApproximately(4.0, Tol);
        r[2].Should().BeApproximately(6.0, Tol);
        r[3].Should().BeApproximately(8.0, Tol);
    }

    [Fact]
    public void Subtract_ScalarLeft_ReversesOrder()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });

        var r = 10.0 - a;

        r[0].Should().BeApproximately(9.0, Tol);
        r[1].Should().BeApproximately(8.0, Tol);
        r[2].Should().BeApproximately(7.0, Tol);
    }

    [Fact]
    public void Divide_ScalarLeft_ReversesOrder()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 4.0 });

        var r = 8.0 / a;

        r[0].Should().BeApproximately(8.0, Tol);
        r[1].Should().BeApproximately(4.0, Tol);
        r[2].Should().BeApproximately(2.0, Tol);
    }

    #endregion

    #region Unary

    [Fact]
    public void Negate_FlipsSignsElementwise()
    {
        var a = Numpy.Array(new[] { 1.0, -2.0, 3.0 });

        var r = -a;

        r[0].Should().BeApproximately(-1.0, Tol);
        r[1].Should().BeApproximately(2.0, Tol);
        r[2].Should().BeApproximately(-3.0, Tol);
    }

    [Fact]
    public void Negate_PreservesShape()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, new[] { 2, 3 });

        var r = -a;

        r.Shape.Should().Equal(new[] { 2, 3 });
        r[1, 2].Should().BeApproximately(-6.0, Tol);
    }

    [Fact]
    public void Negate_NullArg_Throws()
    {
        Action act = () => { var _ = -(NdArray<double>)null!; };
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Broadcasting

    [Fact]
    public void Add_Broadcast_RowVectorAndColumnVector()
    {
        // (3,1) + (1,4) -> (3,4) where r[i,j] = a[i,0] + b[0,j].
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0 }, new[] { 3, 1 });
        var b = new NdArray<double>(new[] { 10.0, 20.0, 30.0, 40.0 }, new[] { 1, 4 });

        var r = a + b;

        r.Shape.Should().Equal(new[] { 3, 4 });
        r[0, 0].Should().BeApproximately(11.0, Tol);
        r[0, 3].Should().BeApproximately(41.0, Tol);
        r[2, 0].Should().BeApproximately(13.0, Tol);
        r[2, 3].Should().BeApproximately(43.0, Tol);
    }

    [Fact]
    public void Add_Broadcast_1DToMatrix_AlignsTrailingAxis()
    {
        // (5,) + (3,5) -> (3,5) where each row of b has the 1-D a added to it.
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });
        var b = new NdArray<double>(
            new[] { 10.0, 20.0, 30.0, 40.0, 50.0, 100.0, 200.0, 300.0, 400.0, 500.0, 1000.0, 2000.0, 3000.0, 4000.0, 5000.0 },
            new[] { 3, 5 });

        var r = a + b;

        r.Shape.Should().Equal(new[] { 3, 5 });
        r[0, 0].Should().BeApproximately(11.0, Tol);
        r[1, 2].Should().BeApproximately(303.0, Tol);
        r[2, 4].Should().BeApproximately(5005.0, Tol);
    }

    [Fact]
    public void Multiply_Broadcast_ScalarLikeShape()
    {
        // (1,) * (4,) -> (4,)
        var a = new NdArray<double>(new[] { 7.0 }, new[] { 1 });
        var b = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0 });

        var r = a * b;

        r.Shape.Should().Equal(new[] { 4 });
        r[0].Should().BeApproximately(7.0, Tol);
        r[3].Should().BeApproximately(28.0, Tol);
    }

    [Fact]
    public void Add_IncompatibleShapes_Throws()
    {
        // (3,) and (4,) are not broadcastable.
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var b = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0 });

        Action act = () => { var _ = a + b; };

        act.Should().Throw<ArgumentException>().WithMessage("*not broadcastable*");
    }

    [Fact]
    public void Add_IncompatibleMatrices_Throws()
    {
        // (2,3) and (4,3) — incompatible on axis 0.
        var a = new NdArray<double>(new double[6], new[] { 2, 3 });
        var b = new NdArray<double>(new double[12], new[] { 4, 3 });

        Action act = () => { var _ = a + b; };

        act.Should().Throw<ArgumentException>().WithMessage("*not broadcastable*");
    }

    [Fact]
    public void Add_NullArgs_Throws()
    {
        var a = Numpy.Array(new[] { 1.0 });
        Action actA = () => { var _ = (NdArray<double>)null! + a; };
        Action actB = () => { var _ = a + (NdArray<double>)null!; };

        actA.Should().Throw<ArgumentNullException>();
        actB.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
