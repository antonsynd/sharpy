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
using static Sharpy.Stdlib.Tests.Spy.Statistics.StatisticsAdditionalTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Statistics
    {
        [global::Sharpy.SharpyModule("statistics.statistics_additional_tests")]
        public static partial class StatisticsAdditionalTests
        {
        }
    }

    public static partial class Statistics
    {
        public partial class StatisticsAdditionalTestsTests
        {
            [Xunit.FactAttribute]
            public void TestPstdevSingleElementReturnsZero()
            {
#line (10, 5) - (10, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(0.0d, statistics.Pstdev(new Sharpy.List<double>() { 5.0d }));
            }

            [Xunit.FactAttribute]
            public void TestVarianceAllIdenticalThrowsWhenSingleElement()
            {
#line (16, 5) - (19, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Throws<StatisticsError>((global::System.Action)(() =>
                {
#line (17, 9) - (17, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                    statistics.Variance(new Sharpy.List<double>() { 7.0d });
                }));
            }

            [Xunit.FactAttribute]
            public void TestVarianceAllIdenticalReturnsZero()
            {
#line (21, 5) - (21, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double result = statistics.Variance(new Sharpy.List<double>() { 3.0d, 3.0d, 3.0d, 3.0d });
#line (22, 5) - (22, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(0.0d, result);
            }

            [Xunit.FactAttribute]
            public void TestPvarianceAllIdenticalReturnsZero()
            {
#line (26, 5) - (26, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double result = statistics.Pvariance(new Sharpy.List<double>() { 3.0d, 3.0d, 3.0d, 3.0d });
#line (27, 5) - (27, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(0.0d, result);
            }

            [Xunit.FactAttribute]
            public void TestVarianceIntOverloadMatchesDoubleResult()
            {
#line (33, 5) - (33, 85) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double expected = statistics.Variance(new Sharpy.List<double>() { 2.0d, 4.0d, 4.0d, 4.0d, 5.0d, 5.0d, 7.0d, 9.0d });
#line (34, 5) - (34, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double result = statistics.Variance(new Sharpy.List<int>() { 2, 4, 4, 4, 5, 5, 7, 9 });
#line (35, 5) - (35, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(expected, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestPvarianceIntOverloadMatchesDoubleResult()
            {
#line (39, 5) - (39, 86) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double expected = statistics.Pvariance(new Sharpy.List<double>() { 2.0d, 4.0d, 4.0d, 4.0d, 5.0d, 5.0d, 7.0d, 9.0d });
#line (40, 5) - (40, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double result = statistics.Pvariance(new Sharpy.List<int>() { 2, 4, 4, 4, 5, 5, 7, 9 });
#line (41, 5) - (41, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(expected, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestPstdevIntOverloadMatchesDoubleResult()
            {
#line (45, 5) - (45, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double expected = statistics.Pstdev(new Sharpy.List<double>() { 2.0d, 4.0d, 4.0d, 4.0d, 5.0d, 5.0d, 7.0d, 9.0d });
#line (46, 5) - (46, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double result = statistics.Pstdev(new Sharpy.List<int>() { 2, 4, 4, 4, 5, 5, 7, 9 });
#line (47, 5) - (47, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(expected, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestVarianceLongOverloadMatchesDoubleResult()
            {
#line (51, 5) - (51, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double expected = statistics.Variance(new Sharpy.List<double>() { 10.0d, 20.0d, 30.0d });
#line (52, 5) - (52, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double result = statistics.Variance(new Sharpy.List<int>() { 10, 20, 30 });
#line (53, 5) - (53, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(expected, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestPvarianceLongOverloadMatchesDoubleResult()
            {
#line (57, 5) - (57, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double expected = statistics.Pvariance(new Sharpy.List<double>() { 10.0d, 20.0d, 30.0d });
#line (58, 5) - (58, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double result = statistics.Pvariance(new Sharpy.List<int>() { 10, 20, 30 });
#line (59, 5) - (59, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(expected, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestPstdevLongOverloadMatchesDoubleResult()
            {
#line (63, 5) - (63, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double expected = statistics.Pstdev(new Sharpy.List<double>() { 10.0d, 20.0d, 30.0d });
#line (64, 5) - (64, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double result = statistics.Pstdev(new Sharpy.List<int>() { 10, 20, 30 });
#line (65, 5) - (65, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(expected, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestFmeanSingleElementReturnsThatElement()
            {
#line (71, 5) - (71, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(42.0d, statistics.Fmean(new Sharpy.List<double>() { 42.0d }));
            }

            [Xunit.FactAttribute]
            public void TestFmeanEmptyDataThrowsStatisticsError()
            {
#line (75, 5) - (75, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Sharpy.List<double> empty = new Sharpy.List<double>()
                {
                };
#line (76, 5) - (79, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Throws<StatisticsError>((global::System.Action)(() =>
                {
#line (77, 9) - (77, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                    statistics.Fmean(empty);
                }));
            }

            [Xunit.FactAttribute]
            public void TestFmeanIntOverloadMatchesMean()
            {
#line (81, 5) - (81, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double expected = statistics.Mean(new Sharpy.List<int>() { 1, 2, 3, 4, 5 });
#line (82, 5) - (82, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(expected, statistics.Fmean(new Sharpy.List<int>() { 1, 2, 3, 4, 5 }));
            }

            [Xunit.FactAttribute]
            public void TestStdevTwoElementsReturnsCorrectResult()
            {
#line (88, 5) - (88, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double result = statistics.Stdev(new Sharpy.List<double>() { 1.0d, 3.0d });
#line (89, 5) - (89, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(1.4142135623730951d, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestVarianceTwoElementsReturnsCorrectResult()
            {
#line (93, 5) - (93, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double result = statistics.Variance(new Sharpy.List<double>() { 1.0d, 3.0d });
#line (94, 5) - (94, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(2.0d, result, 1e-10d);
            }

            [Xunit.FactAttribute]
            public void TestModeAllUniqueReturnsFirstEncountered()
            {
#line (100, 5) - (100, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(1, statistics.Mode(new Sharpy.List<int>() { 3, 1, 4, 1 }));
            }

            [Xunit.FactAttribute]
            public void TestModeDoubleValuesReturnsMode()
            {
#line (104, 5) - (104, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(1.5d, statistics.Mode(new Sharpy.List<double>() { 1.5d, 1.5d, 2.0d }));
            }

            [Xunit.FactAttribute]
            public void TestMeanDoesNotMutateInput()
            {
#line (110, 5) - (110, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Sharpy.List<double> data = new Sharpy.List<double>()
                {
                    4.0d,
                    1.0d,
                    3.0d,
                    2.0d
                };
#line (111, 5) - (111, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                statistics.Mean(data);
#line (112, 5) - (112, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<double>() { 4.0d, 1.0d, 3.0d, 2.0d }, data);
            }

            [Xunit.FactAttribute]
            public void TestMeanFloatingPointValuesReturnsCorrectAverage()
            {
#line (118, 5) - (118, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                double result = statistics.Mean(new Sharpy.List<double>() { 0.1d, 0.2d, 0.3d });
#line (119, 5) - (119, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/statistics/statistics_additional_tests.spy"
                Xunit.Assert.Equal(0.2d, result, 1e-14d);
            }
        }
    }
}
