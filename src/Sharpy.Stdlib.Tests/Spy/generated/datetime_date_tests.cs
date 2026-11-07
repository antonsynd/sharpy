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

namespace Sharpy.Stdlib.Tests.Spy.Datetime.DatetimeDateTests
{
    [global::Sharpy.SharpyModule("datetime.datetime_date_tests")]
    public static partial class DatetimeDateTestsModule
    {
    }

    public partial class DatetimeDateTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestDateTodayYearIsRecent()
        {
#line (15, 5) - (15, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var today = global::Sharpy.Date.Today();
#line (16, 5) - (16, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.True(today.Year >= 2024);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateWeekdaySundayIsSix()
        {
#line (21, 5) - (21, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d = new global::Sharpy.Date(2024, 1, 14);
#line (22, 5) - (22, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(6, d.Weekday());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateWeekdayKnownMondayIsZero()
        {
#line (27, 5) - (27, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d = new global::Sharpy.Date(2024, 1, 1);
#line (28, 5) - (28, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(0, d.Weekday());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateIsoweekdayMondayIsOne()
        {
#line (33, 5) - (33, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d = new global::Sharpy.Date(2024, 1, 1);
#line (34, 5) - (34, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(1, d.Isoweekday());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateIsoweekdaySundayIsSeven()
        {
#line (39, 5) - (39, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d = new global::Sharpy.Date(2024, 1, 14);
#line (40, 5) - (40, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(7, d.Isoweekday());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateIsoformatReturnsYyyyMmDd()
        {
#line (44, 5) - (44, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d = new global::Sharpy.Date(2024, 3, 5);
#line (45, 5) - (45, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal("2024-03-05", d.Isoformat());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateReplaceOnlyYear()
        {
#line (49, 5) - (49, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d = new global::Sharpy.Date(2024, 6, 20);
#line (50, 5) - (50, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var result = d.Replace(year: (int?)2025);
#line (51, 5) - (51, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(2025, result.Year);
#line (52, 5) - (52, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(6, result.Month);
#line (53, 5) - (53, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(20, result.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateReplaceOnlyDay()
        {
#line (57, 5) - (57, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d = new global::Sharpy.Date(2024, 6, 20);
#line (58, 5) - (58, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var result = d.Replace(day: 1);
#line (59, 5) - (59, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(2024, result.Year);
#line (60, 5) - (60, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(6, result.Month);
#line (61, 5) - (61, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(1, result.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateToordinalFromordinalRoundTrip()
        {
#line (65, 5) - (65, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var original = new global::Sharpy.Date(2024, 6, 15);
#line (66, 5) - (66, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            int ordinal = original.Toordinal();
#line (67, 5) - (67, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var restored = global::Sharpy.Date.Fromordinal(ordinal);
#line (68, 5) - (68, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(original.Year, restored.Year);
#line (69, 5) - (69, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(original.Month, restored.Month);
#line (70, 5) - (70, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(original.Day, restored.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateFromisoformatParsesCorrectly()
        {
#line (74, 5) - (74, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d = global::Sharpy.Date.Fromisoformat("2024-01-15");
#line (75, 5) - (75, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(2024, d.Year);
#line (76, 5) - (76, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(1, d.Month);
#line (77, 5) - (77, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(15, d.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateStrftimeYearMonthDay()
        {
#line (81, 5) - (81, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d = new global::Sharpy.Date(2024, 3, 5);
#line (82, 5) - (82, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal("2024/03/05", d.Strftime("%Y/%m/%d"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDatePlusTimedeltaIncrementDay()
        {
#line (86, 5) - (86, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d = new global::Sharpy.Date(2024, 6, 30);
#line (87, 5) - (87, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var result = d + new global::Sharpy.Timedelta(days: 1);
#line (88, 5) - (88, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(2024, result.Year);
#line (89, 5) - (89, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(7, result.Month);
#line (90, 5) - (90, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(1, result.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateMinusTimedeltaDecrementDay()
        {
#line (94, 5) - (94, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d = new global::Sharpy.Date(2024, 3, 1);
#line (95, 5) - (95, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var result = d - new global::Sharpy.Timedelta(days: 1);
#line (96, 5) - (96, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(2024, result.Year);
#line (97, 5) - (97, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(2, result.Month);
#line (98, 5) - (98, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(29, result.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateMinusDateReturnsTimedelta()
        {
#line (102, 5) - (102, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d1 = new global::Sharpy.Date(2024, 2, 10);
#line (103, 5) - (103, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d2 = new global::Sharpy.Date(2024, 1, 1);
#line (104, 5) - (104, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var td = d1 - d2;
#line (105, 5) - (105, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(40, td.Days);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateComparisonGreaterThan()
        {
#line (109, 5) - (109, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d1 = new global::Sharpy.Date(2024, 6, 15);
#line (110, 5) - (110, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d2 = new global::Sharpy.Date(2024, 1, 1);
#line (111, 5) - (111, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.True(d1 > d2);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateComparisonLessOrEqual()
        {
#line (115, 5) - (115, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d1 = new global::Sharpy.Date(2024, 1, 1);
#line (116, 5) - (116, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d2 = new global::Sharpy.Date(2024, 1, 1);
#line (117, 5) - (117, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.True(d1 <= d2);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateComparisonGreaterOrEqual()
        {
#line (121, 5) - (121, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d1 = new global::Sharpy.Date(2024, 6, 1);
#line (122, 5) - (122, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d2 = new global::Sharpy.Date(2024, 1, 1);
#line (123, 5) - (123, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.True(d1 >= d2);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateComparisonNotEqual()
        {
#line (127, 5) - (127, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d1 = new global::Sharpy.Date(2024, 1, 1);
#line (128, 5) - (128, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d2 = new global::Sharpy.Date(2024, 1, 2);
#line (129, 5) - (129, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.NotEqual(d2, d1);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateLeapYearFeb29IsValid()
        {
#line (134, 5) - (134, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            var d = new global::Sharpy.Date(2024, 2, 29);
#line (135, 5) - (135, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(2, d.Month);
#line (136, 5) - (136, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            Xunit.Assert.Equal(29, d.Day);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDateNonLeapYearFeb29RaisesValueError()
        {
#line (141, 5) - (142, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
            bool __raised_0 = false;
#line hidden
            try
            {
#line (142, 9) - (142, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                new global::Sharpy.Date(2023, 2, 29);
#line hidden
            }
            catch (ValueError)
            {
                __raised_0 = true;
            }

            if (!__raised_0)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
        }
    }
}
#line default
