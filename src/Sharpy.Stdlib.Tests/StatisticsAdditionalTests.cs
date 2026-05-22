using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class StatisticsAdditionalTests
{
    // -- Pstdev with single element: population stdev of one value is 0 --

    [Fact]
    public void Pstdev_SingleElement_ReturnsZero()
    {
        // Python: statistics.pstdev([5]) -> 0.0
        Sharpy.Statistics.Pstdev(new double[] { 5.0 }).Should().Be(0.0);
    }

    // -- Variance and Pvariance with all identical values --

    [Fact]
    public void Variance_AllIdentical_ThrowsWhenSingleElement()
    {
        // variance([x]) requires at least 2 elements
        FluentActions.Invoking(() => Sharpy.Statistics.Variance(new double[] { 7.0 }))
            .Should().Throw<Sharpy.StatisticsError>();
    }

    [Fact]
    public void Variance_AllIdentical_ReturnsZero()
    {
        // Python: statistics.variance([3,3,3,3]) -> 0.0
        double result = Sharpy.Statistics.Variance(new double[] { 3.0, 3.0, 3.0, 3.0 });
        result.Should().Be(0.0);
    }

    [Fact]
    public void Pvariance_AllIdentical_ReturnsZero()
    {
        // Python: statistics.pvariance([3,3,3,3]) -> 0.0
        double result = Sharpy.Statistics.Pvariance(new double[] { 3.0, 3.0, 3.0, 3.0 });
        result.Should().Be(0.0);
    }

    // -- Integer and long overloads for Variance, Pvariance, Pstdev --

    [Fact]
    public void Variance_IntOverload_MatchesDoubleResult()
    {
        double expected = Sharpy.Statistics.Variance(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        double result = Sharpy.Statistics.Variance(new int[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        result.Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void Pvariance_IntOverload_MatchesDoubleResult()
    {
        double expected = Sharpy.Statistics.Pvariance(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        double result = Sharpy.Statistics.Pvariance(new int[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        result.Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void Pstdev_IntOverload_MatchesDoubleResult()
    {
        double expected = Sharpy.Statistics.Pstdev(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        double result = Sharpy.Statistics.Pstdev(new int[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        result.Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void Variance_LongOverload_MatchesDoubleResult()
    {
        double expected = Sharpy.Statistics.Variance(new double[] { 10, 20, 30 });
        double result = Sharpy.Statistics.Variance(new long[] { 10L, 20L, 30L });
        result.Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void Pvariance_LongOverload_MatchesDoubleResult()
    {
        double expected = Sharpy.Statistics.Pvariance(new double[] { 10, 20, 30 });
        double result = Sharpy.Statistics.Pvariance(new long[] { 10L, 20L, 30L });
        result.Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void Pstdev_LongOverload_MatchesDoubleResult()
    {
        double expected = Sharpy.Statistics.Pstdev(new double[] { 10, 20, 30 });
        double result = Sharpy.Statistics.Pstdev(new long[] { 10L, 20L, 30L });
        result.Should().BeApproximately(expected, 1e-10);
    }

    // -- Fmean edge cases --

    [Fact]
    public void Fmean_SingleElement_ReturnsThatElement()
    {
        Sharpy.Statistics.Fmean(new double[] { 42.0 }).Should().Be(42.0);
    }

    [Fact]
    public void Fmean_EmptyData_ThrowsStatisticsError()
    {
        FluentActions.Invoking(() => Sharpy.Statistics.Fmean(Array.Empty<double>()))
            .Should().Throw<Sharpy.StatisticsError>();
    }

    [Fact]
    public void Fmean_IntOverload_MatchesMean()
    {
        double expected = Sharpy.Statistics.Mean(new int[] { 1, 2, 3, 4, 5 });
        Sharpy.Statistics.Fmean(new int[] { 1, 2, 3, 4, 5 }).Should().Be(expected);
    }

    // -- Stdev with minimum two elements --

    [Fact]
    public void Stdev_TwoElements_ReturnsCorrectResult()
    {
        // Python: statistics.stdev([1, 3]) -> sqrt(2) ≈ 1.4142135623730951
        double result = Sharpy.Statistics.Stdev(new double[] { 1.0, 3.0 });
        result.Should().BeApproximately(System.Math.Sqrt(2.0), 1e-10);
    }

    [Fact]
    public void Variance_TwoElements_ReturnsCorrectResult()
    {
        // Python: statistics.variance([1, 3]) -> 2.0
        double result = Sharpy.Statistics.Variance(new double[] { 1.0, 3.0 });
        result.Should().BeApproximately(2.0, 1e-10);
    }

    // -- Mode: all unique values, first encountered wins --

    [Fact]
    public void Mode_AllUnique_ReturnsFirstEncountered()
    {
        // When all values appear exactly once, mode returns the first
        Sharpy.Statistics.Mode(new int[] { 3, 1, 4, 1 }).Should().Be(1);
    }

    [Fact]
    public void Mode_DoubleValues_ReturnsMode()
    {
        // Python: statistics.mode([1.5, 1.5, 2.0]) -> 1.5
        Sharpy.Statistics.Mode(new double[] { 1.5, 1.5, 2.0 }).Should().Be(1.5);
    }

    // -- Mean does not mutate input --

    [Fact]
    public void Mean_DoesNotMutateInput()
    {
        var data = new System.Collections.Generic.List<double> { 4.0, 1.0, 3.0, 2.0 };
        Sharpy.Statistics.Mean(data);
        data.Should().Equal(4.0, 1.0, 3.0, 2.0);
    }

    // -- Mean with floats: verify floating-point result --

    [Fact]
    public void Mean_FloatingPointValues_ReturnsCorrectAverage()
    {
        // Python: statistics.mean([0.1, 0.2, 0.3]) -> 0.2
        double result = Sharpy.Statistics.Mean(new double[] { 0.1, 0.2, 0.3 });
        result.Should().BeApproximately(0.2, 1e-14);
    }
}
