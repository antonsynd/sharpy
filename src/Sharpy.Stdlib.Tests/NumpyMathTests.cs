using System;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

public class NumpyMathTests
{
    private const double Tol = 1e-12;

    #region Elementwise math vs System.Math

    [Fact]
    public void Sqrt_ArrayMatchesSystemMath()
    {
        var a = Numpy.Array(new[] { 0.0, 1.0, 4.0, 9.0, 16.0 });

        var r = Numpy.Sqrt(a);

        for (int i = 0; i < a.Size; i++)
        {
            r[i].Should().BeApproximately(System.Math.Sqrt(a[i]), Tol);
        }
    }

    [Fact]
    public void Sqrt_Scalar_MatchesSystemMath()
    {
        Numpy.Sqrt(25.0).Should().BeApproximately(5.0, Tol);
        Numpy.Sqrt(2.0).Should().BeApproximately(System.Math.Sqrt(2.0), Tol);
    }

    [Fact]
    public void Exp_ArrayMatchesSystemMath()
    {
        var a = Numpy.Array(new[] { 0.0, 1.0, 2.0, -1.0 });

        var r = Numpy.Exp(a);

        r[0].Should().BeApproximately(1.0, Tol);
        r[1].Should().BeApproximately(System.Math.E, 1e-9);
        for (int i = 0; i < a.Size; i++)
        {
            r[i].Should().BeApproximately(System.Math.Exp(a[i]), Tol);
        }
    }

    [Fact]
    public void Log_ArrayMatchesSystemMath()
    {
        var a = Numpy.Array(new[] { 1.0, System.Math.E, 10.0 });

        var r = Numpy.Log(a);

        r[0].Should().BeApproximately(0.0, Tol);
        r[1].Should().BeApproximately(1.0, Tol);
        r[2].Should().BeApproximately(System.Math.Log(10.0), Tol);
    }

    [Fact]
    public void Log2_ArrayMatchesSystemMath()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 4.0, 8.0 });

        var r = Numpy.Log2(a);

        r[0].Should().BeApproximately(0.0, Tol);
        r[1].Should().BeApproximately(1.0, Tol);
        r[2].Should().BeApproximately(2.0, Tol);
        r[3].Should().BeApproximately(3.0, Tol);
    }

    [Fact]
    public void Log10_ArrayMatchesSystemMath()
    {
        var a = Numpy.Array(new[] { 1.0, 10.0, 100.0, 1000.0 });

        var r = Numpy.Log10(a);

        r[0].Should().BeApproximately(0.0, Tol);
        r[1].Should().BeApproximately(1.0, Tol);
        r[2].Should().BeApproximately(2.0, Tol);
        r[3].Should().BeApproximately(3.0, Tol);
    }

    [Fact]
    public void Abs_ArrayMatchesSystemMath()
    {
        var a = Numpy.Array(new[] { -1.0, 0.0, 2.0, -3.5 });

        var r = Numpy.Abs(a);

        r[0].Should().BeApproximately(1.0, Tol);
        r[1].Should().BeApproximately(0.0, Tol);
        r[2].Should().BeApproximately(2.0, Tol);
        r[3].Should().BeApproximately(3.5, Tol);
    }

    [Fact]
    public void Sin_Cos_Tan_MatchSystemMath()
    {
        var a = Numpy.Array(new[] { 0.0, System.Math.PI / 6, System.Math.PI / 4, System.Math.PI / 2 });

        var s = Numpy.Sin(a);
        var c = Numpy.Cos(a);
        var t = Numpy.Tan(a);

        for (int i = 0; i < a.Size; i++)
        {
            s[i].Should().BeApproximately(System.Math.Sin(a[i]), 1e-12);
            c[i].Should().BeApproximately(System.Math.Cos(a[i]), 1e-12);
        }

        t[0].Should().BeApproximately(0.0, Tol);
        t[2].Should().BeApproximately(1.0, 1e-12);
    }

    [Fact]
    public void Arcsin_Arccos_Arctan_RoundTrip()
    {
        var x = Numpy.Array(new[] { 0.0, 0.5, 0.75, -0.5 });

        var asx = Numpy.Arcsin(x);
        for (int i = 0; i < x.Size; i++)
        {
            asx[i].Should().BeApproximately(System.Math.Asin(x[i]), Tol);
            Numpy.Sin(asx[i]).Should().BeApproximately(x[i], 1e-12);
        }

        var acx = Numpy.Arccos(x);
        for (int i = 0; i < x.Size; i++)
        {
            acx[i].Should().BeApproximately(System.Math.Acos(x[i]), Tol);
        }

        var atx = Numpy.Arctan(x);
        for (int i = 0; i < x.Size; i++)
        {
            atx[i].Should().BeApproximately(System.Math.Atan(x[i]), Tol);
        }
    }

    [Fact]
    public void Floor_Ceil_Round_MatchSystemMath()
    {
        var a = Numpy.Array(new[] { 1.2, 1.5, 1.7, -1.2, -1.5, -1.7 });

        var fl = Numpy.Floor(a);
        var ce = Numpy.Ceil(a);
        var rd = Numpy.Round(a);

        for (int i = 0; i < a.Size; i++)
        {
            fl[i].Should().BeApproximately(System.Math.Floor(a[i]), Tol);
            ce[i].Should().BeApproximately(System.Math.Ceiling(a[i]), Tol);
            rd[i].Should().BeApproximately(System.Math.Round(a[i]), Tol);
        }
    }

    [Fact]
    public void Round_WithDecimals_HonorsParameter()
    {
        var a = Numpy.Array(new[] { 1.2345, 2.5555, -3.1415 });

        var r = Numpy.Round(a, 2);

        r[0].Should().BeApproximately(1.23, Tol);
        r[1].Should().BeApproximately(2.56, Tol);
        r[2].Should().BeApproximately(-3.14, Tol);
    }

    #endregion

    #region Power

    [Fact]
    public void Power_ArrayArray_BroadcastsAndComputes()
    {
        var a = Numpy.Array(new[] { 2.0, 3.0, 4.0 });
        var b = Numpy.Array(new[] { 2.0, 2.0, 0.5 });

        var r = Numpy.Power(a, b);

        r[0].Should().BeApproximately(4.0, Tol);
        r[1].Should().BeApproximately(9.0, Tol);
        r[2].Should().BeApproximately(2.0, Tol);
    }

    [Fact]
    public void Power_ArrayScalar_RaisesByConstant()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0 });

        var r = Numpy.Power(a, 2.0);

        r[0].Should().BeApproximately(1.0, Tol);
        r[1].Should().BeApproximately(4.0, Tol);
        r[2].Should().BeApproximately(9.0, Tol);
        r[3].Should().BeApproximately(16.0, Tol);
    }

    [Fact]
    public void Power_ScalarArray_RaisesScalarToExponents()
    {
        var b = Numpy.Array(new[] { 0.0, 1.0, 2.0, 3.0 });

        var r = Numpy.Power(2.0, b);

        r[0].Should().BeApproximately(1.0, Tol);
        r[1].Should().BeApproximately(2.0, Tol);
        r[2].Should().BeApproximately(4.0, Tol);
        r[3].Should().BeApproximately(8.0, Tol);
    }

    [Fact]
    public void Power_BroadcastsRowAndColumn()
    {
        // (1,3) ** (2,1) -> (2,3)
        var a = new NdArray<double>(new[] { 2.0, 3.0, 4.0 }, new[] { 1, 3 });
        var b = new NdArray<double>(new[] { 1.0, 2.0 }, new[] { 2, 1 });

        var r = Numpy.Power(a, b);

        r.Shape.Should().Equal(new[] { 2, 3 });
        r[0, 0].Should().BeApproximately(2.0, Tol);
        r[0, 1].Should().BeApproximately(3.0, Tol);
        r[1, 0].Should().BeApproximately(4.0, Tol);
        r[1, 1].Should().BeApproximately(9.0, Tol);
        r[1, 2].Should().BeApproximately(16.0, Tol);
    }

    #endregion

    #region Reductions (full)

    [Fact]
    public void Sum_FullReduction_AddsAllElements()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });

        Numpy.Sum(a).Should().BeApproximately(15.0, Tol);
    }

    [Fact]
    public void Sum_EmptyArray_IsZero()
    {
        var a = Numpy.Zeros(0);

        Numpy.Sum(a).Should().BeApproximately(0.0, Tol);
    }

    [Fact]
    public void Min_FullReduction_FindsMinimum()
    {
        var a = Numpy.Array(new[] { 3.0, 1.0, 4.0, 1.0, 5.0, 9.0, 2.0, 6.0 });

        Numpy.Min(a).Should().BeApproximately(1.0, Tol);
    }

    [Fact]
    public void Max_FullReduction_FindsMaximum()
    {
        var a = Numpy.Array(new[] { 3.0, 1.0, 4.0, 1.0, 5.0, 9.0, 2.0, 6.0 });

        Numpy.Max(a).Should().BeApproximately(9.0, Tol);
    }

    [Fact]
    public void Mean_FullReduction_AveragesAllElements()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });

        Numpy.Mean(a).Should().BeApproximately(3.0, Tol);
    }

    [Fact]
    public void Var_FullReduction_PopulationVariance()
    {
        // [1,2,3,4,5]: mean=3, variance = ((-2)^2+(-1)^2+0+1+4)/5 = 10/5 = 2
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });

        Numpy.Var(a).Should().BeApproximately(2.0, Tol);
    }

    [Fact]
    public void Std_FullReduction_SqrtOfVariance()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });

        Numpy.Std(a).Should().BeApproximately(System.Math.Sqrt(2.0), Tol);
    }

    [Fact]
    public void Median_OddCount_ReturnsMiddleElement()
    {
        var a = Numpy.Array(new[] { 5.0, 1.0, 3.0, 2.0, 4.0 });

        Numpy.Median(a).Should().BeApproximately(3.0, Tol);
    }

    [Fact]
    public void Median_EvenCount_AveragesTwoMiddleElements()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0 });

        Numpy.Median(a).Should().BeApproximately(2.5, Tol);
    }

    [Fact]
    public void Min_EmptyArray_Throws()
    {
        var a = Numpy.Zeros(0);

        Action act = () => Numpy.Min(a);

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Reductions (along axis)

    [Fact]
    public void Sum_AlongAxis0_CollapsesRows()
    {
        // [[1,2,3],[4,5,6]] sum along axis 0 -> [5,7,9]
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, new[] { 2, 3 });

        var r = Numpy.Sum(a, 0);

        r.Shape.Should().Equal(new[] { 3 });
        r[0].Should().BeApproximately(5.0, Tol);
        r[1].Should().BeApproximately(7.0, Tol);
        r[2].Should().BeApproximately(9.0, Tol);
    }

    [Fact]
    public void Sum_AlongAxis1_CollapsesColumns()
    {
        // [[1,2,3],[4,5,6]] sum along axis 1 -> [6,15]
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, new[] { 2, 3 });

        var r = Numpy.Sum(a, 1);

        r.Shape.Should().Equal(new[] { 2 });
        r[0].Should().BeApproximately(6.0, Tol);
        r[1].Should().BeApproximately(15.0, Tol);
    }

    [Fact]
    public void Mean_AlongAxis_ComputesPerRowOrColumn()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, new[] { 2, 3 });

        var r0 = Numpy.Mean(a, 0);
        var r1 = Numpy.Mean(a, 1);

        r0.Shape.Should().Equal(new[] { 3 });
        r0[0].Should().BeApproximately(2.5, Tol);
        r0[1].Should().BeApproximately(3.5, Tol);
        r0[2].Should().BeApproximately(4.5, Tol);

        r1.Shape.Should().Equal(new[] { 2 });
        r1[0].Should().BeApproximately(2.0, Tol);
        r1[1].Should().BeApproximately(5.0, Tol);
    }

    [Fact]
    public void Max_AlongAxis_FindsPerSliceMaximum()
    {
        var a = new NdArray<double>(new[] { 1.0, 9.0, 3.0, 4.0, 2.0, 6.0 }, new[] { 2, 3 });

        var r = Numpy.Max(a, 1);

        r[0].Should().BeApproximately(9.0, Tol);
        r[1].Should().BeApproximately(6.0, Tol);
    }

    [Fact]
    public void Median_AlongAxis_PerRow()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 5.0, 4.0, 6.0 }, new[] { 2, 3 });

        var r = Numpy.Median(a, 1);

        r[0].Should().BeApproximately(2.0, Tol);
        r[1].Should().BeApproximately(5.0, Tol);
    }

    [Fact]
    public void Sum_AlongAxis_NegativeAxis_WrapsToTrailing()
    {
        // axis=-1 for a (2,3) array means axis 1.
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, new[] { 2, 3 });

        var r = Numpy.Sum(a, -1);

        r.Shape.Should().Equal(new[] { 2 });
        r[0].Should().BeApproximately(6.0, Tol);
        r[1].Should().BeApproximately(15.0, Tol);
    }

    [Fact]
    public void Sum_AlongAxis_OutOfRange_Throws()
    {
        var a = new NdArray<double>(new double[6], new[] { 2, 3 });

        Action act = () => Numpy.Sum(a, 5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Comparisons

    [Fact]
    public void Equal_SameShape_ReturnsBoolArray()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var b = Numpy.Array(new[] { 1.0, 0.0, 3.0 });

        var r = Numpy.Equal(a, b);

        r.Shape.Should().Equal(new[] { 3 });
        r[0].Should().BeTrue();
        r[1].Should().BeFalse();
        r[2].Should().BeTrue();
    }

    [Fact]
    public void NotEqual_SameShape()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var b = Numpy.Array(new[] { 1.0, 0.0, 3.0 });

        var r = Numpy.NotEqual(a, b);

        r[0].Should().BeFalse();
        r[1].Should().BeTrue();
        r[2].Should().BeFalse();
    }

    [Fact]
    public void Less_LessEqual()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var b = Numpy.Array(new[] { 2.0, 2.0, 2.0 });

        var lt = Numpy.Less(a, b);
        var le = Numpy.LessEqual(a, b);

        lt[0].Should().BeTrue();
        lt[1].Should().BeFalse();
        lt[2].Should().BeFalse();

        le[0].Should().BeTrue();
        le[1].Should().BeTrue();
        le[2].Should().BeFalse();
    }

    [Fact]
    public void Greater_GreaterEqual()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var b = Numpy.Array(new[] { 2.0, 2.0, 2.0 });

        var gt = Numpy.Greater(a, b);
        var ge = Numpy.GreaterEqual(a, b);

        gt[0].Should().BeFalse();
        gt[1].Should().BeFalse();
        gt[2].Should().BeTrue();

        ge[0].Should().BeFalse();
        ge[1].Should().BeTrue();
        ge[2].Should().BeTrue();
    }

    [Fact]
    public void Equal_BroadcastsScalar()
    {
        // Compare each element against a 1-element array.
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0, 2.0 });
        var b = new NdArray<double>(new[] { 2.0 }, new[] { 1 });

        var r = Numpy.Equal(a, b);

        r[0].Should().BeFalse();
        r[1].Should().BeTrue();
        r[2].Should().BeFalse();
        r[3].Should().BeTrue();
    }

    #endregion
}
