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
using static Sharpy.Stdlib.Tests.Spy.Datetime.DatetimeDateTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Datetime
    {
        [global::Sharpy.SharpyModule("datetime.datetime_date_tests")]
        public static partial class DatetimeDateTests
        {
        }
    }

    public static partial class Datetime
    {
        public partial class DatetimeDateTestsTests
        {
            [Xunit.FactAttribute]
            public void TestDateTodayYearIsRecent()
            {
#line (16, 5) - (16, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var today = global::Sharpy.Date.Today();
#line (17, 5) - (17, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.True(today.Year >= 2024);
            }

            [Xunit.FactAttribute]
            public void TestDateWeekdaySundayIsSix()
            {
#line (22, 5) - (22, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d = new global::Sharpy.Date(2024, 1, 14);
#line (23, 5) - (23, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(6, d.Weekday());
            }

            [Xunit.FactAttribute]
            public void TestDateWeekdayKnownMondayIsZero()
            {
#line (28, 5) - (28, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d = new global::Sharpy.Date(2024, 1, 1);
#line (29, 5) - (29, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(0, d.Weekday());
            }

            [Xunit.FactAttribute]
            public void TestDateIsoweekdayMondayIsOne()
            {
#line (34, 5) - (34, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d = new global::Sharpy.Date(2024, 1, 1);
#line (35, 5) - (35, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(1, d.Isoweekday());
            }

            [Xunit.FactAttribute]
            public void TestDateIsoweekdaySundayIsSeven()
            {
#line (40, 5) - (40, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d = new global::Sharpy.Date(2024, 1, 14);
#line (41, 5) - (41, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(7, d.Isoweekday());
            }

            [Xunit.FactAttribute]
            public void TestDateIsoformatReturnsYyyyMmDd()
            {
#line (45, 5) - (45, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d = new global::Sharpy.Date(2024, 3, 5);
#line (46, 5) - (46, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal("2024-03-05", d.Isoformat());
            }

            [Xunit.FactAttribute]
            public void TestDateReplaceOnlyYear()
            {
#line (50, 5) - (50, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d = new global::Sharpy.Date(2024, 6, 20);
#line (51, 5) - (51, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var result = d.Replace(year: 2025);
#line (52, 5) - (52, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(2025, result.Year);
#line (53, 5) - (53, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(6, result.Month);
#line (54, 5) - (54, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(20, result.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateReplaceOnlyDay()
            {
#line (58, 5) - (58, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d = new global::Sharpy.Date(2024, 6, 20);
#line (59, 5) - (59, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var result = d.Replace(day: 1);
#line (60, 5) - (60, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(2024, result.Year);
#line (61, 5) - (61, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(6, result.Month);
#line (62, 5) - (62, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(1, result.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateToordinalFromordinalRoundTrip()
            {
#line (66, 5) - (66, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var original = new global::Sharpy.Date(2024, 6, 15);
#line (67, 5) - (67, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                int ordinal = original.Toordinal();
#line (68, 5) - (68, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var restored = global::Sharpy.Date.Fromordinal(ordinal);
#line (69, 5) - (69, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(original.Year, restored.Year);
#line (70, 5) - (70, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(original.Month, restored.Month);
#line (71, 5) - (71, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(original.Day, restored.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateFromisoformatParsesCorrectly()
            {
#line (75, 5) - (75, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d = global::Sharpy.Date.Fromisoformat("2024-01-15");
#line (76, 5) - (76, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(2024, d.Year);
#line (77, 5) - (77, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(1, d.Month);
#line (78, 5) - (78, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(15, d.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateStrftimeYearMonthDay()
            {
#line (82, 5) - (82, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d = new global::Sharpy.Date(2024, 3, 5);
#line (83, 5) - (83, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal("2024/03/05", d.Strftime("%Y/%m/%d"));
            }

            [Xunit.FactAttribute]
            public void TestDatePlusTimedeltaIncrementDay()
            {
#line (87, 5) - (87, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d = new global::Sharpy.Date(2024, 6, 30);
#line (88, 5) - (88, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var result = d + new global::Sharpy.Timedelta(days: 1);
#line (89, 5) - (89, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(2024, result.Year);
#line (90, 5) - (90, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(7, result.Month);
#line (91, 5) - (91, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(1, result.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateMinusTimedeltaDecrementDay()
            {
#line (95, 5) - (95, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d = new global::Sharpy.Date(2024, 3, 1);
#line (96, 5) - (96, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var result = d - new global::Sharpy.Timedelta(days: 1);
#line (97, 5) - (97, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(2024, result.Year);
#line (98, 5) - (98, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(2, result.Month);
#line (99, 5) - (99, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(29, result.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateMinusDateReturnsTimedelta()
            {
#line (103, 5) - (103, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d1 = new global::Sharpy.Date(2024, 2, 10);
#line (104, 5) - (104, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d2 = new global::Sharpy.Date(2024, 1, 1);
#line (105, 5) - (105, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var td = d1 - d2;
#line (106, 5) - (106, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(40, td.Days);
            }

            [Xunit.FactAttribute]
            public void TestDateComparisonGreaterThan()
            {
#line (110, 5) - (110, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d1 = new global::Sharpy.Date(2024, 6, 15);
#line (111, 5) - (111, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d2 = new global::Sharpy.Date(2024, 1, 1);
#line (112, 5) - (112, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.True(d1 > d2);
            }

            [Xunit.FactAttribute]
            public void TestDateComparisonLessOrEqual()
            {
#line (116, 5) - (116, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d1 = new global::Sharpy.Date(2024, 1, 1);
#line (117, 5) - (117, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d2 = new global::Sharpy.Date(2024, 1, 1);
#line (118, 5) - (118, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.True(d1 <= d2);
            }

            [Xunit.FactAttribute]
            public void TestDateComparisonGreaterOrEqual()
            {
#line (122, 5) - (122, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d1 = new global::Sharpy.Date(2024, 6, 1);
#line (123, 5) - (123, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d2 = new global::Sharpy.Date(2024, 1, 1);
#line (124, 5) - (124, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.True(d1 >= d2);
            }

            [Xunit.FactAttribute]
            public void TestDateComparisonNotEqual()
            {
#line (128, 5) - (128, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d1 = new global::Sharpy.Date(2024, 1, 1);
#line (129, 5) - (129, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d2 = new global::Sharpy.Date(2024, 1, 2);
#line (130, 5) - (130, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.NotEqual(d2, d1);
            }

            [Xunit.FactAttribute]
            public void TestDateLeapYearFeb29IsValid()
            {
#line (135, 5) - (135, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                var d = new global::Sharpy.Date(2024, 2, 29);
#line (136, 5) - (136, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(2, d.Month);
#line (137, 5) - (137, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Equal(29, d.Day);
            }

            [Xunit.FactAttribute]
            public void TestDateNonLeapYearFeb29ThrowsArgumentOutOfRange()
            {
#line (142, 5) - (144, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                Xunit.Assert.Throws<ArgumentOutOfRangeException>((global::System.Action)(() =>
                {
#line (143, 9) - (143, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/datetime/datetime_date_tests.spy"
                    new global::Sharpy.Date(2023, 2, 29);
                }));
            }
        }
    }
}
