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
using calendar = global::Sharpy.CalendarModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Calendar.CalendarTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Calendar
    {
        [global::Sharpy.SharpyModule("calendar.calendar_tests")]
        public static partial class CalendarTests
        {
        }
    }

    public static partial class Calendar
    {
        public partial class CalendarTestsTests
        {
            [Xunit.TheoryAttribute]
            [Xunit.InlineDataAttribute(2024, true)]
            [Xunit.InlineDataAttribute(2025, false)]
            [Xunit.InlineDataAttribute(2000, true)]
            [Xunit.InlineDataAttribute(1900, false)]
            public void TestIsleapReturnsExpected(int year, bool expected)
            {
#line (9, 5) - (9, 46) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(expected, calendar.Isleap(year));
            }

            [Xunit.FactAttribute]
            public void TestLeapdaysForwardRangeCountsLeapYears()
            {
#line (15, 5) - (15, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(6, calendar.Leapdays(2000, 2024));
            }

            [Xunit.FactAttribute]
            public void TestLeapdaysReversedRangeReturnsNegative()
            {
#line (19, 5) - (19, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(-6, calendar.Leapdays(2024, 2000));
            }

            [Xunit.FactAttribute]
            public void TestWeekdayThursdayReturnsThree()
            {
#line (25, 5) - (25, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(3, calendar.Weekday(2026, 5, 28));
            }

            [Xunit.FactAttribute]
            public void TestWeekdayMondayReturnsZero()
            {
#line (29, 5) - (29, 46) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(0, calendar.Weekday(2026, 6, 1));
            }

            [Xunit.FactAttribute]
            public void TestMonthrangeFebNonLeapReturnsSundayAnd28()
            {
#line (35, 5) - (35, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                global::System.ValueTuple<int, int> result = calendar.Monthrange(2026, 2);
#line (36, 5) - (36, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal((6, 28), result);
            }

            [Xunit.FactAttribute]
            public void TestMonthrangeFebLeapReturnsThursdayAnd29()
            {
#line (40, 5) - (40, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                global::System.ValueTuple<int, int> result = calendar.Monthrange(2024, 2);
#line (41, 5) - (41, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal((3, 29), result);
            }

            [Xunit.FactAttribute]
            public void TestMonthrangeJanuaryReturnsThursdayAnd31()
            {
#line (45, 5) - (45, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                global::System.ValueTuple<int, int> result = calendar.Monthrange(2026, 1);
#line (46, 5) - (46, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal((3, 31), result);
            }

            [Xunit.FactAttribute]
            public void TestMonthcalendarJune2026FirstWeekStartsOnDayOne()
            {
#line (52, 5) - (52, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                calendar.Setfirstweekday(0);
#line (53, 5) - (53, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Sharpy.List<Sharpy.List<int>> cal = calendar.Monthcalendar(2026, 6);
#line (55, 5) - (58, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                foreach (var __loopVar_0 in cal)
                {
                    var week = __loopVar_0;
#line (56, 9) - (56, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                    Xunit.Assert.Equal(7, global::Sharpy.Builtins.Len(week));
                }

#line (58, 5) - (58, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Sharpy.List<int> firstWeek = cal[0];
#line (59, 5) - (59, 48) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3, 4, 5, 6, 7 }, firstWeek);
#line (61, 5) - (61, 46) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Sharpy.List<int> lastWeek = cal[global::Sharpy.Builtins.Len(cal) - 1];
#line (62, 5) - (62, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(29, lastWeek[0]);
#line (63, 5) - (63, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(30, lastWeek[1]);
#line (64, 5) - (64, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(0, lastWeek[2]);
#line (66, 5) - (66, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                calendar.Setfirstweekday(0);
            }

            [Xunit.FactAttribute]
            public void TestConstantsWeekdayValuesAreCorrect()
            {
#line (72, 5) - (72, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(0, calendar.MONDAY);
#line (73, 5) - (73, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(1, calendar.TUESDAY);
#line (74, 5) - (74, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(5, calendar.SATURDAY);
#line (75, 5) - (75, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(6, calendar.SUNDAY);
            }

            [Xunit.FactAttribute]
            public void TestConstantsDayNameHasSevenEntriesStartingMonday()
            {
#line (79, 5) - (79, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(7, global::Sharpy.Builtins.Len(calendar.DayName));
#line (80, 5) - (80, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal("Monday", calendar.DayName[0]);
#line (81, 5) - (81, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal("Sunday", calendar.DayName[6]);
            }

            [Xunit.FactAttribute]
            public void TestConstantsMonthNameFirstEntryEmptyThenJanuary()
            {
#line (85, 5) - (85, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal("", calendar.MonthName[0]);
#line (86, 5) - (86, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal("January", calendar.MonthName[1]);
#line (87, 5) - (87, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal("December", calendar.MonthName[12]);
            }

            [Xunit.FactAttribute]
            public void TestConstantsMonthAbbrFirstEntryEmptyThenJan()
            {
#line (91, 5) - (91, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal("", calendar.MonthAbbr[0]);
#line (92, 5) - (92, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal("Jan", calendar.MonthAbbr[1]);
            }

            [Xunit.FactAttribute]
            public void TestItermonthdaysFeb2026PadsLeadingZeros()
            {
#line (98, 5) - (98, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                var cal = new global::Sharpy.Calendar(0);
#line (99, 5) - (99, 56) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Sharpy.List<int> days = new global::Sharpy.List<int>(cal.Itermonthdays(2026, 2));
#line (101, 5) - (103, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                foreach (var __loopVar_1 in global::Sharpy.Builtins.Range(6))
                {
                    var i = __loopVar_1;
#line (102, 9) - (102, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                    Xunit.Assert.Equal(0, days[i]);
                }

#line (103, 5) - (103, 25) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(1, days[6]);
#line (105, 5) - (105, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(days) % 7);
#line (107, 5) - (107, 55) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Sharpy.List<int> nonZero = new Sharpy.List<int>(days.Where((int d) => d != 0).Select((int d) => d));
#line (108, 5) - (108, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.List<int>(global::Sharpy.Builtins.Range(1, 29)), nonZero);
            }

            [Xunit.FactAttribute]
            public void TestItermonthdays2FirstweekdaySundayFirstWeekdayIsSix()
            {
#line (114, 5) - (114, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                var cal = new global::Sharpy.Calendar(6);
#line (115, 5) - (115, 46) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                var pairs = new global::Sharpy.List<global::System.ValueTuple<int, int>>(cal.Itermonthdays2(2026, 6));
#line (117, 5) - (117, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(6, pairs[0].Item2);
#line (119, 5) - (119, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(1, pairs[1].Item1);
#line (120, 5) - (120, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(0, pairs[1].Item2);
            }

            [Xunit.FactAttribute]
            public void TestTextCalendarFormatmonthContainsKeyContent()
            {
#line (126, 5) - (126, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                var cal = new global::Sharpy.TextCalendar(0);
#line (127, 5) - (127, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                string output = cal.Formatmonth(2026, 6);
#line (128, 5) - (128, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("June", output);
#line (129, 5) - (129, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("2026", output);
#line (130, 5) - (130, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("Mo", output);
#line (131, 5) - (131, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("Su", output);
#line (132, 5) - (132, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("15", output);
#line (133, 5) - (133, 27) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("30", output);
            }

            [Xunit.FactAttribute]
            public void TestHtmlCalendarFormatmonthContainsTableAndDays()
            {
#line (139, 5) - (139, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                var cal = new global::Sharpy.HTMLCalendar(0);
#line (140, 5) - (140, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                string output = cal.Formatmonth(2026, 6);
#line (141, 5) - (141, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("<table", output);
#line (142, 5) - (142, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("class=\"month\"", output);
#line (143, 5) - (143, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("June 2026", output);
#line (144, 5) - (144, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("class=\"mon\"", output);
#line (145, 5) - (145, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("class=\"noday\"", output);
#line (146, 5) - (146, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains(">15</td>", output);
#line (147, 5) - (147, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains(">30</td>", output);
#line (148, 5) - (148, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("</table>", output);
            }

            [Xunit.FactAttribute]
            public void TestHtmlCalendarFormatmonthWithoutYearOmitsYear()
            {
#line (152, 5) - (152, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                var cal = new global::Sharpy.HTMLCalendar(0);
#line (153, 5) - (153, 60) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                string output = cal.Formatmonth(2026, 6, withyear: false);
#line (154, 5) - (154, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains(">June</th>", output);
#line (155, 5) - (155, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.DoesNotContain("June 2026", output);
            }

            [Xunit.FactAttribute]
            public void TestTimegmEpochReturnsZero()
            {
#line (161, 5) - (161, 54) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(0, calendar.Timegm(1970, 1, 1, 0, 0, 0));
            }

            [Xunit.FactAttribute]
            public void TestTimegmKnownDateReturnsExpectedSeconds()
            {
#line (165, 5) - (165, 63) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(1767225600, calendar.Timegm(2026, 1, 1, 0, 0, 0));
            }

            [Xunit.FactAttribute]
            public void TestSetfirstweekdayChangesModuleMonthOutput()
            {
#line (171, 5) - (171, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                calendar.Setfirstweekday(0);
#line (172, 5) - (172, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                string mondayFirst = calendar.Month(2026, 6);
#line (173, 5) - (173, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                calendar.Setfirstweekday(6);
#line (174, 5) - (174, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                string sundayFirst = calendar.Month(2026, 6);
#line (176, 5) - (176, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.NotEqual(sundayFirst, mondayFirst);
#line (177, 5) - (177, 51) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("Mo Tu We Th Fr Sa Su", mondayFirst);
#line (178, 5) - (178, 51) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Contains("Su Mo Tu We Th Fr Sa", sundayFirst);
#line (180, 5) - (180, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                calendar.Setfirstweekday(0);
            }

            [Xunit.FactAttribute]
            public void TestSetfirstweekdayInvalidValueThrows()
            {
#line (184, 5) - (186, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (185, 9) - (185, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                    calendar.Setfirstweekday(7);
                }));
#line (186, 5) - (191, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (187, 9) - (187, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                    calendar.Setfirstweekday(-1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestMonthrangeLeapVsNonLeapFebruaryDiffersInDayCount()
            {
#line (193, 5) - (193, 58) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                global::System.ValueTuple<int, int> leap = calendar.Monthrange(2024, 2);
#line (194, 5) - (194, 62) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                global::System.ValueTuple<int, int> nonLeap = calendar.Monthrange(2025, 2);
#line (195, 5) - (195, 26) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(29, leap.Item2);
#line (196, 5) - (196, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(28, nonLeap.Item2);
            }

            [Xunit.FactAttribute]
            public void TestMonthrangeDecemberReturns31Days()
            {
#line (200, 5) - (200, 61) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                global::System.ValueTuple<int, int> result = calendar.Monthrange(2026, 12);
#line (201, 5) - (201, 28) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(31, result.Item2);
            }

            [Xunit.FactAttribute]
            public void TestMonthcalendarDecember2026HasAllDays()
            {
#line (205, 5) - (205, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                calendar.Setfirstweekday(0);
#line (206, 5) - (206, 61) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Sharpy.List<Sharpy.List<int>> cal = calendar.Monthcalendar(2026, 12);
#line (207, 5) - (207, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Sharpy.List<int> allDays = new Sharpy.List<int>()
                {
                };
#line (208, 5) - (212, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                foreach (var __loopVar_2 in cal)
                {
                    var week = __loopVar_2;
#line (209, 9) - (212, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                    foreach (var __loopVar_3 in week)
                    {
                        var d = __loopVar_3;
#line (210, 13) - (212, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                        if (d != 0)
                        {
#line (211, 17) - (211, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                            allDays.Append(d);
                        }
                    }
                }

#line (212, 5) - (212, 20) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                allDays.Sort();
#line (213, 5) - (213, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.List<int>(global::Sharpy.Builtins.Range(1, 32)), allDays);
#line (215, 5) - (215, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/calendar/calendar_tests.spy"
                calendar.Setfirstweekday(0);
            }
        }
    }
}
