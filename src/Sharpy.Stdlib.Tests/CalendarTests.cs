using System.Linq;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Calendar_Tests
{
    // ----- Isleap -----

    [Theory]
    [InlineData(2024, true)]
    [InlineData(2025, false)]
    [InlineData(2000, true)]
    [InlineData(1900, false)]
    public void Isleap_ReturnsExpected(int year, bool expected)
    {
        Sharpy.CalendarModule.Isleap(year).Should().Be(expected);
    }

    // ----- Leapdays -----

    [Fact]
    public void Leapdays_ForwardRange_CountsLeapYears()
    {
        Sharpy.CalendarModule.Leapdays(2000, 2024).Should().Be(6);
    }

    [Fact]
    public void Leapdays_ReversedRange_ReturnsNegative()
    {
        Sharpy.CalendarModule.Leapdays(2024, 2000).Should().Be(-6);
    }

    // ----- Weekday -----

    [Fact]
    public void Weekday_Thursday_ReturnsThree()
    {
        Sharpy.CalendarModule.Weekday(2026, 5, 28).Should().Be(3);
    }

    [Fact]
    public void Weekday_Monday_ReturnsZero()
    {
        Sharpy.CalendarModule.Weekday(2026, 6, 1).Should().Be(0);
    }

    // ----- Monthrange -----

    [Fact]
    public void Monthrange_FebNonLeap_ReturnsSundayAnd28()
    {
        Sharpy.CalendarModule.Monthrange(2026, 2).Should().Be((6, 28));
    }

    [Fact]
    public void Monthrange_FebLeap_ReturnsThursdayAnd29()
    {
        Sharpy.CalendarModule.Monthrange(2024, 2).Should().Be((3, 29));
    }

    [Fact]
    public void Monthrange_January_ReturnsThursdayAnd31()
    {
        Sharpy.CalendarModule.Monthrange(2026, 1).Should().Be((3, 31));
    }

    // ----- Monthcalendar -----

    [Fact]
    public void Monthcalendar_June2026_FirstWeekStartsOnDayOne()
    {
        Sharpy.CalendarModule.Setfirstweekday(0);
        try
        {
            var cal = Sharpy.CalendarModule.Monthcalendar(2026, 6);

            // Each week has 7 days.
            foreach (var week in cal)
                week.Count.Should().Be(7);

            // June 1, 2026 is a Monday, so the first week is exactly 1..7.
            var firstWeek = cal[0];
            firstWeek.Should().Equal(1, 2, 3, 4, 5, 6, 7);

            // The last real day (30) appears, padded with trailing zeros.
            var lastWeek = cal[cal.Count - 1];
            lastWeek[0].Should().Be(29);
            lastWeek[1].Should().Be(30);
            lastWeek[2].Should().Be(0);
        }
        finally
        {
            Sharpy.CalendarModule.Setfirstweekday(0);
        }
    }

    // ----- Constants -----

    [Fact]
    public void Constants_WeekdayValues_AreCorrect()
    {
        Sharpy.CalendarModule.MONDAY.Should().Be(0);
        Sharpy.CalendarModule.TUESDAY.Should().Be(1);
        Sharpy.CalendarModule.SATURDAY.Should().Be(5);
        Sharpy.CalendarModule.SUNDAY.Should().Be(6);
    }

    [Fact]
    public void Constants_DayName_HasSevenEntriesStartingMonday()
    {
        Sharpy.CalendarModule.DayName.Count.Should().Be(7);
        Sharpy.CalendarModule.DayName[0].Should().Be("Monday");
        Sharpy.CalendarModule.DayName[6].Should().Be("Sunday");
    }

    [Fact]
    public void Constants_MonthName_FirstEntryEmptyThenJanuary()
    {
        Sharpy.CalendarModule.MonthName[0].Should().Be("");
        Sharpy.CalendarModule.MonthName[1].Should().Be("January");
        Sharpy.CalendarModule.MonthName[12].Should().Be("December");
    }

    [Fact]
    public void Constants_MonthAbbr_FirstEntryEmptyThenJan()
    {
        Sharpy.CalendarModule.MonthAbbr[0].Should().Be("");
        Sharpy.CalendarModule.MonthAbbr[1].Should().Be("Jan");
    }

    // ----- Itermonthdays (padding) -----

    [Fact]
    public void Itermonthdays_Feb2026_PadsLeadingZeros()
    {
        var cal = new Sharpy.Calendar(0);
        var days = cal.Itermonthdays(2026, 2).ToList();

        // Feb 1, 2026 is a Sunday -> 6 leading zeros before day 1.
        days.Take(6).Should().AllBeEquivalentTo(0);
        days[6].Should().Be(1);

        // Total count is a multiple of 7.
        (days.Count % 7).Should().Be(0);

        // All non-zero days 1..28 are present.
        days.Where(d => d != 0).Should().Equal(Enumerable.Range(1, 28));
    }

    // ----- Firstweekday -----

    [Fact]
    public void Itermonthdays2_FirstweekdaySunday_FirstWeekdayIsSix()
    {
        var cal = new Sharpy.Calendar(6);
        var pairs = cal.Itermonthdays2(2026, 6).ToList();

        // With Sunday as first weekday, the first slot's weekday is 6.
        pairs[0].weekday.Should().Be(6);
        // June 1 (Monday) lands in the second slot with weekday 0.
        pairs[1].day.Should().Be(1);
        pairs[1].weekday.Should().Be(0);
    }

    // ----- TextCalendar.Formatmonth -----

    [Fact]
    public void TextCalendar_Formatmonth_ContainsKeyContent()
    {
        var cal = new Sharpy.TextCalendar(0);
        string output = cal.Formatmonth(2026, 6);

        output.Should().Contain("June");
        output.Should().Contain("2026");
        output.Should().Contain("Mo");
        output.Should().Contain("Su");
        output.Should().Contain("15");
        output.Should().Contain("30");
    }

    // ----- HTMLCalendar.Formatmonth -----

    [Fact]
    public void HTMLCalendar_Formatmonth_ContainsTableAndDays()
    {
        var cal = new Sharpy.HTMLCalendar(0);
        string output = cal.Formatmonth(2026, 6);

        output.Should().Contain("<table");
        output.Should().Contain("class=\"month\"");
        output.Should().Contain("June 2026");
        output.Should().Contain("class=\"mon\"");
        output.Should().Contain("class=\"noday\"");
        output.Should().Contain(">15</td>");
        output.Should().Contain(">30</td>");
        output.Should().Contain("</table>");
    }

    [Fact]
    public void HTMLCalendar_Formatmonth_WithoutYear_OmitsYear()
    {
        var cal = new Sharpy.HTMLCalendar(0);
        string output = cal.Formatmonth(2026, 6, withyear: false);

        output.Should().Contain(">June</th>");
        output.Should().NotContain("June 2026");
    }

    // ----- Timegm -----

    [Fact]
    public void Timegm_Epoch_ReturnsZero()
    {
        Sharpy.CalendarModule.Timegm(1970, 1, 1, 0, 0, 0).Should().Be(0L);
    }

    [Fact]
    public void Timegm_KnownDate_ReturnsExpectedSeconds()
    {
        Sharpy.CalendarModule.Timegm(2026, 1, 1, 0, 0, 0).Should().Be(1767225600L);
    }

    // ----- Setfirstweekday -----

    [Fact]
    public void Setfirstweekday_ChangesModuleMonthOutput()
    {
        Sharpy.CalendarModule.Setfirstweekday(0);
        try
        {
            string mondayFirst = Sharpy.CalendarModule.Month(2026, 6);

            Sharpy.CalendarModule.Setfirstweekday(6);
            string sundayFirst = Sharpy.CalendarModule.Month(2026, 6);

            // The day-header row differs depending on the first weekday.
            mondayFirst.Should().NotBe(sundayFirst);
            // Monday-first header begins with "Mo"; Sunday-first begins with "Su".
            mondayFirst.Should().Contain("Mo Tu We Th Fr Sa Su");
            sundayFirst.Should().Contain("Su Mo Tu We Th Fr Sa");
        }
        finally
        {
            Sharpy.CalendarModule.Setfirstweekday(0);
        }
    }

    [Fact]
    public void Setfirstweekday_InvalidValue_Throws()
    {
        Assert.Throws<Sharpy.ValueError>(() => Sharpy.CalendarModule.Setfirstweekday(7));
        Assert.Throws<Sharpy.ValueError>(() => Sharpy.CalendarModule.Setfirstweekday(-1));
    }

    // ----- Edge cases -----

    [Fact]
    public void Monthrange_LeapVsNonLeapFebruary_DiffersInDayCount()
    {
        Sharpy.CalendarModule.Monthrange(2024, 2).Item2.Should().Be(29);
        Sharpy.CalendarModule.Monthrange(2025, 2).Item2.Should().Be(28);
    }

    [Fact]
    public void Monthrange_December_Returns31Days()
    {
        Sharpy.CalendarModule.Monthrange(2026, 12).Item2.Should().Be(31);
    }

    [Fact]
    public void Monthcalendar_December2026_HasAllDays()
    {
        Sharpy.CalendarModule.Setfirstweekday(0);
        try
        {
            var cal = Sharpy.CalendarModule.Monthcalendar(2026, 12);
            var allDays = cal.SelectMany(w => w).Where(d => d != 0).OrderBy(d => d);
            allDays.Should().Equal(Enumerable.Range(1, 31));
        }
        finally
        {
            Sharpy.CalendarModule.Setfirstweekday(0);
        }
    }
}
