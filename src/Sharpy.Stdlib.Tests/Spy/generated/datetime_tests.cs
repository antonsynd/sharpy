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
using datetime = global::Sharpy.Datetime;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Datetime.DatetimeTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Datetime
    {
        [global::Sharpy.SharpyModule("datetime.datetime_tests")]
        public static partial class DatetimeTests
        {
        }
    }

    public static partial class Datetime
    {
        public partial class DatetimeTestsTests
        {
            [Xunit.FactAttribute]
            public void TestDateConstructorSetsYearMonthDay2024115()
            {
#line (33, 5) - (33, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var date = new global::Sharpy.Date(2024, 1, 15);
#line (34, 5) - (34, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2024, date.Year);
#line (35, 5) - (35, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1, date.Month);
#line (36, 5) - (36, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(15, date.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateConstructorSetsYearMonthDay20001231()
            {
#line (40, 5) - (40, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var date = new global::Sharpy.Date(2000, 12, 31);
#line (41, 5) - (41, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2000, date.Year);
#line (42, 5) - (42, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(12, date.Month);
#line (43, 5) - (43, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(31, date.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateConstructorSetsYearMonthDay197011()
            {
#line (47, 5) - (47, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var date = new global::Sharpy.Date(1970, 1, 1);
#line (48, 5) - (48, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1970, date.Year);
#line (49, 5) - (49, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1, date.Month);
#line (50, 5) - (50, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1, date.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateToStringReturnsIsoFormat()
            {
#line (54, 5) - (54, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var date = new global::Sharpy.Date(2024, 1, 15);
#line (55, 5) - (55, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("2024-01-15", global::Sharpy.Builtins.Str(date));
            }

            [Xunit.FactAttribute]
            public void TestDateTodayReturnsCurrentDate()
            {
#line (59, 5) - (59, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var today = global::Sharpy.Date.Today();
#line (60, 5) - (60, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(today.Year > 2020);
#line (61, 5) - (61, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(1 <= (today.Month is var __cmp_0 ? __cmp_0 : __cmp_0) && __cmp_0 <= 12);
#line (62, 5) - (62, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(1 <= (today.Day is var __cmp_1 ? __cmp_1 : __cmp_1) && __cmp_1 <= 31);
            }

            [Xunit.FactAttribute]
            public void TestDateInvalidDateThrowsArgumentOutOfRange()
            {
#line (66, 5) - (71, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Throws<ArgumentOutOfRangeException>((global::System.Action)(() =>
                {
#line (67, 9) - (67, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                    new global::Sharpy.Date(2024, 13, 1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestTimeConstructorSetsComponents143000()
            {
#line (73, 5) - (73, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var time = new global::Sharpy.Time(14, 30, 0, 0);
#line (74, 5) - (74, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(14, time.Hour);
#line (75, 5) - (75, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(30, time.Minute);
#line (76, 5) - (76, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, time.Second);
#line (77, 5) - (77, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, time.Microsecond);
            }

            [Xunit.FactAttribute]
            public void TestTimeConstructorSetsComponents0000()
            {
#line (81, 5) - (81, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var time = new global::Sharpy.Time(0, 0, 0, 0);
#line (82, 5) - (82, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, time.Hour);
#line (83, 5) - (83, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, time.Minute);
#line (84, 5) - (84, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, time.Second);
#line (85, 5) - (85, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, time.Microsecond);
            }

            [Xunit.FactAttribute]
            public void TestTimeConstructorSetsComponents235959999999()
            {
#line (89, 5) - (89, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var time = new global::Sharpy.Time(23, 59, 59, 999999);
#line (90, 5) - (90, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(23, time.Hour);
#line (91, 5) - (91, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(59, time.Minute);
#line (92, 5) - (92, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(59, time.Second);
#line (93, 5) - (93, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(999999, time.Microsecond);
            }

            [Xunit.FactAttribute]
            public void TestTimeDefaultParametersAreZero()
            {
#line (97, 5) - (97, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var time = new global::Sharpy.Time();
#line (98, 5) - (98, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, time.Hour);
#line (99, 5) - (99, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, time.Minute);
#line (100, 5) - (100, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, time.Second);
#line (101, 5) - (101, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, time.Microsecond);
            }

            [Xunit.FactAttribute]
            public void TestTimeToStringReturnsFormattedString()
            {
#line (105, 5) - (105, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var time = new global::Sharpy.Time(14, 30, 0, 0);
#line (106, 5) - (106, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("14:30:00.000000", global::Sharpy.Builtins.Str(time));
            }

            [Xunit.FactAttribute]
            public void TestDatetimeConstructorSetsAllComponents()
            {
#line (112, 5) - (112, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 6, 15, 10, 30, 45, 123456);
#line (113, 5) - (113, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2024, dt.Year);
#line (114, 5) - (114, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(6, dt.Month);
#line (115, 5) - (115, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(15, dt.Day);
#line (116, 5) - (116, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(10, dt.Hour);
#line (117, 5) - (117, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(30, dt.Minute);
#line (118, 5) - (118, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(45, dt.Second);
#line (119, 5) - (119, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(123456, dt.Microsecond);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeDefaultTimeParametersAreZero()
            {
#line (123, 5) - (123, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 1);
#line (124, 5) - (124, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, dt.Hour);
#line (125, 5) - (125, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, dt.Minute);
#line (126, 5) - (126, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, dt.Second);
#line (127, 5) - (127, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, dt.Microsecond);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeNowReturnsCurrentDatetime()
            {
#line (131, 5) - (131, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var now = global::Sharpy.DateTime.Now();
#line (132, 5) - (132, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(now.Year > 2020);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeUtcnowReturnsUtcDatetime()
            {
#line (136, 5) - (136, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var utcNow = global::Sharpy.DateTime.Utcnow();
#line (137, 5) - (137, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(utcNow.Year > 2020);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeDateComponentReturnsDate()
            {
#line (141, 5) - (141, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 3, 20, 14, 30, 0);
#line (142, 5) - (142, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var date = dt.DateComponent;
#line (143, 5) - (143, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2024, date.Year);
#line (144, 5) - (144, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(3, date.Month);
#line (145, 5) - (145, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(20, date.Day);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeTimeComponentReturnsTime()
            {
#line (149, 5) - (149, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 3, 20, 14, 30, 45);
#line (150, 5) - (150, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var time = dt.TimeComponent;
#line (151, 5) - (151, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(14, time.Hour);
#line (152, 5) - (152, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(30, time.Minute);
#line (153, 5) - (153, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(45, time.Second);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeCombineMergesDateAndTime()
            {
#line (157, 5) - (157, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var date = new global::Sharpy.Date(2024, 6, 15);
#line (158, 5) - (158, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var time = new global::Sharpy.Time(10, 30, 45);
#line (159, 5) - (159, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = global::Sharpy.DateTime.Combine(date, time);
#line (160, 5) - (160, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2024, dt.Year);
#line (161, 5) - (161, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(6, dt.Month);
#line (162, 5) - (162, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(15, dt.Day);
#line (163, 5) - (163, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(10, dt.Hour);
#line (164, 5) - (164, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(30, dt.Minute);
#line (165, 5) - (165, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(45, dt.Second);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaDaysReturnsCorrectValue()
            {
#line (171, 5) - (171, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta(days: 5);
#line (172, 5) - (172, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(5, td.Days);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaSecondsReturnsSecondsComponent()
            {
#line (176, 5) - (176, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta(seconds: 30);
#line (177, 5) - (177, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(30, td.Seconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaMicrosecondsReturnsCorrectValue()
            {
#line (181, 5) - (181, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta(microseconds: 500);
#line (182, 5) - (182, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(500, td.Microseconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaWeeksConvertsToDays()
            {
#line (186, 5) - (186, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta(weeks: 2);
#line (187, 5) - (187, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(14, td.Days);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaTotalSecondsReturnsCombinedSeconds()
            {
#line (191, 5) - (191, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta(days: 1, hours: 1, minutes: 1, seconds: 1);
#line (193, 5) - (193, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(90061.0d, td.TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaDefaultParametersAreZero()
            {
#line (197, 5) - (197, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta();
#line (198, 5) - (198, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, td.Days);
#line (199, 5) - (199, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, td.Seconds);
#line (200, 5) - (200, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, td.Microseconds);
#line (201, 5) - (201, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0.0d, td.TotalSeconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaSecondsReturnsRemainingSecondsAfterDays()
            {
#line (206, 5) - (206, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta(days: 1, hours: 2, minutes: 3, seconds: 4);
#line (207, 5) - (207, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(7384, td.Seconds);
            }

            [Xunit.FactAttribute]
            public void TestDatetimePlusTimedeltaReturnsNewDatetime()
            {
#line (213, 5) - (213, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 1);
#line (214, 5) - (214, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta(days: 1);
#line (215, 5) - (215, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = dt + td;
#line (216, 5) - (216, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2024, result.Year);
#line (217, 5) - (217, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1, result.Month);
#line (218, 5) - (218, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2, result.Day);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeMinusTimedeltaReturnsNewDatetime()
            {
#line (222, 5) - (222, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 5);
#line (223, 5) - (223, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta(days: 3);
#line (224, 5) - (224, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = dt - td;
#line (225, 5) - (225, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2, result.Day);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeMinusDatetimeReturnsTimedelta()
            {
#line (229, 5) - (229, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt1 = new global::Sharpy.DateTime(2024, 1, 5);
#line (230, 5) - (230, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt2 = new global::Sharpy.DateTime(2024, 1, 1);
#line (231, 5) - (231, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = dt1 - dt2;
#line (232, 5) - (232, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(4, result.Days);
#line (233, 5) - (233, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, result.Seconds);
            }

            [Xunit.FactAttribute]
            public void TestDatePlusTimedeltaReturnsNewDate()
            {
#line (239, 5) - (239, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var date = new global::Sharpy.Date(2024, 1, 1);
#line (240, 5) - (240, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta(days: 10);
#line (241, 5) - (241, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = date + td;
#line (242, 5) - (242, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(11, result.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateMinusTimedeltaReturnsNewDate()
            {
#line (246, 5) - (246, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var date = new global::Sharpy.Date(2024, 1, 15);
#line (247, 5) - (247, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta(days: 5);
#line (248, 5) - (248, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = date - td;
#line (249, 5) - (249, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(10, result.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateMinusDateReturnsTimedelta()
            {
#line (253, 5) - (253, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var d1 = new global::Sharpy.Date(2024, 1, 15);
#line (254, 5) - (254, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var d2 = new global::Sharpy.Date(2024, 1, 1);
#line (255, 5) - (255, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = d1 - d2;
#line (256, 5) - (256, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(14, result.Days);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaPlusTimedelta()
            {
#line (262, 5) - (262, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td1 = new global::Sharpy.Timedelta(days: 1);
#line (263, 5) - (263, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td2 = new global::Sharpy.Timedelta(hours: 12);
#line (264, 5) - (264, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = td1 + td2;
#line (265, 5) - (265, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1, result.Days);
#line (266, 5) - (266, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(43200, result.Seconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaMinusTimedelta()
            {
#line (270, 5) - (270, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = new global::Sharpy.Timedelta(days: 5) - new global::Sharpy.Timedelta(days: 2);
#line (271, 5) - (271, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(3, result.Days);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaMultiplyInt()
            {
#line (275, 5) - (275, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = new global::Sharpy.Timedelta(days: 1) * 3;
#line (276, 5) - (276, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(3, result.Days);
            }

            [Xunit.FactAttribute]
            public void TestIntMultiplyTimedelta()
            {
#line (280, 5) - (280, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = 3 * new global::Sharpy.Timedelta(days: 1);
#line (281, 5) - (281, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(3, result.Days);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaNegate()
            {
#line (285, 5) - (285, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta(days: 1);
#line (286, 5) - (286, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = -td;
#line (287, 5) - (287, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(-1, result.Days);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaDivideInt()
            {
#line (291, 5) - (291, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = new global::Sharpy.Timedelta(days: 10) / 3;
#line (292, 5) - (292, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(3, result.Days);
#line (293, 5) - (293, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(28800, result.Seconds);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaAbs()
            {
#line (297, 5) - (297, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td = new global::Sharpy.Timedelta(days: -5);
#line (298, 5) - (298, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = td.Abs();
#line (299, 5) - (299, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(5, result.Days);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeComparisonOperators()
            {
#line (305, 5) - (305, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt1 = new global::Sharpy.DateTime(2024, 1, 1);
#line (306, 5) - (306, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt2 = new global::Sharpy.DateTime(2024, 1, 2);
#line (307, 5) - (307, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt3 = new global::Sharpy.DateTime(2024, 1, 1);
#line (308, 5) - (308, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(dt1 < dt2);
#line (309, 5) - (309, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(dt2 > dt1);
#line (310, 5) - (310, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(dt3, dt1);
#line (311, 5) - (311, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.NotEqual(dt2, dt1);
#line (312, 5) - (312, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(dt1 <= dt3);
#line (313, 5) - (313, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(dt2 >= dt1);
            }

            [Xunit.FactAttribute]
            public void TestDateComparisonOperators()
            {
#line (317, 5) - (317, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var d1 = new global::Sharpy.Date(2024, 1, 1);
#line (318, 5) - (318, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var d2 = new global::Sharpy.Date(2024, 1, 2);
#line (319, 5) - (319, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(d1 < d2);
#line (320, 5) - (320, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Date(2024, 1, 1), d1);
            }

            [Xunit.FactAttribute]
            public void TestTimeComparisonOperators()
            {
#line (324, 5) - (324, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var t1 = new global::Sharpy.Time(10, 0);
#line (325, 5) - (325, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var t2 = new global::Sharpy.Time(12, 0);
#line (326, 5) - (326, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(t1 < t2);
#line (327, 5) - (327, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(t2 > t1);
            }

            [Xunit.FactAttribute]
            public void TestTimedeltaComparisonOperators()
            {
#line (331, 5) - (331, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td1 = new global::Sharpy.Timedelta(days: 1);
#line (332, 5) - (332, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var td2 = new global::Sharpy.Timedelta(hours: 12);
#line (333, 5) - (333, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.True(td1 > td2);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeStrftimeBasicFormat()
            {
#line (339, 5) - (339, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 15, 14, 30, 45);
#line (340, 5) - (340, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("2024-01-15", dt.Strftime("%Y-%m-%d"));
            }

            [Xunit.FactAttribute]
            public void TestDatetimeStrftimeDayNames()
            {
#line (344, 5) - (344, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 15);
#line (345, 5) - (345, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("Monday", dt.Strftime("%A"));
            }

            [Xunit.FactAttribute]
            public void TestDatetimeStrftimePercentLiteral()
            {
#line (349, 5) - (349, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 15);
#line (350, 5) - (350, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("100%", dt.Strftime("100%%"));
            }

            [Xunit.FactAttribute]
            public void TestDateStrftime()
            {
#line (354, 5) - (354, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var d = new global::Sharpy.Date(2024, 1, 15);
#line (355, 5) - (355, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("2024-01-15", d.Strftime("%Y-%m-%d"));
            }

            [Xunit.FactAttribute]
            public void TestDatetimeStrptimeParsesDate()
            {
#line (361, 5) - (361, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = global::Sharpy.DateTime.Strptime("2024-01-15", "%Y-%m-%d");
#line (362, 5) - (362, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2024, dt.Year);
#line (363, 5) - (363, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1, dt.Month);
#line (364, 5) - (364, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(15, dt.Day);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeWeekdayMondayIsZero()
            {
#line (371, 5) - (371, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 15);
#line (372, 5) - (372, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, dt.Weekday());
            }

            [Xunit.FactAttribute]
            public void TestDatetimeIsoweekdayMondayIsOne()
            {
#line (376, 5) - (376, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 15);
#line (377, 5) - (377, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1, dt.Isoweekday());
            }

            [Xunit.FactAttribute]
            public void TestDatetimeIsoformatDefault()
            {
#line (381, 5) - (381, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 15);
#line (382, 5) - (382, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("2024-01-15T00:00:00", dt.Isoformat());
            }

            [Xunit.FactAttribute]
            public void TestDatetimeIsoformatCustomSep()
            {
#line (386, 5) - (386, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 15);
#line (387, 5) - (387, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("2024-01-15 00:00:00", dt.Isoformat(" "));
            }

            [Xunit.FactAttribute]
            public void TestDatetimeReplace()
            {
#line (391, 5) - (391, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 15, 10, 30);
#line (392, 5) - (392, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = dt.Replace(year: 2025);
#line (393, 5) - (393, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2025, result.Year);
#line (394, 5) - (394, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1, result.Month);
#line (395, 5) - (395, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(15, result.Day);
#line (396, 5) - (396, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(10, result.Hour);
#line (397, 5) - (397, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(30, result.Minute);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeFromisoformat()
            {
#line (401, 5) - (401, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = global::Sharpy.DateTime.Fromisoformat("2024-01-15T14:30:00");
#line (402, 5) - (402, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2024, dt.Year);
#line (403, 5) - (403, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(14, dt.Hour);
#line (404, 5) - (404, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(30, dt.Minute);
            }

            [Xunit.FactAttribute]
            public void TestDateWeekdayMondayIsZero()
            {
#line (408, 5) - (408, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var d = new global::Sharpy.Date(2024, 1, 15);
#line (409, 5) - (409, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, d.Weekday());
            }

            [Xunit.FactAttribute]
            public void TestDateIsoformat()
            {
#line (413, 5) - (413, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var d = new global::Sharpy.Date(2024, 1, 15);
#line (414, 5) - (414, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("2024-01-15", d.Isoformat());
            }

            [Xunit.FactAttribute]
            public void TestDateToordinal()
            {
#line (418, 5) - (418, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var d = new global::Sharpy.Date(2024, 1, 15);
#line (419, 5) - (419, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(738900, d.Toordinal());
            }

            [Xunit.FactAttribute]
            public void TestDateFromordinal()
            {
#line (423, 5) - (423, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var d = global::Sharpy.Date.Fromordinal(738900);
#line (424, 5) - (424, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2024, d.Year);
#line (425, 5) - (425, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1, d.Month);
#line (426, 5) - (426, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(15, d.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateFromisoformat()
            {
#line (430, 5) - (430, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var d = global::Sharpy.Date.Fromisoformat("2024-01-15");
#line (431, 5) - (431, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2024, d.Year);
#line (432, 5) - (432, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1, d.Month);
#line (433, 5) - (433, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(15, d.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateReplace()
            {
#line (437, 5) - (437, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var d = new global::Sharpy.Date(2024, 1, 15);
#line (438, 5) - (438, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = d.Replace(month: 6);
#line (439, 5) - (439, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2024, result.Year);
#line (440, 5) - (440, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(6, result.Month);
#line (441, 5) - (441, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(15, result.Day);
            }

            [Xunit.FactAttribute]
            public void TestTimezoneUtcHasZeroOffset()
            {
#line (447, 5) - (447, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var utc = global::Sharpy.Timezone.Utc;
#line (448, 5) - (448, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, utc.Utcoffset().Days);
#line (449, 5) - (449, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, utc.Utcoffset().Seconds);
#line (450, 5) - (450, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("UTC", utc.Tzname());
            }

            [Xunit.FactAttribute]
            public void TestTimezoneCustomHasCorrectOffset()
            {
#line (454, 5) - (454, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var est = new global::Sharpy.Timezone(new global::Sharpy.Timedelta(hours: -5), "EST");
#line (455, 5) - (455, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(-18000.0d, est.Utcoffset().TotalSeconds);
#line (456, 5) - (456, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("EST", est.Tzname());
            }

            [Xunit.FactAttribute]
            public void TestDatetimeAstimezoneConvertsCorrectly()
            {
#line (460, 5) - (460, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var utc = global::Sharpy.Timezone.Utc;
#line (461, 5) - (461, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var est = new global::Sharpy.Timezone(new global::Sharpy.Timedelta(hours: -5), "EST");
#line (462, 5) - (462, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dtUtc = new global::Sharpy.DateTime(2024, 1, 15, 12, 0, 0, tzinfo: utc);
#line (463, 5) - (463, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dtEst = dtUtc.Astimezone(est);
#line (464, 5) - (464, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(7, dtEst.Hour);
#line (465, 5) - (465, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Same(est, dtEst.Tzinfo);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeWithTimezoneToStringIncludesOffset()
            {
#line (469, 5) - (469, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var utc = global::Sharpy.Timezone.Utc;
#line (470, 5) - (470, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 15, 12, 0, 0, tzinfo: utc);
#line (471, 5) - (471, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Contains("+00:00", global::Sharpy.Builtins.Str(dt));
            }

            [Xunit.FactAttribute]
            public void TestDatetimeLeapYearFeb29()
            {
#line (477, 5) - (477, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 2, 29);
#line (478, 5) - (478, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = dt + new global::Sharpy.Timedelta(days: 1);
#line (479, 5) - (479, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(3, result.Month);
#line (480, 5) - (480, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1, result.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateCrossMonthBoundary()
            {
#line (484, 5) - (484, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var d = new global::Sharpy.Date(2024, 1, 31);
#line (485, 5) - (485, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = d + new global::Sharpy.Timedelta(days: 1);
#line (486, 5) - (486, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(2, result.Month);
#line (487, 5) - (487, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(1, result.Day);
            }

            [Xunit.FactAttribute]
            public void TestDatetimeMidnightCrossing()
            {
#line (491, 5) - (491, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var dt = new global::Sharpy.DateTime(2024, 1, 15, 23, 30, 0);
#line (492, 5) - (492, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var result = dt + new global::Sharpy.Timedelta(hours: 1);
#line (493, 5) - (493, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(16, result.Day);
#line (494, 5) - (494, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(0, result.Hour);
#line (495, 5) - (495, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal(30, result.Minute);
            }

            [Xunit.FactAttribute]
            public void TestTimeIsoformatWithoutMicroseconds()
            {
#line (499, 5) - (499, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var t = new global::Sharpy.Time(14, 30, 0);
#line (500, 5) - (500, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("14:30:00", t.Isoformat());
            }

            [Xunit.FactAttribute]
            public void TestTimeIsoformatWithMicroseconds()
            {
#line (504, 5) - (504, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                var t = new global::Sharpy.Time(14, 30, 0, 123456);
#line (505, 5) - (505, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                Xunit.Assert.Equal("14:30:00.123456", t.Isoformat());
            }
        }
    }
}
