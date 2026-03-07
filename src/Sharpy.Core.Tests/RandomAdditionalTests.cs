using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class RandomAdditional_Tests
{
    // --- Randrange ---

    [Fact]
    public void Randrange_SingleArg_ReturnsInRange()
    {
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 100; i++)
        {
            var val = Sharpy.Random.Randrange(10);
            val.Should().BeGreaterThanOrEqualTo(0);
            val.Should().BeLessThan(10);
        }
    }

    [Fact]
    public void Randrange_TwoArgs_ReturnsInRange()
    {
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 100; i++)
        {
            var val = Sharpy.Random.Randrange(5, 15);
            val.Should().BeGreaterThanOrEqualTo(5);
            val.Should().BeLessThan(15);
        }
    }

    [Fact]
    public void Randrange_WithStep_ReturnsValidValues()
    {
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 100; i++)
        {
            var val = Sharpy.Random.Randrange(0, 10, 2);
            val.Should().BeGreaterThanOrEqualTo(0);
            val.Should().BeLessThan(10);
            (val % 2).Should().Be(0);
        }
    }

    [Fact]
    public void Randrange_NegativeStep()
    {
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 100; i++)
        {
            var val = Sharpy.Random.Randrange(10, 0, -2);
            val.Should().BeGreaterThan(0);
            val.Should().BeLessThanOrEqualTo(10);
            (val % 2).Should().Be(0);
        }
    }

    [Fact]
    public void Randrange_EmptyRange_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Randrange(5, 5))
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Randrange_ZeroStep_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Randrange(0, 10, 0))
            .Should().Throw<ValueError>();
    }

    // --- Gauss ---

    [Fact]
    public void Gauss_MeanAndStdDev_WithinTolerance()
    {
        Sharpy.Random.Seed(42);
        double mu = 5.0;
        double sigma = 2.0;
        int n = 10000;

        double sum = 0;
        double sumSq = 0;
        for (int i = 0; i < n; i++)
        {
            double val = Sharpy.Random.Gauss(mu, sigma);
            sum += val;
            sumSq += val * val;
        }

        double mean = sum / n;
        double variance = (sumSq / n) - (mean * mean);
        double stddev = System.Math.Sqrt(variance);

        mean.Should().BeApproximately(mu, 0.1);
        stddev.Should().BeApproximately(sigma, 0.1);
    }

    [Fact]
    public void Gauss_ZeroSigma_ReturnsMu()
    {
        // With sigma=0, result should always be mu
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 10; i++)
        {
            Sharpy.Random.Gauss(3.0, 0.0).Should().Be(3.0);
        }
    }

    // --- Getrandbits ---

    [Fact]
    public void Getrandbits_ReturnsValueInBitRange()
    {
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 100; i++)
        {
            var val = Sharpy.Random.Getrandbits(8);
            val.Should().BeGreaterThanOrEqualTo(0);
            val.Should().BeLessThan(256);
        }
    }

    [Fact]
    public void Getrandbits_ZeroBits_ReturnsZero()
    {
        Sharpy.Random.Getrandbits(0).Should().Be(0);
    }

    [Fact]
    public void Getrandbits_OneBit_ReturnsZeroOrOne()
    {
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 50; i++)
        {
            var val = Sharpy.Random.Getrandbits(1);
            val.Should().BeGreaterThanOrEqualTo(0);
            val.Should().BeLessThanOrEqualTo(1);
        }
    }

    [Fact]
    public void Getrandbits_NegativeBits_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Getrandbits(-1))
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Getrandbits_TooManyBits_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Getrandbits(31))
            .Should().Throw<ValueError>();
    }

    // --- Choices ---

    [Fact]
    public void Choices_UniformSelection_ReturnsFromPopulation()
    {
        Sharpy.Random.Seed(42);
        var pop = new List<string> { "a", "b", "c" };
        var result = Sharpy.Random.Choices(pop, k: 10);

        ((ICollection<string>)result).Count.Should().Be(10);
        foreach (var item in (IEnumerable<string>)result)
        {
            pop.Should().Contain(item);
        }
    }

    [Fact]
    public void Choices_WithWeights_RespectsDistribution()
    {
        Sharpy.Random.Seed(42);
        var pop = new List<string> { "rare", "common" };
        var weights = new List<double> { 0.01, 0.99 };

        var result = Sharpy.Random.Choices(pop, weights, k: 1000);

        int commonCount = 0;
        foreach (var item in (IEnumerable<string>)result)
        {
            if (item == "common")
                commonCount++;
        }

        // With 99% weight, common should appear most of the time
        commonCount.Should().BeGreaterThan(900);
    }

    [Fact]
    public void Choices_EmptyPopulation_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Choices(new List<int>(), k: 1))
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Choices_MismatchedWeights_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Choices(
            new List<int> { 1, 2, 3 },
            new List<double> { 1.0, 2.0 },
            k: 1))
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Choices_KZero_ReturnsEmpty()
    {
        Sharpy.Random.Seed(42);
        var result = Sharpy.Random.Choices(new List<int> { 1, 2, 3 }, k: 0);
        ((ICollection<int>)result).Count.Should().Be(0);
    }

    // --- Sys additions ---

    [Fact]
    public void Sys_Maxsize_IsIntMaxValue()
    {
        Sharpy.Sys.Maxsize.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Sys_Getsizeof_Null_ReturnsZero()
    {
        Sharpy.Sys.Getsizeof(null).Should().Be(0);
    }

    [Fact]
    public void Sys_Getsizeof_ValueType_ReturnsPositive()
    {
        var result = Sharpy.Sys.Getsizeof(42);
        // int should have a known size (4 bytes)
        result.Should().Be(4);
    }

    [Fact]
    public void Sys_Getsizeof_ReferenceType_ReturnsNegativeOne()
    {
        var result = Sharpy.Sys.Getsizeof("hello");
        result.Should().Be(-1);
    }
}
