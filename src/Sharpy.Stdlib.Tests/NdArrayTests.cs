using System;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class NdArrayTests
{
    #region Constructor and Basic Properties

    [Fact]
    public void Constructor_OneDimensional_SetsProperties()
    {
        var data = new[] { 1.0, 2.0, 3.0, 4.0 };
        var arr = new NdArray<double>(data, new[] { 4 });

        arr.Ndim.Should().Be(1);
        arr.Size.Should().Be(4);
        arr.Shape.Should().Equal(new[] { 4 });
        arr.Dtype.Should().Be("float64");
    }

    [Fact]
    public void Constructor_TwoDimensional_SetsProperties()
    {
        var data = new[] { 1, 2, 3, 4, 5, 6 };
        var arr = new NdArray<int>(data, new[] { 2, 3 });

        arr.Ndim.Should().Be(2);
        arr.Size.Should().Be(6);
        arr.Shape.Should().Equal(new[] { 2, 3 });
        arr.Dtype.Should().Be("int32");
    }

    [Fact]
    public void Constructor_ThreeDimensional_SetsProperties()
    {
        var data = new double[2 * 3 * 4];
        var arr = new NdArray<double>(data, new[] { 2, 3, 4 });

        arr.Ndim.Should().Be(3);
        arr.Size.Should().Be(24);
        arr.Shape.Should().Equal(new[] { 2, 3, 4 });
    }

    [Fact]
    public void Constructor_ZeroDimensional_HasNdimZero()
    {
        var data = new[] { 42.0 };
        var arr = new NdArray<double>(data, new int[0]);

        arr.Ndim.Should().Be(0);
        // Product of empty shape is 1 (empty product).
        arr.Size.Should().Be(1);
        arr.Shape.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_EmptyDimension_HasSizeZero()
    {
        var data = Array.Empty<double>();
        var arr = new NdArray<double>(data, new[] { 0 });

        arr.Ndim.Should().Be(1);
        arr.Size.Should().Be(0);
        arr.Shape.Should().Equal(new[] { 0 });
    }

    [Fact]
    public void Constructor_EmptyShapeWithZeroDim_HasSizeZero()
    {
        var data = Array.Empty<int>();
        var arr = new NdArray<int>(data, new[] { 2, 0, 3 });

        arr.Size.Should().Be(0);
    }

    [Fact]
    public void Constructor_DataLengthMismatch_Throws()
    {
        var data = new[] { 1, 2, 3 };
        Action act = () => new NdArray<int>(data, new[] { 2, 2 });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeDimension_Throws()
    {
        var data = new[] { 1, 2, 3 };
        Action act = () => new NdArray<int>(data, new[] { -1, 3 });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullData_Throws()
    {
        Action act = () => new NdArray<int>(null!, new[] { 0 });
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullShape_Throws()
    {
        Action act = () => new NdArray<int>(new[] { 1 }, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Strides

    [Fact]
    public void Strides_OneDimensional_IsOne()
    {
        var arr = new NdArray<double>(new double[5], new[] { 5 });
        arr.Strides.Should().Equal(new[] { 1 });
    }

    [Fact]
    public void Strides_TwoDimensional_RowMajor()
    {
        var arr = new NdArray<double>(new double[6], new[] { 2, 3 });
        // Row-major: strides are [cols, 1] = [3, 1]
        arr.Strides.Should().Equal(new[] { 3, 1 });
    }

    [Fact]
    public void Strides_ThreeDimensional_RowMajor()
    {
        var arr = new NdArray<double>(new double[24], new[] { 2, 3, 4 });
        // Row-major: [3*4, 4, 1] = [12, 4, 1]
        arr.Strides.Should().Equal(new[] { 12, 4, 1 });
    }

    [Fact]
    public void Shape_ReturnsDefensiveCopy()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        var shape = arr.Shape;
        shape[0] = 99;
        // Modifying the returned shape array must not affect the array.
        arr.Shape.Should().Equal(new[] { 2, 2 });
    }

    [Fact]
    public void Strides_ReturnsDefensiveCopy()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        var strides = arr.Strides;
        strides[0] = 99;
        arr.Strides.Should().Equal(new[] { 2, 1 });
    }

    #endregion

    #region Dtype mappings

    [Fact]
    public void Dtype_Float64()
    {
        new NdArray<double>(new double[1], new[] { 1 }).Dtype.Should().Be("float64");
    }

    [Fact]
    public void Dtype_Float32()
    {
        new NdArray<float>(new float[1], new[] { 1 }).Dtype.Should().Be("float32");
    }

    [Fact]
    public void Dtype_Int32()
    {
        new NdArray<int>(new int[1], new[] { 1 }).Dtype.Should().Be("int32");
    }

    [Fact]
    public void Dtype_Int64()
    {
        new NdArray<long>(new long[1], new[] { 1 }).Dtype.Should().Be("int64");
    }

    [Fact]
    public void Dtype_Bool()
    {
        new NdArray<bool>(new bool[1], new[] { 1 }).Dtype.Should().Be("bool");
    }

    #endregion
}
