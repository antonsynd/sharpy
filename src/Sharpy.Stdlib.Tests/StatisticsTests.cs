using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class StatisticsTests
{
    // -- mean --

    [Fact]
    public void Mean_SimpleValues()
    {
        Sharpy.Statistics.Mean(new double[] { 1, 2, 3, 4, 5 }).Should().Be(3.0);
    }

    [Fact]
    public void Mean_SingleValue()
    {
        Sharpy.Statistics.Mean(new double[] { 42 }).Should().Be(42.0);
    }

    [Fact]
    public void Mean_EmptyData_ThrowsStatisticsError()
    {
        FluentActions.Invoking(() => Sharpy.Statistics.Mean(Array.Empty<double>()))
            .Should().Throw<Sharpy.StatisticsError>();
    }

    [Fact]
    public void Mean_IntOverload()
    {
        Sharpy.Statistics.Mean(new int[] { 1, 2, 3, 4, 5 }).Should().Be(3.0);
    }

    [Fact]
    public void Mean_LongOverload()
    {
        Sharpy.Statistics.Mean(new long[] { 10, 20, 30 }).Should().Be(20.0);
    }

    // -- fmean --

    [Fact]
    public void Fmean_MatchesMean()
    {
        Sharpy.Statistics.Fmean(new double[] { 1, 2, 3, 4, 5 }).Should().Be(3.0);
    }

    // -- median --

    [Fact]
    public void Median_OddCount()
    {
        Sharpy.Statistics.Median(new double[] { 1, 3, 5 }).Should().Be(3.0);
    }

    [Fact]
    public void Median_EvenCount()
    {
        // Python: statistics.median([1,2,3,4]) -> 2.5
        Sharpy.Statistics.Median(new double[] { 1, 2, 3, 4 }).Should().Be(2.5);
    }

    [Fact]
    public void Median_SingleElement()
    {
        Sharpy.Statistics.Median(new double[] { 7 }).Should().Be(7.0);
    }

    [Fact]
    public void Median_EmptyData_ThrowsStatisticsError()
    {
        FluentActions.Invoking(() => Sharpy.Statistics.Median(Array.Empty<double>()))
            .Should().Throw<Sharpy.StatisticsError>();
    }

    [Fact]
    public void Median_UnsortedInput()
    {
        Sharpy.Statistics.Median(new double[] { 4, 1, 3, 2 }).Should().Be(2.5);
    }

    [Fact]
    public void Median_IntOverload()
    {
        Sharpy.Statistics.Median(new int[] { 1, 2, 3, 4 }).Should().Be(2.5);
    }

    // -- median_low --

    [Fact]
    public void MedianLow_EvenCount()
    {
        // Python: statistics.median_low([1,2,3,4]) -> 2
        Sharpy.Statistics.MedianLow(new double[] { 1, 2, 3, 4 }).Should().Be(2.0);
    }

    [Fact]
    public void MedianLow_OddCount()
    {
        Sharpy.Statistics.MedianLow(new double[] { 1, 3, 5 }).Should().Be(3.0);
    }

    // -- median_high --

    [Fact]
    public void MedianHigh_EvenCount()
    {
        // Python: statistics.median_high([1,2,3,4]) -> 3
        Sharpy.Statistics.MedianHigh(new double[] { 1, 2, 3, 4 }).Should().Be(3.0);
    }

    [Fact]
    public void MedianHigh_OddCount()
    {
        Sharpy.Statistics.MedianHigh(new double[] { 1, 3, 5 }).Should().Be(3.0);
    }

    // -- mode --

    [Fact]
    public void Mode_ClearWinner()
    {
        // Python: statistics.mode([1,1,2,3]) -> 1
        Sharpy.Statistics.Mode(new int[] { 1, 1, 2, 3 }).Should().Be(1);
    }

    [Fact]
    public void Mode_TiedReturnsFirstEncountered()
    {
        // When tied, first encountered wins
        Sharpy.Statistics.Mode(new int[] { 1, 2, 1, 2, 3 }).Should().Be(1);
    }

    [Fact]
    public void Mode_SingleElement()
    {
        Sharpy.Statistics.Mode(new string[] { "hello" }).Should().Be("hello");
    }

    [Fact]
    public void Mode_EmptyData_ThrowsStatisticsError()
    {
        FluentActions.Invoking(() => Sharpy.Statistics.Mode(Array.Empty<int>()))
            .Should().Throw<Sharpy.StatisticsError>();
    }

    // -- stdev --

    [Fact]
    public void Stdev_KnownResult()
    {
        // Python: statistics.stdev([2,4,4,4,5,5,7,9]) -> 2.138089935299395
        double result = Sharpy.Statistics.Stdev(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        result.Should().BeApproximately(2.138089935299395, 1e-10);
    }

    [Fact]
    public void Stdev_SingleElement_ThrowsStatisticsError()
    {
        FluentActions.Invoking(() => Sharpy.Statistics.Stdev(new double[] { 1 }))
            .Should().Throw<Sharpy.StatisticsError>();
    }

    [Fact]
    public void Stdev_IntOverload()
    {
        double result = Sharpy.Statistics.Stdev(new int[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        result.Should().BeApproximately(2.138089935299395, 1e-10);
    }

    // -- pstdev --

    [Fact]
    public void Pstdev_KnownResult()
    {
        // Python: statistics.pstdev([2,4,4,4,5,5,7,9]) -> 2.0
        double result = Sharpy.Statistics.Pstdev(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        result.Should().BeApproximately(2.0, 1e-10);
    }

    // -- variance --

    [Fact]
    public void Variance_KnownResult()
    {
        // Python: statistics.variance([2,4,4,4,5,5,7,9]) -> 4.571428571428571
        double result = Sharpy.Statistics.Variance(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        result.Should().BeApproximately(4.571428571428571, 1e-10);
    }

    [Fact]
    public void Variance_SingleElement_ThrowsStatisticsError()
    {
        FluentActions.Invoking(() => Sharpy.Statistics.Variance(new double[] { 1 }))
            .Should().Throw<Sharpy.StatisticsError>();
    }

    // -- pvariance --

    [Fact]
    public void Pvariance_KnownResult()
    {
        // Python: statistics.pvariance([2,4,4,4,5,5,7,9]) -> 4.0
        double result = Sharpy.Statistics.Pvariance(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        result.Should().BeApproximately(4.0, 1e-10);
    }

    [Fact]
    public void Pvariance_SingleElement_DoesNotThrow()
    {
        double result = Sharpy.Statistics.Pvariance(new double[] { 5 });
        result.Should().Be(0.0);
    }

    // -- non-mutation --

    [Fact]
    public void Median_DoesNotMutateInput()
    {
        var data = new Sharpy.List<double> { 4, 1, 3, 2 };
        Sharpy.Statistics.Median(data);
        data.Should().Equal(4, 1, 3, 2);
    }

    [Fact]
    public void MedianLow_DoesNotMutateInput()
    {
        var data = new Sharpy.List<double> { 4, 1, 3, 2 };
        Sharpy.Statistics.MedianLow(data);
        data.Should().Equal(4, 1, 3, 2);
    }

    [Fact]
    public void MedianHigh_DoesNotMutateInput()
    {
        var data = new Sharpy.List<double> { 4, 1, 3, 2 };
        Sharpy.Statistics.MedianHigh(data);
        data.Should().Equal(4, 1, 3, 2);
    }
}
