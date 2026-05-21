using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

public class NumpyLinalgTests
{
    private const double Tol = 1e-9;

    #region Dot / Matmul

    [Fact]
    public void Dot_VectorVector_ComputesInnerProduct()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var b = Numpy.Array(new[] { 4.0, 5.0, 6.0 });

        var result = NumpyLinalg.Dot(a, b);

        result.Ndim.Should().Be(0);
        result.Size.Should().Be(1);
        ScalarOf(result).Should().BeApproximately(32.0, Tol);
    }

    [Fact]
    public void Dot_MatrixMatrix_ComputesProduct()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });
        var b = new NdArray<double>(new[] { 5.0, 6.0, 7.0, 8.0 }, new[] { 2, 2 });

        var result = NumpyLinalg.Dot(a, b);

        result.Shape.Should().Equal(new[] { 2, 2 });
        result[0, 0].Should().BeApproximately(19.0, Tol);
        result[0, 1].Should().BeApproximately(22.0, Tol);
        result[1, 0].Should().BeApproximately(43.0, Tol);
        result[1, 1].Should().BeApproximately(50.0, Tol);
    }

    [Fact]
    public void Dot_MatrixVector_ProducesVector()
    {
        var m = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });
        var v = Numpy.Array(new[] { 5.0, 6.0 });

        var result = NumpyLinalg.Dot(m, v);

        result.Shape.Should().Equal(new[] { 2 });
        result[0].Should().BeApproximately(17.0, Tol);
        result[1].Should().BeApproximately(39.0, Tol);
    }

    [Fact]
    public void Dot_VectorMatrix_ProducesVector()
    {
        var v = Numpy.Array(new[] { 1.0, 2.0 });
        var m = new NdArray<double>(new[] { 3.0, 4.0, 5.0, 6.0 }, new[] { 2, 2 });

        var result = NumpyLinalg.Dot(v, m);

        result.Shape.Should().Equal(new[] { 2 });
        result[0].Should().BeApproximately(13.0, Tol);
        result[1].Should().BeApproximately(16.0, Tol);
    }

    [Fact]
    public void Dot_ShapeMismatch_ThrowsValueError()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0 });
        var b = Numpy.Array(new[] { 1.0, 2.0, 3.0 });

        Action act = () => NumpyLinalg.Dot(a, b);

        act.Should().Throw<ValueError>().WithMessage("*not aligned*");
    }

    [Fact]
    public void Dot_MatrixDimMismatch_ThrowsValueError()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, new[] { 2, 3 });
        var b = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });

        Action act = () => NumpyLinalg.Dot(a, b);

        act.Should().Throw<ValueError>().WithMessage("*not aligned*");
    }

    [Fact]
    public void Dot_HigherRank_ThrowsValueError()
    {
        var a = new NdArray<double>(new double[8], new[] { 2, 2, 2 });
        var b = new NdArray<double>(new double[8], new[] { 2, 2, 2 });

        Action act = () => NumpyLinalg.Dot(a, b);

        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Dot_NullArg_ThrowsArgumentNullException()
    {
        var a = Numpy.Array(new[] { 1.0 });
        Action act = () => NumpyLinalg.Dot(null!, a);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Matmul_DelegatesToDot()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });
        var b = new NdArray<double>(new[] { 5.0, 6.0, 7.0, 8.0 }, new[] { 2, 2 });

        var dotResult = NumpyLinalg.Dot(a, b);
        var matmulResult = NumpyLinalg.Matmul(a, b);

        matmulResult.Shape.Should().Equal(dotResult.Shape);
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                matmulResult[i, j].Should().BeApproximately(dotResult[i, j], Tol);
            }
        }
    }

    [Fact]
    public void Dot_TransposedView_WorksOnNonContiguousInput()
    {
        // A = [[1,2],[3,4]] -> A.T = [[1,3],[2,4]]; A.T · A = [[10,14],[14,20]]
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });
        var at = a.Transpose();

        var result = NumpyLinalg.Dot(at, a);

        result[0, 0].Should().BeApproximately(10.0, Tol);
        result[0, 1].Should().BeApproximately(14.0, Tol);
        result[1, 0].Should().BeApproximately(14.0, Tol);
        result[1, 1].Should().BeApproximately(20.0, Tol);
    }

    [Fact]
    public void Numpy_Dot_TopLevelAlias_Works()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });
        var b = Numpy.Array(new[] { 4.0, 5.0, 6.0 });

        var result = Numpy.Dot(a, b);

        ScalarOf(result).Should().BeApproximately(32.0, Tol);
    }

    [Fact]
    public void Numpy_Matmul_TopLevelAlias_Works()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });
        var b = new NdArray<double>(new[] { 5.0, 6.0, 7.0, 8.0 }, new[] { 2, 2 });

        var result = Numpy.Matmul(a, b);

        result[0, 0].Should().BeApproximately(19.0, Tol);
    }

    #endregion

    #region Inv

    [Fact]
    public void Inv_TwoByTwo_ComputesInverse()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });

        var inv = NumpyLinalg.Inv(a);

        inv.Shape.Should().Equal(new[] { 2, 2 });
        inv[0, 0].Should().BeApproximately(-2.0, Tol);
        inv[0, 1].Should().BeApproximately(1.0, Tol);
        inv[1, 0].Should().BeApproximately(1.5, Tol);
        inv[1, 1].Should().BeApproximately(-0.5, Tol);
    }

    [Fact]
    public void Inv_TimesOriginal_GivesIdentity()
    {
        var a = new NdArray<double>(new[] { 4.0, 7.0, 2.0, 6.0 }, new[] { 2, 2 });
        var inv = NumpyLinalg.Inv(a);
        var product = NumpyLinalg.Dot(a, inv);

        product[0, 0].Should().BeApproximately(1.0, Tol);
        product[0, 1].Should().BeApproximately(0.0, Tol);
        product[1, 0].Should().BeApproximately(0.0, Tol);
        product[1, 1].Should().BeApproximately(1.0, Tol);
    }

    [Fact]
    public void Inv_Singular_ThrowsValueError()
    {
        // det = 0
        var a = new NdArray<double>(new[] { 1.0, 2.0, 2.0, 4.0 }, new[] { 2, 2 });

        Action act = () => NumpyLinalg.Inv(a);

        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Inv_NonSquare_ThrowsValueError()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, new[] { 2, 3 });

        Action act = () => NumpyLinalg.Inv(a);

        act.Should().Throw<ValueError>().WithMessage("*square*");
    }

    [Fact]
    public void Inv_NotTwoD_ThrowsValueError()
    {
        var a = Numpy.Array(new[] { 1.0, 2.0, 3.0 });

        Action act = () => NumpyLinalg.Inv(a);

        act.Should().Throw<ValueError>().WithMessage("*2-D*");
    }

    #endregion

    #region Det

    [Fact]
    public void Det_TwoByTwo_MatchesAnalytical()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });

        NumpyLinalg.Det(a).Should().BeApproximately(-2.0, Tol);
    }

    [Fact]
    public void Det_Identity_IsOne()
    {
        var id = Numpy.Eye(4);

        NumpyLinalg.Det(id).Should().BeApproximately(1.0, Tol);
    }

    [Fact]
    public void Det_Singular_IsZero()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 2.0, 4.0 }, new[] { 2, 2 });

        NumpyLinalg.Det(a).Should().BeApproximately(0.0, Tol);
    }

    [Fact]
    public void Det_NonSquare_ThrowsValueError()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, new[] { 2, 3 });

        Action act = () => NumpyLinalg.Det(a);

        act.Should().Throw<ValueError>().WithMessage("*square*");
    }

    #endregion

    #region Eig

    [Fact]
    public void Eig_DiagonalMatrix_ReturnsDiagonalAsEigenvalues()
    {
        // Diagonal matrix: eigenvalues are the diagonal entries.
        var a = new NdArray<double>(new[] { 3.0, 0.0, 0.0, 5.0 }, new[] { 2, 2 });

        var (values, vectors) = NumpyLinalg.Eig(a);

        values.Shape.Should().Equal(new[] { 2 });
        vectors.Shape.Should().Equal(new[] { 2, 2 });

        var sortedValues = new[] { values[0], values[1] }.OrderBy(x => x).ToArray();
        sortedValues[0].Should().BeApproximately(3.0, 1e-6);
        sortedValues[1].Should().BeApproximately(5.0, 1e-6);
    }

    [Fact]
    public void Eig_SymmetricMatrix_HasKnownEigenvalues()
    {
        // [[2,1],[1,2]] has eigenvalues {1, 3}
        var a = new NdArray<double>(new[] { 2.0, 1.0, 1.0, 2.0 }, new[] { 2, 2 });

        var (values, _) = NumpyLinalg.Eig(a);

        var sorted = new[] { values[0], values[1] }.OrderBy(x => x).ToArray();
        sorted[0].Should().BeApproximately(1.0, 1e-6);
        sorted[1].Should().BeApproximately(3.0, 1e-6);
    }

    [Fact]
    public void Eig_NonSquare_ThrowsValueError()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, new[] { 2, 3 });

        Action act = () => NumpyLinalg.Eig(a);

        act.Should().Throw<ValueError>().WithMessage("*square*");
    }

    #endregion

    #region Svd

    [Fact]
    public void Svd_RecomposesOriginalMatrix()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });

        var (u, s, vh) = NumpyLinalg.Svd(a);

        u.Shape.Should().Equal(new[] { 2, 2 });
        s.Shape.Should().Equal(new[] { 2 });
        vh.Shape.Should().Equal(new[] { 2, 2 });

        // Reconstruct A = U · diag(S) · Vh
        var sigma = new NdArray<double>(new[] { s[0], 0.0, 0.0, s[1] }, new[] { 2, 2 });
        var us = NumpyLinalg.Dot(u, sigma);
        var recon = NumpyLinalg.Dot(us, vh);

        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                recon[i, j].Should().BeApproximately(a[i, j], 1e-9);
            }
        }
    }

    [Fact]
    public void Svd_SingularValuesAreNonNegativeAndDescending()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, new[] { 3, 2 });

        var (_, s, _) = NumpyLinalg.Svd(a);

        s[0].Should().BeGreaterThanOrEqualTo(s[1] - Tol);
        s[0].Should().BeGreaterThanOrEqualTo(0);
        s[1].Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Solve

    [Fact]
    public void Solve_TwoByTwoSystem_FindsKnownSolution()
    {
        // [[1,2],[3,4]] x = [5,11]  ->  x = [1,2]
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });
        var b = Numpy.Array(new[] { 5.0, 11.0 });

        var x = NumpyLinalg.Solve(a, b);

        x.Shape.Should().Equal(new[] { 2 });
        x[0].Should().BeApproximately(1.0, Tol);
        x[1].Should().BeApproximately(2.0, Tol);
    }

    [Fact]
    public void Solve_MultipleRhs_ReturnsMatrixSolution()
    {
        // A · X = B, with A and B both 2x2
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });
        var b = new NdArray<double>(new[] { 5.0, 6.0, 11.0, 14.0 }, new[] { 2, 2 });

        var x = NumpyLinalg.Solve(a, b);

        x.Shape.Should().Equal(new[] { 2, 2 });
        // Verify A·X == B
        var prod = NumpyLinalg.Dot(a, x);
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                prod[i, j].Should().BeApproximately(b[i, j], Tol);
            }
        }
    }

    [Fact]
    public void Solve_Singular_ThrowsValueError()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 2.0, 4.0 }, new[] { 2, 2 });
        var b = Numpy.Array(new[] { 1.0, 2.0 });

        Action act = () => NumpyLinalg.Solve(a, b);

        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Solve_NonSquare_ThrowsValueError()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }, new[] { 2, 3 });
        var b = Numpy.Array(new[] { 1.0, 2.0 });

        Action act = () => NumpyLinalg.Solve(a, b);

        act.Should().Throw<ValueError>().WithMessage("*square*");
    }

    [Fact]
    public void Solve_DimensionMismatch_ThrowsValueError()
    {
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });
        var b = Numpy.Array(new[] { 1.0, 2.0, 3.0 });

        Action act = () => NumpyLinalg.Solve(a, b);

        act.Should().Throw<ValueError>().WithMessage("*not aligned*");
    }

    #endregion

    #region Norm

    [Fact]
    public void Norm_Vector_Returns_L2Norm()
    {
        var v = Numpy.Array(new[] { 3.0, 4.0 });

        NumpyLinalg.Norm(v).Should().BeApproximately(5.0, Tol);
    }

    [Fact]
    public void Norm_Vector_Higher_Dim()
    {
        // sqrt(1+4+9+16+25) == sqrt(55)
        var v = Numpy.Array(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });

        NumpyLinalg.Norm(v).Should().BeApproximately(System.Math.Sqrt(55.0), Tol);
    }

    [Fact]
    public void Norm_Matrix_FrobeniusNorm()
    {
        // [[1,2],[3,4]]: ||A||_F = sqrt(1+4+9+16) = sqrt(30)
        var a = new NdArray<double>(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 2, 2 });

        NumpyLinalg.Norm(a).Should().BeApproximately(System.Math.Sqrt(30.0), Tol);
    }

    [Fact]
    public void Norm_ZeroVector_IsZero()
    {
        var v = Numpy.Array(new[] { 0.0, 0.0, 0.0 });

        NumpyLinalg.Norm(v).Should().BeApproximately(0.0, Tol);
    }

    [Fact]
    public void Norm_HigherRank_ThrowsValueError()
    {
        var a = new NdArray<double>(new double[8], new[] { 2, 2, 2 });

        Action act = () => NumpyLinalg.Norm(a);

        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Norm_Null_ThrowsArgumentNullException()
    {
        Action act = () => NumpyLinalg.Norm(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    private static double ScalarOf(NdArray<double> a)
    {
        // 0-D arrays expose their single element via the empty-indices indexer.
        if (a.Ndim == 0)
        {
            return a[Array.Empty<int>()];
        }

        return a[0];
    }
}
