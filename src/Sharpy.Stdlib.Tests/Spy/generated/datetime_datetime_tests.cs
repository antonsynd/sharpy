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
using static Sharpy.Stdlib.Tests.Spy.Datetime.DatetimeDatetimeTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Datetime
    {
        [global::Sharpy.SharpyModule("datetime.datetime_datetime_tests")]
        public static partial class DatetimeDatetimeTests
        {
        }
    }

    public static partial class Datetime
    {
        public partial class DatetimeDatetimeTestsTests
        {
            [Xunit.FactAttribute]
            public void TestDatetimeDateOnlyConstructorTimesAreZero()
            {
#line (18, 5) - (18, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 6, 15);
#line (19, 5) - (19, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(2024, dt.Year);
#line (20, 5) - (20, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(6, dt.Month);
#line (21, 5) - (21, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(15, dt.Day);
#line (22, 5) - (22, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(0, dt.Hour);
#line (23, 5) - (23, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(0, dt.Minute);
#line (24, 5) - (24, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(0, dt.Second);
#line (25, 5) - (25, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(0, dt.Microsecond);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeWithHourAndMinuteSecondDefaultsZero()
            {
#line (29, 5) - (29, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 1, 12, 30);
#line (30, 5) - (30, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(12, dt.Hour);
#line (31, 5) - (31, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(30, dt.Minute);
#line (32, 5) - (32, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(0, dt.Second);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeReplaceOnlyHour()
            {
#line (38, 5) - (38, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 6, 15, 10, 30, 45);
#line (39, 5) - (39, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var result = dt.Replace(hour: 12);
#line (40, 5) - (40, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(2024, result.Year);
#line (41, 5) - (41, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(6, result.Month);
#line (42, 5) - (42, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(15, result.Day);
#line (43, 5) - (43, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(12, result.Hour);
#line (44, 5) - (44, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(30, result.Minute);
#line (45, 5) - (45, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(45, result.Second);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeReplaceYearMonthDay()
            {
#line (49, 5) - (49, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 6, 15, 10, 30, 45);
#line (50, 5) - (50, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var result = dt.Replace(year: 2025, month: 1, day: 1);
#line (51, 5) - (51, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(2025, result.Year);
#line (52, 5) - (52, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(1, result.Month);
#line (53, 5) - (53, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(1, result.Day);
#line (54, 5) - (54, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(10, result.Hour);
#line (55, 5) - (55, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(30, result.Minute);
#line (56, 5) - (56, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(45, result.Second);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeDateComponentHasCorrectFields()
            {
#line (62, 5) - (62, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 11, 22, 8, 15, 0);
#line (63, 5) - (63, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var date = dt.DateComponent;
#line (64, 5) - (64, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(2024, date.Year);
#line (65, 5) - (65, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(11, date.Month);
#line (66, 5) - (66, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(22, date.Day);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeTimeComponentHasCorrectFields()
            {
#line (70, 5) - (70, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 11, 22, 8, 15, 30, 123456);
#line (71, 5) - (71, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var time = dt.TimeComponent;
#line (72, 5) - (72, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(8, time.Hour);
#line (73, 5) - (73, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(15, time.Minute);
#line (74, 5) - (74, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(30, time.Second);
#line (75, 5) - (75, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(123456, time.Microsecond);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeTimestampEpochIsZero()
            {
#line (82, 5) - (82, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var utc = global::Sharpy.Timezone.Utc;
#line (83, 5) - (83, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var epoch = new global::Sharpy.DateTime(1970, 1, 1, 0, 0, 0, tzinfo: utc);
#line (84, 5) - (84, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(0.0d, epoch.Timestamp());
            }

            [Xunit.FactAttribute]
            public void TestDatetimeTimestampIsPositiveForRecentDates()
            {
#line (88, 5) - (88, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var utc = global::Sharpy.Timezone.Utc;
#line (89, 5) - (89, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 1, 0, 0, 0, tzinfo: utc);
#line (90, 5) - (90, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.True(dt.Timestamp() > 0.0d);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeIsoformatWithMicroseconds()
            {
#line (96, 5) - (96, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 6, 15, 10, 30, 45, 123456);
#line (97, 5) - (97, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal("2024-06-15T10:30:45.123456", dt.Isoformat());
            }

            [Xunit.FactAttribute]
            public void TestDatetimeIsoformatWithTimezoneIncludesOffset()
            {
#line (101, 5) - (101, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var utc = global::Sharpy.Timezone.Utc;
#line (102, 5) - (102, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 1, 12, 0, 0, tzinfo: utc);
#line (103, 5) - (103, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Contains("+00:00", dt.Isoformat());
            }

            [Xunit.FactAttribute]
            public void TestDatetimeIsoformatCustomSeparatorSpace()
            {
#line (107, 5) - (107, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 6, 15, 10, 30, 0);
#line (108, 5) - (108, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal("2024-06-15 10:30:00", dt.Isoformat(" "));
            }

            [Xunit.FactAttribute]
            public void TestDatetimeComparisonLessOrEqual()
            {
#line (114, 5) - (114, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt1 = new global::Sharpy.DateTime(2024, 1, 1);
#line (115, 5) - (115, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt2 = new global::Sharpy.DateTime(2024, 1, 1);
#line (116, 5) - (116, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.True(dt1 <= dt2);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeComparisonGreaterOrEqual()
            {
#line (120, 5) - (120, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt1 = new global::Sharpy.DateTime(2024, 6, 1);
#line (121, 5) - (121, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt2 = new global::Sharpy.DateTime(2024, 1, 1);
#line (122, 5) - (122, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.True(dt1 >= dt2);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeAstimezonePositiveOffset()
            {
#line (128, 5) - (128, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var utc = global::Sharpy.Timezone.Utc;
#line (129, 5) - (129, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var ist = new global::Sharpy.Timezone(new global::Sharpy.Timedelta(hours: 5, minutes: 30), "IST");
#line (130, 5) - (130, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dtUtc = new global::Sharpy.DateTime(2024, 1, 15, 0, 0, 0, tzinfo: utc);
#line (131, 5) - (131, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dtIst = dtUtc.Astimezone(ist);
#line (132, 5) - (132, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(5, dtIst.Hour);
#line (133, 5) - (133, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(30, dtIst.Minute);
            }

            [Xunit.FactAttribute]
            public void TestTimezoneUtcStaticPropertyIsUtc()
            {
#line (139, 5) - (139, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var utc = global::Sharpy.Timezone.Utc;
#line (140, 5) - (140, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal("UTC", utc.Tzname());
#line (141, 5) - (141, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(0.0d, utc.Utcoffset().TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestTimezonePositiveOffsetNoName()
            {
#line (145, 5) - (145, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var tz = new global::Sharpy.Timezone(new global::Sharpy.Timedelta(hours: 5));
#line (146, 5) - (146, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(18000.0d, tz.Utcoffset().TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestTimezoneNegativeOffsetWithName()
            {
#line (150, 5) - (150, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var pst = new global::Sharpy.Timezone(new global::Sharpy.Timedelta(hours: -8), "PST");
#line (151, 5) - (151, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal("PST", pst.Tzname());
#line (152, 5) - (152, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Equal(-28800.0d, pst.Utcoffset().TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeWithTimezoneHasCorrectTzinfo()
            {
#line (156, 5) - (156, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var utc = global::Sharpy.Timezone.Utc;
#line (157, 5) - (157, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 15, 12, 0, 0, tzinfo: utc);
#line (158, 5) - (158, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Same(utc, dt.Tzinfo);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeWithoutTimezoneTzinfoIsNull()
            {
#line (162, 5) - (162, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 15, 12, 0, 0);
#line (163, 5) - (163, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_datetime_tests.spy"
                Xunit.Assert.Null(dt.Tzinfo);
            }
        }
    }
}
