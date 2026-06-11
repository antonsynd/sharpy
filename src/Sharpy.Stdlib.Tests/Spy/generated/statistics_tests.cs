// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using statistics = global::Sharpy.Statistics;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Statistics.StatisticsTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Statistics
    {
        [global::Sharpy.SharpyModule("statistics.statistics_tests")]
        public static partial class StatisticsTests
        {
        }
    }

    public static partial class Statistics
    {
        public partial class StatisticsTestsTests
        {
            [Xunit.FactAttribute]
            public void TestMeanSimpleValues()
            {
#line (10, 5) - (10, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(3.0d, statistics.Mean(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d }));
            }

            [Xunit.FactAttribute]
            public void TestMeanSingleValue()
            {
#line (14, 5) - (14, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(42.0d, statistics.Mean(new Sharpy.List<double>() { 42.0d }));
            }

            [Xunit.FactAttribute]
            public void TestMeanEmptyDataThrowsStatisticsError()
            {
#line (18, 5) - (18, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Sharpy.List<double> empty = new Sharpy.List<double>()
                {
                };
#line (19, 5) - (22, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Throws<StatisticsError>((global::System.Action)(() =>
                {
#line (20, 9) - (20, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                    statistics.Mean(empty);
                }));
            }

            [Xunit.FactAttribute]
            public void TestMeanIntOverload()
            {
#line (24, 5) - (24, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(3.0d, statistics.Mean(new Sharpy.List<int>() { 1, 2, 3, 4, 5 }));
            }

            [Xunit.FactAttribute]
            public void TestMeanLongOverload()
            {
#line (28, 5) - (28, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(20.0d, statistics.Mean(new Sharpy.List<int>() { 10, 20, 30 }));
            }

            [Xunit.FactAttribute]
            public void TestFmeanMatchesMean()
            {
#line (34, 5) - (34, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(3.0d, statistics.Fmean(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d }));
            }

            [Xunit.FactAttribute]
            public void TestMedianOddCount()
            {
#line (40, 5) - (40, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(3.0d, statistics.Median(new Sharpy.List<double>() { 1.0d, 3.0d, 5.0d }));
            }

            [Xunit.FactAttribute]
            public void TestMedianEvenCount()
            {
#line (44, 5) - (44, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(2.5d, statistics.Median(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }));
            }

            [Xunit.FactAttribute]
            public void TestMedianSingleElement()
            {
#line (48, 5) - (48, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(7.0d, statistics.Median(new Sharpy.List<double>() { 7.0d }));
            }

            [Xunit.FactAttribute]
            public void TestMedianEmptyDataThrowsStatisticsError()
            {
#line (52, 5) - (52, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Sharpy.List<double> empty = new Sharpy.List<double>()
                {
                };
#line (53, 5) - (56, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Throws<StatisticsError>((global::System.Action)(() =>
                {
#line (54, 9) - (54, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                    statistics.Median(empty);
                }));
            }

            [Xunit.FactAttribute]
            public void TestMedianUnsortedInput()
            {
#line (58, 5) - (58, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(2.5d, statistics.Median(new Sharpy.List<double>() { 4.0d, 1.0d, 3.0d, 2.0d }));
            }

            [Xunit.FactAttribute]
            public void TestMedianIntOverload()
            {
#line (62, 5) - (62, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(2.5d, statistics.Median(new Sharpy.List<int>() { 1, 2, 3, 4 }));
            }

            [Xunit.FactAttribute]
            public void TestMedianLowEvenCount()
            {
#line (68, 5) - (68, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(2.0d, statistics.MedianLow(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }));
            }

            [Xunit.FactAttribute]
            public void TestMedianLowOddCount()
            {
#line (72, 5) - (72, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(3.0d, statistics.MedianLow(new Sharpy.List<double>() { 1.0d, 3.0d, 5.0d }));
            }

            [Xunit.FactAttribute]
            public void TestMedianHighEvenCount()
            {
#line (78, 5) - (78, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(3.0d, statistics.MedianHigh(new Sharpy.List<double>() { 1.0d, 2.0d, 3.0d, 4.0d }));
            }

            [Xunit.FactAttribute]
            public void TestMedianHighOddCount()
            {
#line (82, 5) - (82, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(3.0d, statistics.MedianHigh(new Sharpy.List<double>() { 1.0d, 3.0d, 5.0d }));
            }

            [Xunit.FactAttribute]
            public void TestModeClearWinner()
            {
#line (88, 5) - (88, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(1, statistics.Mode(new Sharpy.List<int>() { 1, 1, 2, 3 }));
            }

            [Xunit.FactAttribute]
            public void TestModeTiedReturnsFirstEncountered()
            {
#line (92, 5) - (92, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(1, statistics.Mode(new Sharpy.List<int>() { 1, 2, 1, 2, 3 }));
            }

            [Xunit.FactAttribute]
            public void TestModeSingleElement()
            {
#line (96, 5) - (96, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal("hello", statistics.Mode(new Sharpy.List<string>() { "hello" }));
            }

            [Xunit.FactAttribute]
            public void TestModeEmptyDataThrowsStatisticsError()
            {
#line (100, 5) - (100, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (101, 5) - (106, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Throws<StatisticsError>((global::System.Action)(() =>
                {
#line (102, 9) - (102, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                    statistics.Mode(empty);
                }));
            }

            [Xunit.FactAttribute]
            public void TestStdevKnownResult()
            {
#line (108, 5) - (108, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                double result = statistics.Stdev(new Sharpy.List<double>() { 2.0d, 4.0d, 4.0d, 4.0d, 5.0d, 5.0d, 7.0d, 9.0d });
#line (109, 5) - (109, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(2.138089935299395d, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestStdevSingleElementThrowsStatisticsError()
            {
#line (113, 5) - (116, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Throws<StatisticsError>((global::System.Action)(() =>
                {
#line (114, 9) - (114, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                    statistics.Stdev(new Sharpy.List<double>() { 1.0d });
                }));
            }

            [Xunit.FactAttribute]
            public void TestStdevIntOverload()
            {
#line (118, 5) - (118, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                double result = statistics.Stdev(new Sharpy.List<int>() { 2, 4, 4, 4, 5, 5, 7, 9 });
#line (119, 5) - (119, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(2.138089935299395d, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestPstdevKnownResult()
            {
#line (125, 5) - (125, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                double result = statistics.Pstdev(new Sharpy.List<double>() { 2.0d, 4.0d, 4.0d, 4.0d, 5.0d, 5.0d, 7.0d, 9.0d });
#line (126, 5) - (126, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(2.0d, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestVarianceKnownResult()
            {
#line (132, 5) - (132, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                double result = statistics.Variance(new Sharpy.List<double>() { 2.0d, 4.0d, 4.0d, 4.0d, 5.0d, 5.0d, 7.0d, 9.0d });
#line (133, 5) - (133, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(4.571428571428571d, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestVarianceSingleElementThrowsStatisticsError()
            {
#line (137, 5) - (142, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Throws<StatisticsError>((global::System.Action)(() =>
                {
#line (138, 9) - (138, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                    statistics.Variance(new Sharpy.List<double>() { 1.0d });
                }));
            }

            [Xunit.FactAttribute]
            public void TestPvarianceKnownResult()
            {
#line (144, 5) - (144, 84) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                double result = statistics.Pvariance(new Sharpy.List<double>() { 2.0d, 4.0d, 4.0d, 4.0d, 5.0d, 5.0d, 7.0d, 9.0d });
#line (145, 5) - (145, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(4.0d, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestPvarianceSingleElementDoesNotThrow()
            {
#line (149, 5) - (149, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                double result = statistics.Pvariance(new Sharpy.List<double>() { 5.0d });
#line (150, 5) - (150, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(0.0d, result);
            }

            [Xunit.FactAttribute]
            public void TestMedianDoesNotMutateInput()
            {
#line (156, 5) - (156, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Sharpy.List<double> data = new Sharpy.List<double>()
                {
                    4.0d,
                    1.0d,
                    3.0d,
                    2.0d
                };
#line (157, 5) - (157, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                statistics.Median(data);
#line (158, 5) - (158, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<double>() { 4.0d, 1.0d, 3.0d, 2.0d }, data);
            }

            [Xunit.FactAttribute]
            public void TestMedianLowDoesNotMutateInput()
            {
#line (162, 5) - (162, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Sharpy.List<double> data = new Sharpy.List<double>()
                {
                    4.0d,
                    1.0d,
                    3.0d,
                    2.0d
                };
#line (163, 5) - (163, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                statistics.MedianLow(data);
#line (164, 5) - (164, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<double>() { 4.0d, 1.0d, 3.0d, 2.0d }, data);
            }

            [Xunit.FactAttribute]
            public void TestMedianHighDoesNotMutateInput()
            {
#line (168, 5) - (168, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Sharpy.List<double> data = new Sharpy.List<double>()
                {
                    4.0d,
                    1.0d,
                    3.0d,
                    2.0d
                };
#line (169, 5) - (169, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                statistics.MedianHigh(data);
#line (170, 5) - (170, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<double>() { 4.0d, 1.0d, 3.0d, 2.0d }, data);
            }
        }
    }
}
