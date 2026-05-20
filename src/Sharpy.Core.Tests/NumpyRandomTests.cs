using System;
using System.Linq;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class NumpyRandomTests
{
    [Fact]
    public void Seed_ProducesReproducibleSequence()
    {
        NumpyRandom.Seed(42);
        var a = NumpyRandom.Rand(5);
        NumpyRandom.Seed(42);
        var b = NumpyRandom.Rand(5);
        for (int i = 0; i < 5; i++)
        {
            a._data[i].Should().Be(b._data[i]);
        }
    }

    [Fact]
    public void Rand_ProducesValuesInUnitInterval()
    {
        NumpyRandom.Seed(7);
        var arr = NumpyRandom.Rand(100);
        arr.Shape.Should().Equal(new[] { 100 });
        foreach (var v in arr._data)
        {
            v.Should().BeInRange(0.0, 1.0);
            v.Should().NotBe(1.0);
        }
    }

    [Fact]
    public void Rand_MultiDimensionalShape()
    {
        NumpyRandom.Seed(3);
        var arr = NumpyRandom.Rand(2, 3, 4);
        arr.Shape.Should().Equal(new[] { 2, 3, 4 });
        arr.Size.Should().Be(24);
    }

    [Fact]
    public void Randn_HasApproximatelyZeroMean()
    {
        NumpyRandom.Seed(123);
        var arr = NumpyRandom.Randn(10000);
        var mean = arr._data.Average();
        mean.Should().BeApproximately(0.0, 0.05);
    }

    [Fact]
    public void Randint_ProducesValuesInRange()
    {
        NumpyRandom.Seed(11);
        var arr = NumpyRandom.Randint(5, 10, new[] { 200 });
        foreach (var v in arr._data)
        {
            v.Should().BeInRange(5, 9);
        }
    }

    [Fact]
    public void Randint_ThrowsWhenHighNotGreaterThanLow()
    {
        Action act = () => NumpyRandom.Randint(5, 5, new[] { 4 });
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Normal_RespectsMeanAndScale()
    {
        NumpyRandom.Seed(99);
        var arr = NumpyRandom.Normal(10.0, 2.0, new[] { 10000 });
        var mean = arr._data.Average();
        mean.Should().BeApproximately(10.0, 0.1);
    }

    [Fact]
    public void Normal_ThrowsWhenScaleNegative()
    {
        Action act = () => NumpyRandom.Normal(0.0, -1.0, new[] { 5 });
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Uniform_ProducesValuesInRange()
    {
        NumpyRandom.Seed(8);
        var arr = NumpyRandom.Uniform(-3.0, 5.0, new[] { 500 });
        foreach (var v in arr._data)
        {
            v.Should().BeInRange(-3.0, 5.0);
        }
    }

    [Fact]
    public void Uniform_ThrowsWhenHighLessThanLow()
    {
        Action act = () => NumpyRandom.Uniform(5.0, 0.0, new[] { 4 });
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Choice_WithReplacement()
    {
        NumpyRandom.Seed(4);
        var source = Numpy.Array(new[] { 10, 20, 30, 40, 50 });
        var arr = NumpyRandom.Choice(source, 100, replace: true);
        arr.Shape.Should().Equal(new[] { 100 });
        foreach (var v in arr._data)
        {
            new[] { 10, 20, 30, 40, 50 }.Should().Contain(v);
        }
    }

    [Fact]
    public void Choice_WithoutReplacement_HasUniqueElements()
    {
        NumpyRandom.Seed(15);
        var source = Numpy.Array(new[] { 1, 2, 3, 4, 5 });
        var arr = NumpyRandom.Choice(source, 5, replace: false);
        arr.Shape.Should().Equal(new[] { 5 });
        arr._data.Distinct().Count().Should().Be(5);
    }

    [Fact]
    public void Choice_WithoutReplacement_ThrowsWhenSizeTooLarge()
    {
        var source = Numpy.Array(new[] { 1, 2, 3 });
        Action act = () => NumpyRandom.Choice(source, 5, replace: false);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Choice_ThrowsForNon1D()
    {
        var source = new NdArray<int>(new[] { 1, 2, 3, 4 }, new[] { 2, 2 });
        Action act = () => NumpyRandom.Choice(source, 2);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Shuffle_PreservesElements()
    {
        NumpyRandom.Seed(21);
        var arr = Numpy.Array(new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        NumpyRandom.Shuffle(arr);
        arr._data.OrderBy(x => x).Should().Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
    }

    [Fact]
    public void Shuffle_TwoDimensional_PermutesRows()
    {
        NumpyRandom.Seed(33);
        // Rows: [0,1,2], [10,11,12], [20,21,22]
        var arr = new NdArray<int>(
            new[] { 0, 1, 2, 10, 11, 12, 20, 21, 22 },
            new[] { 3, 3 });
        NumpyRandom.Shuffle(arr);
        // Each row preserved as a unit — check that the set of "leading digits" is still {0, 10, 20}
        var firstColumn = new[] { arr._data[0], arr._data[3], arr._data[6] };
        firstColumn.OrderBy(x => x).Should().Equal(new[] { 0, 10, 20 });
    }

    [Fact]
    public void Shuffle_OneDim_NoOpOnSingleton()
    {
        var arr = Numpy.Array(new[] { 42 });
        NumpyRandom.Shuffle(arr);
        arr._data[0].Should().Be(42);
    }
}
