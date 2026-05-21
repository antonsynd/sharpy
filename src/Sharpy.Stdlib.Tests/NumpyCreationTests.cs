using System;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class NumpyCreationTests
{
    #region Array

    [Fact]
    public void Array_From1DData_CreatesNdArray()
    {
        var arr = Numpy.Array(new[] { 1.0, 2.0, 3.0 });

        arr.Ndim.Should().Be(1);
        arr.Size.Should().Be(3);
        arr.Shape.Should().Equal(new[] { 3 });
        arr[0].Should().Be(1.0);
        arr[1].Should().Be(2.0);
        arr[2].Should().Be(3.0);
    }

    [Fact]
    public void Array_CopiesData()
    {
        var data = new[] { 1, 2, 3 };
        var arr = Numpy.Array(data);
        data[0] = 99;
        // Source mutation must not affect the array
        arr[0].Should().Be(1);
    }

    [Fact]
    public void Array_Null_Throws()
    {
        Action act = () => Numpy.Array<int>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Array_Empty_CreatesEmpty()
    {
        var arr = Numpy.Array(Array.Empty<int>());
        arr.Size.Should().Be(0);
        arr.Shape.Should().Equal(new[] { 0 });
    }

    #endregion

    #region Zeros

    [Fact]
    public void Zeros_1D_AllElementsZero()
    {
        var arr = Numpy.Zeros(5);
        arr.Shape.Should().Equal(new[] { 5 });
        for (int i = 0; i < 5; i++)
        {
            arr[i].Should().Be(0.0);
        }
    }

    [Fact]
    public void Zeros_2D_AllElementsZero()
    {
        var arr = Numpy.Zeros(2, 3);
        arr.Shape.Should().Equal(new[] { 2, 3 });
        arr.Size.Should().Be(6);
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                arr[i, j].Should().Be(0.0);
            }
        }
    }

    [Fact]
    public void Zeros_Dtype_IsFloat64()
    {
        var arr = Numpy.Zeros(3);
        arr.Dtype.Should().Be("float64");
    }

    [Fact]
    public void Zeros_NegativeDimension_Throws()
    {
        Action act = () => Numpy.Zeros(-1);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Ones

    [Fact]
    public void Ones_1D_AllElementsOne()
    {
        var arr = Numpy.Ones(4);
        arr.Shape.Should().Equal(new[] { 4 });
        for (int i = 0; i < 4; i++)
        {
            arr[i].Should().Be(1.0);
        }
    }

    [Fact]
    public void Ones_2D_AllElementsOne()
    {
        var arr = Numpy.Ones(2, 2);
        arr.Shape.Should().Equal(new[] { 2, 2 });
        arr[0, 0].Should().Be(1.0);
        arr[0, 1].Should().Be(1.0);
        arr[1, 0].Should().Be(1.0);
        arr[1, 1].Should().Be(1.0);
    }

    #endregion

    #region Full

    [Fact]
    public void Full_1D_AllElementsAreFillValue()
    {
        var arr = Numpy.Full(new[] { 4 }, 7);
        arr.Shape.Should().Equal(new[] { 4 });
        for (int i = 0; i < 4; i++)
        {
            arr[i].Should().Be(7);
        }
    }

    [Fact]
    public void Full_2D_AllElementsAreFillValue()
    {
        var arr = Numpy.Full(new[] { 2, 3 }, 2.5);
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                arr[i, j].Should().Be(2.5);
            }
        }
    }

    [Fact]
    public void Full_NullShape_Throws()
    {
        Action act = () => Numpy.Full<int>(null!, 1);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Eye

    [Fact]
    public void Eye_2x2_IsIdentity()
    {
        var arr = Numpy.Eye(2);
        arr.Shape.Should().Equal(new[] { 2, 2 });
        arr[0, 0].Should().Be(1.0);
        arr[0, 1].Should().Be(0.0);
        arr[1, 0].Should().Be(0.0);
        arr[1, 1].Should().Be(1.0);
    }

    [Fact]
    public void Eye_3x3_IsIdentity()
    {
        var arr = Numpy.Eye(3);
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                arr[i, j].Should().Be(i == j ? 1.0 : 0.0);
            }
        }
    }

    [Fact]
    public void Eye_Zero_ReturnsEmptyMatrix()
    {
        var arr = Numpy.Eye(0);
        arr.Shape.Should().Equal(new[] { 0, 0 });
        arr.Size.Should().Be(0);
    }

    [Fact]
    public void Eye_Negative_Throws()
    {
        Action act = () => Numpy.Eye(-1);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Arange

    [Fact]
    public void Arange_DefaultStep_Generates()
    {
        var arr = Numpy.Arange(0.0, 5.0);
        arr.Shape.Should().Equal(new[] { 5 });
        arr[0].Should().Be(0.0);
        arr[1].Should().Be(1.0);
        arr[4].Should().Be(4.0);
    }

    [Fact]
    public void Arange_CustomStep_Generates()
    {
        var arr = Numpy.Arange(0.0, 10.0, 2.0);
        arr.Shape.Should().Equal(new[] { 5 });
        arr[0].Should().Be(0.0);
        arr[4].Should().Be(8.0);
    }

    [Fact]
    public void Arange_StopEqualsStart_ReturnsEmpty()
    {
        var arr = Numpy.Arange(5.0, 5.0);
        arr.Size.Should().Be(0);
    }

    [Fact]
    public void Arange_NegativeStep_Decreases()
    {
        var arr = Numpy.Arange(5.0, 0.0, -1.0);
        arr.Shape.Should().Equal(new[] { 5 });
        arr[0].Should().Be(5.0);
        arr[4].Should().Be(1.0);
    }

    [Fact]
    public void Arange_ZeroStep_Throws()
    {
        Action act = () => Numpy.Arange(0.0, 5.0, 0.0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Arange_StopLessThanStartWithPositiveStep_ReturnsEmpty()
    {
        var arr = Numpy.Arange(5.0, 0.0, 1.0);
        arr.Size.Should().Be(0);
    }

    #endregion

    #region Linspace

    [Fact]
    public void Linspace_Endpoints_AreExact()
    {
        var arr = Numpy.Linspace(0.0, 1.0, 5);
        arr.Shape.Should().Equal(new[] { 5 });
        arr[0].Should().Be(0.0);
        arr[4].Should().Be(1.0);
    }

    [Fact]
    public void Linspace_EvenlySpaced()
    {
        var arr = Numpy.Linspace(0.0, 1.0, 5);
        arr[1].Should().BeApproximately(0.25, 1e-9);
        arr[2].Should().BeApproximately(0.5, 1e-9);
        arr[3].Should().BeApproximately(0.75, 1e-9);
    }

    [Fact]
    public void Linspace_DefaultNum_Is50()
    {
        var arr = Numpy.Linspace(0.0, 1.0);
        arr.Size.Should().Be(50);
    }

    [Fact]
    public void Linspace_NumOne_ReturnsStart()
    {
        var arr = Numpy.Linspace(2.0, 5.0, 1);
        arr.Shape.Should().Equal(new[] { 1 });
        arr[0].Should().Be(2.0);
    }

    [Fact]
    public void Linspace_NumZero_ReturnsEmpty()
    {
        var arr = Numpy.Linspace(0.0, 1.0, 0);
        arr.Size.Should().Be(0);
        arr.Shape.Should().Equal(new[] { 0 });
    }

    [Fact]
    public void Linspace_NegativeNum_Throws()
    {
        Action act = () => Numpy.Linspace(0.0, 1.0, -1);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Empty

    [Fact]
    public void Empty_HasCorrectShape()
    {
        var arr = Numpy.Empty(2, 3);
        arr.Shape.Should().Equal(new[] { 2, 3 });
        arr.Size.Should().Be(6);
    }

    [Fact]
    public void Empty_Dtype_IsFloat64()
    {
        var arr = Numpy.Empty(3);
        arr.Dtype.Should().Be("float64");
    }

    #endregion
}
