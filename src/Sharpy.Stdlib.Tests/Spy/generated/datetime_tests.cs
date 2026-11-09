// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

#pragma warning disable xUnit2009, xUnit2017

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using datetime = global::Sharpy.Datetime;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Datetime.DatetimeTests
{
    [global::Sharpy.SharpyModule("datetime.datetime_tests")]
    public static partial class DatetimeTestsModule
    {
    }

    public partial class DatetimeTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestDateConstructorSetsYearMonthDay2024115()
        {
#line (31, 5) - (31, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var date = new global::Sharpy.Date(2024, 1, 15);
#line (32, 5) - (32, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2024, date.Year);
#line (33, 5) - (33, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1, date.Month);
#line (34, 5) - (34, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(15, date.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateConstructorSetsYearMonthDay20001231()
        {
#line (38, 5) - (38, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var date = new global::Sharpy.Date(2000, 12, 31);
#line (39, 5) - (39, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2000, date.Year);
#line (40, 5) - (40, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(12, date.Month);
#line (41, 5) - (41, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(31, date.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateConstructorSetsYearMonthDay197011()
        {
#line (45, 5) - (45, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var date = new global::Sharpy.Date(1970, 1, 1);
#line (46, 5) - (46, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1970, date.Year);
#line (47, 5) - (47, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1, date.Month);
#line (48, 5) - (48, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1, date.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateToStringReturnsIsoFormat()
        {
#line (52, 5) - (52, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var date = new global::Sharpy.Date(2024, 1, 15);
#line (53, 5) - (53, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("2024-01-15", global::Sharpy.Builtins.Str(date));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateTodayReturnsCurrentDate()
        {
#line (57, 5) - (57, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var today = global::Sharpy.Date.Today();
#line (58, 5) - (58, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(today.Year > 2020);
#line (59, 5) - (59, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(1 <= (today.Month is var __cmp_0 ? __cmp_0 : __cmp_0) && __cmp_0 <= 12);
#line (60, 5) - (60, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(1 <= (today.Day is var __cmp_1 ? __cmp_1 : __cmp_1) && __cmp_1 <= 31);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateInvalidDateRaisesValueError()
        {
#line (64, 5) - (65, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (65, 9) - (65, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.Date(2024, 13, 1);
#line hidden
            }
            catch (ValueError)
            {
                __raised_2 = true;
            }

            if (!__raised_2)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestDateFeb29NonLeapRaisesValueError()
        {
#line (71, 5) - (71, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (72, 5) - (75, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (73, 9) - (73, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.Date(2023, 2, 29);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (75, 9) - (75, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (76, 5) - (76, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("day is out of range for month", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateMonth13RaisesValueError()
        {
#line (80, 5) - (80, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (81, 5) - (84, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (82, 9) - (82, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.Date(2023, 13, 1);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (84, 9) - (84, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (85, 5) - (85, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("month must be in 1..12", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateDay0RaisesValueError()
        {
#line (89, 5) - (89, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (90, 5) - (93, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (91, 9) - (91, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.Date(2023, 1, 0);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (93, 9) - (93, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (94, 5) - (94, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("day is out of range for month", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateYear0RaisesValueError()
        {
#line (98, 5) - (98, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (99, 5) - (102, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (100, 9) - (100, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.Date(0, 1, 1);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (102, 9) - (102, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (103, 5) - (103, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("year 0 is out of range", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateYear10000RaisesValueError()
        {
#line (107, 5) - (107, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (108, 5) - (111, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (109, 9) - (109, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.Date(10000, 1, 1);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (111, 9) - (111, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (112, 5) - (112, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("year 10000 is out of range", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateFromordinal0RaisesValueError()
        {
#line (116, 5) - (116, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (117, 5) - (120, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (118, 9) - (118, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                global::Sharpy.Date.Fromordinal(0);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (120, 9) - (120, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (121, 5) - (121, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("ordinal must be >= 1", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateReplaceMonth13RaisesValueError()
        {
#line (125, 5) - (125, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (126, 5) - (129, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (127, 9) - (127, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.Date(2023, 1, 31).Replace(month: 13);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (129, 9) - (129, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (130, 5) - (130, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("month must be in 1..12", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeHour25RaisesValueError()
        {
#line (134, 5) - (134, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (135, 5) - (138, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (136, 9) - (136, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.Time(25, 0);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (138, 9) - (138, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (139, 5) - (139, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("hour must be in 0..23", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeHourNegativeRaisesValueError()
        {
#line (143, 5) - (143, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (144, 5) - (147, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (145, 9) - (145, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.Time(-1);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (147, 9) - (147, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (148, 5) - (148, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("hour must be in 0..23", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeMinute60RaisesValueError()
        {
#line (152, 5) - (152, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (153, 5) - (156, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (154, 9) - (154, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.Time(0, 60);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (156, 9) - (156, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (157, 5) - (157, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("minute must be in 0..59", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeSecond60RaisesValueError()
        {
#line (161, 5) - (161, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (162, 5) - (165, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (163, 9) - (163, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.Time(0, 0, 60);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (165, 9) - (165, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (166, 5) - (166, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("second must be in 0..59", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeMicrosecond1000000RaisesValueError()
        {
#line (170, 5) - (170, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (171, 5) - (174, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (172, 9) - (172, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.Time(0, 0, 0, 1000000);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (174, 9) - (174, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (175, 5) - (175, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("microsecond must be in 0..999999", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeFeb29NonLeapRaisesValueError()
        {
#line (179, 5) - (179, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (180, 5) - (183, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (181, 9) - (181, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.DateTime(2023, 2, 29);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (183, 9) - (183, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (184, 5) - (184, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("day is out of range for month", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeHour24RaisesValueError()
        {
#line (188, 5) - (188, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (189, 5) - (192, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (190, 9) - (190, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.DateTime(2023, 1, 1, 24);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (192, 9) - (192, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (193, 5) - (193, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("hour must be in 0..23", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeReplaceMonth13RaisesValueError()
        {
#line (197, 5) - (197, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var text = "";
#line (198, 5) - (201, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            try
#line hidden
            {
#line (199, 9) - (199, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                new global::Sharpy.DateTime(2023, 1, 1).Replace(month: 13);
#line hidden
            }
            catch (global::Sharpy.ValueError e)
            {
#line (201, 9) - (201, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
                text = global::Sharpy.Builtins.Str(e);
#line hidden
            }

#line (202, 5) - (202, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("month must be in 1..12", text);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeConstructorSetsComponents143000()
        {
#line (208, 5) - (208, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var time = new global::Sharpy.Time(14, 30, 0, 0);
#line (209, 5) - (209, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(14, time.Hour);
#line (210, 5) - (210, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(30, time.Minute);
#line (211, 5) - (211, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, time.Second);
#line (212, 5) - (212, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, time.Microsecond);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeConstructorSetsComponents0000()
        {
#line (216, 5) - (216, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var time = new global::Sharpy.Time(0, 0, 0, 0);
#line (217, 5) - (217, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, time.Hour);
#line (218, 5) - (218, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, time.Minute);
#line (219, 5) - (219, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, time.Second);
#line (220, 5) - (220, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, time.Microsecond);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeConstructorSetsComponents235959999999()
        {
#line (224, 5) - (224, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var time = new global::Sharpy.Time(23, 59, 59, 999999);
#line (225, 5) - (225, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(23, time.Hour);
#line (226, 5) - (226, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(59, time.Minute);
#line (227, 5) - (227, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(59, time.Second);
#line (228, 5) - (228, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(999999, time.Microsecond);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeDefaultParametersAreZero()
        {
#line (232, 5) - (232, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var time = new global::Sharpy.Time();
#line (233, 5) - (233, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, time.Hour);
#line (234, 5) - (234, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, time.Minute);
#line (235, 5) - (235, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, time.Second);
#line (236, 5) - (236, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, time.Microsecond);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeToStringReturnsFormattedString()
        {
#line (240, 5) - (240, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var time = new global::Sharpy.Time(14, 30, 0, 0);
#line (241, 5) - (241, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("14:30:00", global::Sharpy.Builtins.Str(time));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeConstructorSetsAllComponents()
        {
#line (247, 5) - (247, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 6, 15, 10, 30, 45, 123456);
#line (248, 5) - (248, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2024, dt.Year);
#line (249, 5) - (249, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(6, dt.Month);
#line (250, 5) - (250, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(15, dt.Day);
#line (251, 5) - (251, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(10, dt.Hour);
#line (252, 5) - (252, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(30, dt.Minute);
#line (253, 5) - (253, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(45, dt.Second);
#line (254, 5) - (254, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(123456, dt.Microsecond);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeDefaultTimeParametersAreZero()
        {
#line (258, 5) - (258, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 1);
#line (259, 5) - (259, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, dt.Hour);
#line (260, 5) - (260, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, dt.Minute);
#line (261, 5) - (261, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, dt.Second);
#line (262, 5) - (262, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, dt.Microsecond);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeNowReturnsCurrentDatetime()
        {
#line (266, 5) - (266, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var now = global::Sharpy.DateTime.Now();
#line (267, 5) - (267, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(now.Year > 2020);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeUtcnowReturnsUtcDatetime()
        {
#line (271, 5) - (271, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var utcNow = global::Sharpy.DateTime.Utcnow();
#line (272, 5) - (272, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(utcNow.Year > 2020);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeDateComponentReturnsDate()
        {
#line (276, 5) - (276, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 3, 20, 14, 30, 0);
#line (277, 5) - (277, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var date = dt.DateComponent;
#line (278, 5) - (278, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2024, date.Year);
#line (279, 5) - (279, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(3, date.Month);
#line (280, 5) - (280, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(20, date.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeTimeComponentReturnsTime()
        {
#line (284, 5) - (284, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 3, 20, 14, 30, 45);
#line (285, 5) - (285, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var time = dt.TimeComponent;
#line (286, 5) - (286, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(14, time.Hour);
#line (287, 5) - (287, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(30, time.Minute);
#line (288, 5) - (288, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(45, time.Second);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeCombineMergesDateAndTime()
        {
#line (292, 5) - (292, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var date = new global::Sharpy.Date(2024, 6, 15);
#line (293, 5) - (293, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var time = new global::Sharpy.Time(10, 30, 45);
#line (294, 5) - (294, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = global::Sharpy.DateTime.Combine(date, time);
#line (295, 5) - (295, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2024, dt.Year);
#line (296, 5) - (296, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(6, dt.Month);
#line (297, 5) - (297, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(15, dt.Day);
#line (298, 5) - (298, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(10, dt.Hour);
#line (299, 5) - (299, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(30, dt.Minute);
#line (300, 5) - (300, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(45, dt.Second);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaDaysReturnsCorrectValue()
        {
#line (306, 5) - (306, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta(days: 5);
#line (307, 5) - (307, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(5, td.Days);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaSecondsReturnsSecondsComponent()
        {
#line (311, 5) - (311, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta(seconds: 30);
#line (312, 5) - (312, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(30, td.Seconds);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaMicrosecondsReturnsCorrectValue()
        {
#line (316, 5) - (316, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta(microseconds: 500);
#line (317, 5) - (317, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(500, td.Microseconds);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaWeeksConvertsToDays()
        {
#line (321, 5) - (321, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta(weeks: 2);
#line (322, 5) - (322, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(14, td.Days);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaTotalSecondsReturnsCombinedSeconds()
        {
#line (326, 5) - (326, 67) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta(days: 1, hours: 1, minutes: 1, seconds: 1);
#line (328, 5) - (328, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(90061.0d, td.TotalSeconds);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaDefaultParametersAreZero()
        {
#line (332, 5) - (332, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta();
#line (333, 5) - (333, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, td.Days);
#line (334, 5) - (334, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, td.Seconds);
#line (335, 5) - (335, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, td.Microseconds);
#line (336, 5) - (336, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0.0d, td.TotalSeconds);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaSecondsReturnsRemainingSecondsAfterDays()
        {
#line (341, 5) - (341, 67) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta(days: 1, hours: 2, minutes: 3, seconds: 4);
#line (342, 5) - (342, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(7384, td.Seconds);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimePlusTimedeltaReturnsNewDatetime()
        {
#line (348, 5) - (348, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 1);
#line (349, 5) - (349, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta(days: 1);
#line (350, 5) - (350, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = dt + td;
#line (351, 5) - (351, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2024, result.Year);
#line (352, 5) - (352, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1, result.Month);
#line (353, 5) - (353, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2, result.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeMinusTimedeltaReturnsNewDatetime()
        {
#line (357, 5) - (357, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 5);
#line (358, 5) - (358, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta(days: 3);
#line (359, 5) - (359, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = dt - td;
#line (360, 5) - (360, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2, result.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeMinusDatetimeReturnsTimedelta()
        {
#line (364, 5) - (364, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt1 = new global::Sharpy.DateTime(2024, 1, 5);
#line (365, 5) - (365, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt2 = new global::Sharpy.DateTime(2024, 1, 1);
#line (366, 5) - (366, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = dt1 - dt2;
#line (367, 5) - (367, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(4, result.Days);
#line (368, 5) - (368, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, result.Seconds);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatePlusTimedeltaReturnsNewDate()
        {
#line (374, 5) - (374, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var date = new global::Sharpy.Date(2024, 1, 1);
#line (375, 5) - (375, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta(days: 10);
#line (376, 5) - (376, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = date + td;
#line (377, 5) - (377, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(11, result.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateMinusTimedeltaReturnsNewDate()
        {
#line (381, 5) - (381, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var date = new global::Sharpy.Date(2024, 1, 15);
#line (382, 5) - (382, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta(days: 5);
#line (383, 5) - (383, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = date - td;
#line (384, 5) - (384, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(10, result.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateMinusDateReturnsTimedelta()
        {
#line (388, 5) - (388, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var d1 = new global::Sharpy.Date(2024, 1, 15);
#line (389, 5) - (389, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var d2 = new global::Sharpy.Date(2024, 1, 1);
#line (390, 5) - (390, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = d1 - d2;
#line (391, 5) - (391, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(14, result.Days);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaPlusTimedelta()
        {
#line (397, 5) - (397, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td1 = new global::Sharpy.Timedelta(days: 1);
#line (398, 5) - (398, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td2 = new global::Sharpy.Timedelta(hours: 12);
#line (399, 5) - (399, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = td1 + td2;
#line (400, 5) - (400, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1, result.Days);
#line (401, 5) - (401, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(43200, result.Seconds);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaMinusTimedelta()
        {
#line (405, 5) - (405, 69) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = new global::Sharpy.Timedelta(days: 5) - new global::Sharpy.Timedelta(days: 2);
#line (406, 5) - (406, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(3, result.Days);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaMultiplyInt()
        {
#line (410, 5) - (410, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = new global::Sharpy.Timedelta(days: 1) * 3;
#line (411, 5) - (411, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(3, result.Days);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIntMultiplyTimedelta()
        {
#line (415, 5) - (415, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = 3 * new global::Sharpy.Timedelta(days: 1);
#line (416, 5) - (416, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(3, result.Days);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaNegate()
        {
#line (420, 5) - (420, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta(days: 1);
#line (421, 5) - (421, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = -td;
#line (422, 5) - (422, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(-1, result.Days);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaDivideInt()
        {
#line (426, 5) - (426, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = new global::Sharpy.Timedelta(days: 10) / 3;
#line (427, 5) - (427, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(3, result.Days);
#line (428, 5) - (428, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(28800, result.Seconds);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaAbs()
        {
#line (432, 5) - (432, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td = new global::Sharpy.Timedelta(days: -5);
#line (433, 5) - (433, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = td.Abs();
#line (434, 5) - (434, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(5, result.Days);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeComparisonOperators()
        {
#line (440, 5) - (440, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt1 = new global::Sharpy.DateTime(2024, 1, 1);
#line (441, 5) - (441, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt2 = new global::Sharpy.DateTime(2024, 1, 2);
#line (442, 5) - (442, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt3 = new global::Sharpy.DateTime(2024, 1, 1);
#line (443, 5) - (443, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(dt1 < dt2);
#line (444, 5) - (444, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(dt2 > dt1);
#line (445, 5) - (445, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(dt3, dt1);
#line (446, 5) - (446, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.NotEqual(dt2, dt1);
#line (447, 5) - (447, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(dt1 <= dt3);
#line (448, 5) - (448, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(dt2 >= dt1);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateComparisonOperators()
        {
#line (452, 5) - (452, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var d1 = new global::Sharpy.Date(2024, 1, 1);
#line (453, 5) - (453, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var d2 = new global::Sharpy.Date(2024, 1, 2);
#line (454, 5) - (454, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(d1 < d2);
#line (455, 5) - (455, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(new global::Sharpy.Date(2024, 1, 1), d1);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeComparisonOperators()
        {
#line (459, 5) - (459, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var t1 = new global::Sharpy.Time(10, 0);
#line (460, 5) - (460, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var t2 = new global::Sharpy.Time(12, 0);
#line (461, 5) - (461, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(t1 < t2);
#line (462, 5) - (462, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(t2 > t1);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimedeltaComparisonOperators()
        {
#line (466, 5) - (466, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td1 = new global::Sharpy.Timedelta(days: 1);
#line (467, 5) - (467, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var td2 = new global::Sharpy.Timedelta(hours: 12);
#line (468, 5) - (468, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(td1 > td2);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeStrftimeBasicFormat()
        {
#line (474, 5) - (474, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 15, 14, 30, 45);
#line (475, 5) - (475, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("2024-01-15", dt.Strftime("%Y-%m-%d"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeStrftimeDayNames()
        {
#line (479, 5) - (479, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 15);
#line (480, 5) - (480, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("Monday", dt.Strftime("%A"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeStrftimePercentLiteral()
        {
#line (484, 5) - (484, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 15);
#line (485, 5) - (485, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("100%", dt.Strftime("100%%"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateStrftime()
        {
#line (489, 5) - (489, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var d = new global::Sharpy.Date(2024, 1, 15);
#line (490, 5) - (490, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("2024-01-15", d.Strftime("%Y-%m-%d"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeStrptimeParsesDate()
        {
#line (496, 5) - (496, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = global::Sharpy.DateTime.Strptime("2024-01-15", "%Y-%m-%d");
#line (497, 5) - (497, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2024, dt.Year);
#line (498, 5) - (498, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1, dt.Month);
#line (499, 5) - (499, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(15, dt.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeWeekdayMondayIsZero()
        {
#line (506, 5) - (506, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 15);
#line (507, 5) - (507, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, dt.Weekday());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeIsoweekdayMondayIsOne()
        {
#line (511, 5) - (511, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 15);
#line (512, 5) - (512, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1, dt.Isoweekday());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeIsoformatDefault()
        {
#line (516, 5) - (516, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 15);
#line (517, 5) - (517, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("2024-01-15T00:00:00", dt.Isoformat());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeIsoformatCustomSep()
        {
#line (521, 5) - (521, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 15);
#line (522, 5) - (522, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("2024-01-15 00:00:00", dt.Isoformat(" "));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeReplace()
        {
#line (526, 5) - (526, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 15, 10, 30);
#line (527, 5) - (527, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = dt.Replace(year: (int?)2025);
#line (528, 5) - (528, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2025, result.Year);
#line (529, 5) - (529, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1, result.Month);
#line (530, 5) - (530, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(15, result.Day);
#line (531, 5) - (531, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(10, result.Hour);
#line (532, 5) - (532, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(30, result.Minute);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeFromisoformat()
        {
#line (536, 5) - (536, 64) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = global::Sharpy.DateTime.Fromisoformat("2024-01-15T14:30:00");
#line (537, 5) - (537, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2024, dt.Year);
#line (538, 5) - (538, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(14, dt.Hour);
#line (539, 5) - (539, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(30, dt.Minute);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateWeekdayMondayIsZero()
        {
#line (543, 5) - (543, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var d = new global::Sharpy.Date(2024, 1, 15);
#line (544, 5) - (544, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, d.Weekday());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateIsoformat()
        {
#line (548, 5) - (548, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var d = new global::Sharpy.Date(2024, 1, 15);
#line (549, 5) - (549, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("2024-01-15", d.Isoformat());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateToordinal()
        {
#line (553, 5) - (553, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var d = new global::Sharpy.Date(2024, 1, 15);
#line (554, 5) - (554, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(738900, d.Toordinal());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateFromordinal()
        {
#line (558, 5) - (558, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var d = global::Sharpy.Date.Fromordinal(738900);
#line (559, 5) - (559, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2024, d.Year);
#line (560, 5) - (560, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1, d.Month);
#line (561, 5) - (561, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(15, d.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateFromisoformat()
        {
#line (565, 5) - (565, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var d = global::Sharpy.Date.Fromisoformat("2024-01-15");
#line (566, 5) - (566, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2024, d.Year);
#line (567, 5) - (567, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1, d.Month);
#line (568, 5) - (568, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(15, d.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateReplace()
        {
#line (572, 5) - (572, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var d = new global::Sharpy.Date(2024, 1, 15);
#line (573, 5) - (573, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = d.Replace(month: 6);
#line (574, 5) - (574, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2024, result.Year);
#line (575, 5) - (575, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(6, result.Month);
#line (576, 5) - (576, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(15, result.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimezoneUtcHasZeroOffset()
        {
#line (582, 5) - (582, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var utc = global::Sharpy.Timezone.Utc;
#line (583, 5) - (583, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, utc.Utcoffset().Days);
#line (584, 5) - (584, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, utc.Utcoffset().Seconds);
#line (585, 5) - (585, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("UTC", utc.Tzname());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimezoneCustomHasCorrectOffset()
        {
#line (589, 5) - (589, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var est = new global::Sharpy.Timezone(new global::Sharpy.Timedelta(hours: -5), "EST");
#line (590, 5) - (590, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(-18000.0d, est.Utcoffset().TotalSeconds);
#line (591, 5) - (591, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("EST", est.Tzname());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeAstimezoneConvertsCorrectly()
        {
#line (595, 5) - (595, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var utc = global::Sharpy.Timezone.Utc;
#line (596, 5) - (596, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var est = new global::Sharpy.Timezone(new global::Sharpy.Timedelta(hours: -5), "EST");
#line (597, 5) - (597, 66) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dtUtc = new global::Sharpy.DateTime(2024, 1, 15, 12, 0, 0, tzinfo: utc);
#line (598, 5) - (598, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dtEst = dtUtc.Astimezone(est);
#line (599, 5) - (599, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(7, dtEst.Hour);
#line (600, 5) - (600, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Same(est, dtEst.Tzinfo);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeWithTimezoneToStringIncludesOffset()
        {
#line (604, 5) - (604, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var utc = global::Sharpy.Timezone.Utc;
#line (605, 5) - (605, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 15, 12, 0, 0, tzinfo: utc);
#line (606, 5) - (606, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.True(global::Sharpy.Builtins.Str(dt).Contains("+00:00"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeLeapYearFeb29()
        {
#line (612, 5) - (612, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 2, 29);
#line (613, 5) - (613, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = dt + new global::Sharpy.Timedelta(days: 1);
#line (614, 5) - (614, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(3, result.Month);
#line (615, 5) - (615, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1, result.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateCrossMonthBoundary()
        {
#line (619, 5) - (619, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var d = new global::Sharpy.Date(2024, 1, 31);
#line (620, 5) - (620, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = d + new global::Sharpy.Timedelta(days: 1);
#line (621, 5) - (621, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(2, result.Month);
#line (622, 5) - (622, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(1, result.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatetimeMidnightCrossing()
        {
#line (626, 5) - (626, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var dt = new global::Sharpy.DateTime(2024, 1, 15, 23, 30, 0);
#line (627, 5) - (627, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var result = dt + new global::Sharpy.Timedelta(hours: 1);
#line (628, 5) - (628, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(16, result.Day);
#line (629, 5) - (629, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(0, result.Hour);
#line (630, 5) - (630, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal(30, result.Minute);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeIsoformatWithoutMicroseconds()
        {
#line (634, 5) - (634, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var t = new global::Sharpy.Time(14, 30, 0);
#line (635, 5) - (635, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("14:30:00", t.Isoformat());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeIsoformatWithMicroseconds()
        {
#line (639, 5) - (639, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            var t = new global::Sharpy.Time(14, 30, 0, 123456);
#line (640, 5) - (640, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_tests.spy"
            Xunit.Assert.Equal("14:30:00.123456", t.Isoformat());
#line hidden
        }
    }
}
#line default
