using System;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class NdArrayIndexingTests
{
    #region 1-D indexing

    [Fact]
    public void Indexer_1D_Positive_Reads()
    {
        var arr = new NdArray<int>(new[] { 10, 20, 30, 40 }, new[] { 4 });
        arr[0].Should().Be(10);
        arr[3].Should().Be(40);
    }

    [Fact]
    public void Indexer_1D_Negative_CountsFromEnd()
    {
        var arr = new NdArray<int>(new[] { 10, 20, 30, 40 }, new[] { 4 });
        arr[-1].Should().Be(40);
        arr[-4].Should().Be(10);
    }

    [Fact]
    public void Indexer_1D_Set_UpdatesValue()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });
        arr[1] = 99;
        arr[1].Should().Be(99);
    }

    [Fact]
    public void Indexer_1D_SetNegative_UpdatesValue()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });
        arr[-1] = 99;
        arr[2].Should().Be(99);
    }

    [Fact]
    public void Indexer_1D_OutOfRange_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });
        Action act = () => { var _ = arr[3]; };
        act.Should().Throw<IndexError>();
    }

    [Fact]
    public void Indexer_1D_NegativeOutOfRange_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });
        Action act = () => { var _ = arr[-4]; };
        act.Should().Throw<IndexError>();
    }

    #endregion

    #region 2-D indexing

    [Fact]
    public void Indexer_2D_Reads()
    {
        // 2x3:
        // 1 2 3
        // 4 5 6
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        arr[0, 0].Should().Be(1);
        arr[0, 2].Should().Be(3);
        arr[1, 0].Should().Be(4);
        arr[1, 2].Should().Be(6);
    }

    [Fact]
    public void Indexer_2D_Negative_CountsFromEnd()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        arr[-1, -1].Should().Be(6);
        arr[-2, -3].Should().Be(1);
    }

    [Fact]
    public void Indexer_2D_Set_UpdatesValue()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 3 });
        arr[1, 2] = 99;
        arr[1, 2].Should().Be(99);
    }

    [Fact]
    public void Indexer_2D_OutOfRange_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        Action act = () => { var _ = arr[2, 0]; };
        act.Should().Throw<IndexError>();
    }

    [Fact]
    public void Indexer_2D_WrongIndexCount_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        Action tooFew = () => { var _ = arr[1]; };
        Action tooMany = () => { var _ = arr[1, 1, 1]; };
        tooFew.Should().Throw<IndexError>();
        tooMany.Should().Throw<IndexError>();
    }

    #endregion

    #region 3-D indexing

    [Fact]
    public void Indexer_3D_Reads()
    {
        // 2x2x3
        var data = new int[12];
        for (int i = 0; i < 12; i++)
        {
            data[i] = i;
        }
        var arr = new NdArray<int>(data, new[] { 2, 2, 3 });

        // Row-major: flat index = i*6 + j*3 + k
        arr[0, 0, 0].Should().Be(0);
        arr[0, 0, 2].Should().Be(2);
        arr[0, 1, 0].Should().Be(3);
        arr[1, 0, 0].Should().Be(6);
        arr[1, 1, 2].Should().Be(11);
    }

    [Fact]
    public void Indexer_3D_Negative_CountsFromEnd()
    {
        var data = new int[12];
        for (int i = 0; i < 12; i++)
        {
            data[i] = i;
        }
        var arr = new NdArray<int>(data, new[] { 2, 2, 3 });
        arr[-1, -1, -1].Should().Be(11);
    }

    #endregion

    #region Null indices

    [Fact]
    public void Indexer_NullIndices_Throws()
    {
        var arr = new NdArray<int>(new[] { 1, 2, 3 }, new[] { 3 });
        Action act = () => { var _ = arr[null!]; };
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
