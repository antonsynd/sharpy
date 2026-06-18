// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using datetime = global::Sharpy.Datetime;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Datetime.DatetimeTimeTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Datetime
    {
        [global::Sharpy.SharpyModule("datetime.datetime_time_tests")]
        public static partial class DatetimeTimeTests
        {
        }
    }

    public static partial class Datetime
    {
        public partial class DatetimeTimeTestsTests
        {
            [Xunit.FactAttribute]
            public void TestTimeHourOnlyOtherDefaultToZero()
            {
#line (15, 5) - (15, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t = new global::Sharpy.Time(14);
#line (16, 5) - (16, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(14, t.Hour);
#line (17, 5) - (17, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, t.Minute);
#line (18, 5) - (18, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, t.Second);
#line (19, 5) - (19, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, t.Microsecond);
            }

            [Xunit.FactAttribute]
            public void TestTimeMaximumValueIsValid()
            {
#line (23, 5) - (23, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t = new global::Sharpy.Time(23, 59, 59, 999999);
#line (24, 5) - (24, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(23, t.Hour);
#line (25, 5) - (25, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(59, t.Minute);
#line (26, 5) - (26, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(59, t.Second);
#line (27, 5) - (27, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(999999, t.Microsecond);
            }

            [Xunit.FactAttribute]
            public void TestTimeMinimumValueIsZero()
            {
#line (31, 5) - (31, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t = new global::Sharpy.Time(0, 0, 0, 0);
#line (32, 5) - (32, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, t.Hour);
#line (33, 5) - (33, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, t.Minute);
#line (34, 5) - (34, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, t.Second);
#line (35, 5) - (35, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, t.Microsecond);
            }

            [Xunit.FactAttribute]
            public void TestTimeIsoformatWithoutMicrosecondsHasNoDecimal()
            {
#line (39, 5) - (39, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t = new global::Sharpy.Time(10, 30, 0);
#line (40, 5) - (40, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal("10:30:00", t.Isoformat());
#line (41, 5) - (41, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.DoesNotContain(".", t.Isoformat());
            }

            [Xunit.FactAttribute]
            public void TestTimeIsoformatWithMicrosecondsHasSixDecimalDigits()
            {
#line (45, 5) - (45, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t = new global::Sharpy.Time(10, 30, 0, 5);
#line (46, 5) - (46, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal("10:30:00.000005", t.Isoformat());
            }

            [Xunit.FactAttribute]
            public void TestTimeStrftimeHoursMinutesSeconds()
            {
#line (50, 5) - (50, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t = new global::Sharpy.Time(14, 30, 45);
#line (51, 5) - (51, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal("14:30:45", t.Strftime("%H:%M:%S"));
            }

            [Xunit.FactAttribute]
            public void TestTimeStrftimeTwelveHourClock()
            {
#line (55, 5) - (55, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t = new global::Sharpy.Time(14, 30, 0);
#line (56, 5) - (56, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal("02:30 PM", t.Strftime("%I:%M %p"));
            }

            [Xunit.FactAttribute]
            public void TestTimeComparisonGreaterThan()
            {
#line (60, 5) - (60, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t1 = new global::Sharpy.Time(12, 0, 0);
#line (61, 5) - (61, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t2 = new global::Sharpy.Time(11, 59, 59);
#line (62, 5) - (62, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.True(t1 > t2);
            }

            [Xunit.FactAttribute]
            public void TestTimeComparisonEqual()
            {
#line (66, 5) - (66, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t1 = new global::Sharpy.Time(10, 30, 0, 123456);
#line (67, 5) - (67, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t2 = new global::Sharpy.Time(10, 30, 0, 123456);
#line (68, 5) - (68, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(t2, t1);
            }

            [Xunit.FactAttribute]
            public void TestTimeComparisonNotEqual()
            {
#line (72, 5) - (72, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t1 = new global::Sharpy.Time(10, 0, 0);
#line (73, 5) - (73, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t2 = new global::Sharpy.Time(10, 0, 1);
#line (74, 5) - (74, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.NotEqual(t2, t1);
            }

            [Xunit.FactAttribute]
            public void TestTimeComparisonLessOrEqual()
            {
#line (78, 5) - (78, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t1 = new global::Sharpy.Time(10, 0, 0);
#line (79, 5) - (79, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t2 = new global::Sharpy.Time(10, 0, 0);
#line (80, 5) - (80, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.True(t1 <= t2);
            }

            [Xunit.FactAttribute]
            public void TestTimeComparisonGreaterOrEqual()
            {
#line (84, 5) - (84, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t1 = new global::Sharpy.Time(12, 0, 0);
#line (85, 5) - (85, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var t2 = new global::Sharpy.Time(12, 0, 0);
#line (86, 5) - (86, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.True(t1 >= t2);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaOneDayInSecondsIs86400()
            {
#line (92, 5) - (92, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td = new global::Sharpy.Timedelta(days: 1);
#line (93, 5) - (93, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(86400.0d, td.TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedelta3600SecondsIsOneHour()
            {
#line (97, 5) - (97, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td = new global::Sharpy.Timedelta(seconds: 3600);
#line (98, 5) - (98, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(3600.0d, td.TotalSeconds);
#line (99, 5) - (99, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, td.Days);
#line (100, 5) - (100, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(3600, td.Seconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedelta1000000MicrosecondsIsOneSecond()
            {
#line (104, 5) - (104, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td = new global::Sharpy.Timedelta(microseconds: 1000000);
#line (105, 5) - (105, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(1.0d, td.TotalSeconds);
#line (106, 5) - (106, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, td.Days);
#line (107, 5) - (107, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(1, td.Seconds);
#line (108, 5) - (108, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, td.Microseconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedelta90SecondsNormalizesDaysAndSeconds()
            {
#line (112, 5) - (112, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td = new global::Sharpy.Timedelta(seconds: 90);
#line (113, 5) - (113, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, td.Days);
#line (114, 5) - (114, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(90, td.Seconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedelta90000SecondsOverflowsIntoDays()
            {
#line (119, 5) - (119, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td = new global::Sharpy.Timedelta(seconds: 90000);
#line (120, 5) - (120, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(1, td.Days);
#line (121, 5) - (121, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(3600, td.Seconds);
#line (122, 5) - (122, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(90000.0d, td.TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaNegativeDayIsValid()
            {
#line (126, 5) - (126, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td = new global::Sharpy.Timedelta(days: -1);
#line (127, 5) - (127, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(-1, td.Days);
#line (128, 5) - (128, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(-86400.0d, td.TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaSubtractProducesPositiveResult()
            {
#line (132, 5) - (132, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td = new global::Sharpy.Timedelta(days: 2) - new global::Sharpy.Timedelta(days: 1);
#line (133, 5) - (133, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(1, td.Days);
#line (134, 5) - (134, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(86400.0d, td.TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaComparisonEqual()
            {
#line (138, 5) - (138, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td1 = new global::Sharpy.Timedelta(days: 1);
#line (139, 5) - (139, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td2 = new global::Sharpy.Timedelta(hours: 24);
#line (140, 5) - (140, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(td2, td1);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaComparisonNotEqual()
            {
#line (144, 5) - (144, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td1 = new global::Sharpy.Timedelta(days: 1);
#line (145, 5) - (145, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td2 = new global::Sharpy.Timedelta(days: 2);
#line (146, 5) - (146, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.NotEqual(td2, td1);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaComparisonLessOrEqual()
            {
#line (150, 5) - (150, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td1 = new global::Sharpy.Timedelta(hours: 12);
#line (151, 5) - (151, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td2 = new global::Sharpy.Timedelta(days: 1);
#line (152, 5) - (152, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.True(td1 <= td2);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaComparisonGreaterOrEqual()
            {
#line (156, 5) - (156, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td1 = new global::Sharpy.Timedelta(days: 1);
#line (157, 5) - (157, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td2 = new global::Sharpy.Timedelta(hours: 12);
#line (158, 5) - (158, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.True(td1 >= td2);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaNegativeTotalSecondsIsNegative()
            {
#line (162, 5) - (162, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td = new global::Sharpy.Timedelta(days: -2);
#line (163, 5) - (163, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.True(td.TotalSeconds < 0);
#line (164, 5) - (164, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(-172800.0d, td.TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaZeroDeltaAllComponentsZero()
            {
#line (168, 5) - (168, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                var td = new global::Sharpy.Timedelta();
#line (169, 5) - (169, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, td.Days);
#line (170, 5) - (170, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, td.Seconds);
#line (171, 5) - (171, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0, td.Microseconds);
#line (172, 5) - (172, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_time_tests.spy"
                Xunit.Assert.Equal(0.0d, td.TotalSeconds);
            }
        }
    }
}
