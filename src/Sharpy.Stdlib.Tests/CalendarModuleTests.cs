using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class CalendarModuleTests
{
    // --- isleap ---

    [Theory]
    [InlineData(2024, true)]
    [InlineData(2000, true)]
    [InlineData(1900, false)]
    [InlineData(2023, false)]
    [InlineData(2100, false)]
    [InlineData(400, true)]
    public void Isleap_ReturnsCorrectResult(int year, bool expected)
    {
        CalendarModule.Isleap(year).Should().Be(expected);
    }

    // --- leapdays ---

    [Theory]
    [InlineData(2000, 2024, 6)]
    [InlineData(2000, 2001, 1)]
    [InlineData(1900, 1901, 0)]
    [InlineData(2020, 2020, 0)]
    [InlineData(1996, 2001, 2)]
    public void Leapdays_ReturnsCorrectCount(int y1, int y2, int expected)
    {
        CalendarModule.Leapdays(y1, y2).Should().Be(expected);
    }

    // --- weekday ---

    [Theory]
    [InlineData(2026, 5, 28, 3)] // Thursday
    [InlineData(2024, 1, 1, 0)]  // Monday
    [InlineData(2024, 1, 7, 6)]  // Sunday
    [InlineData(2024, 3, 16, 5)] // Saturday
    public void Weekday_ReturnsCorrectDay(int year, int month, int day, int expected)
    {
        CalendarModule.Weekday(year, month, day).Should().Be(expected);
    }

    // --- monthrange ---

    [Fact]
    public void Monthrange_February_LeapYear()
    {
        var (firstDay, numDays) = CalendarModule.Monthrange(2024, 2);
        firstDay.Should().Be(3); // Thursday
        numDays.Should().Be(29);
    }

    [Fact]
    public void Monthrange_February_NonLeapYear()
    {
        var (firstDay, numDays) = CalendarModule.Monthrange(2023, 2);
        firstDay.Should().Be(2); // Wednesday
        numDays.Should().Be(28);
    }

    [Fact]
    public void Monthrange_January2026()
    {
        var (firstDay, numDays) = CalendarModule.Monthrange(2026, 1);
        firstDay.Should().Be(3); // Thursday
        numDays.Should().Be(31);
    }

    // --- monthcalendar ---

    [Fact]
    public void Monthcalendar_ReturnsCorrectMatrix()
    {
        // June 2026 starts on Monday
        var weeks = CalendarModule.Monthcalendar(2026, 6);

        weeks.Should().HaveCount(5);
        weeks[0].Should().Equal(1, 2, 3, 4, 5, 6, 7);
        weeks[4].Should().Equal(29, 30, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void Monthcalendar_February_LeapYear()
    {
        var weeks = CalendarModule.Monthcalendar(2024, 2);

        // Feb 2024 starts on Thursday (index 3 from Monday)
        weeks[0].Should().Equal(0, 0, 0, 1, 2, 3, 4);
        weeks[weeks.Count - 1].Should().Equal(26, 27, 28, 29, 0, 0, 0);
    }

    [Fact]
    public void Monthcalendar_WithCustomFirstweekday()
    {
        // June 2026, firstweekday=6 (Sunday first)
        var weeks = CalendarModule.Monthcalendar(2026, 6, 6);

        // June 1 is Monday, so with Sunday first, Monday is index 1
        weeks[0][0].Should().Be(0); // Sunday slot is empty
        weeks[0][1].Should().Be(1); // Monday = June 1
    }

    // --- month (text formatting) ---

    [Fact]
    public void Month_ContainsMonthNameAndYear()
    {
        string result = CalendarModule.Month(2026, 6);

        result.Should().Contain("June");
        result.Should().Contain("2026");
        result.Should().Contain("Mo");
    }

    // --- calendar (year text) ---

    [Fact]
    public void Calendar_ContainsAllMonths()
    {
        string result = CalendarModule.Calendar(2026);

        result.Should().Contain("January");
        result.Should().Contain("February");
        result.Should().Contain("December");
        result.Should().Contain("2026");
    }

    // --- TextCalendar ---

    [Fact]
    public void TextCalendar_Formatmonth_ProducesOutput()
    {
        var cal = new TextCalendar(0);
        string result = cal.Formatmonth(2026, 6);

        result.Should().Contain("June 2026");
        result.Should().Contain("Mo");
        result.Should().Contain("30");
    }

    [Fact]
    public void TextCalendar_CustomFirstweekday()
    {
        var cal = new TextCalendar(6); // Sunday first
        string result = cal.Formatmonth(2026, 6);

        result.Should().Contain("Su");
        // Su should appear before Mo in the header
        int suIdx = result.IndexOf("Su");
        int moIdx = result.IndexOf("Mo");
        suIdx.Should().BeLessThan(moIdx);
    }

    [Fact]
    public void TextCalendar_Formatyear_ProducesOutput()
    {
        var cal = new TextCalendar(0);
        string result = cal.Formatyear(2026);

        result.Should().Contain("2026");
        result.Should().Contain("January");
        result.Should().Contain("December");
    }

    // --- HTMLCalendar ---

    [Fact]
    public void HTMLCalendar_Formatmonth_ProducesHtml()
    {
        var cal = new HTMLCalendar(0);
        string result = cal.Formatmonth(2026, 6);

        result.Should().Contain("<table");
        result.Should().Contain("June 2026");
        result.Should().Contain("<td class=\"mon\">1</td>");
        result.Should().Contain("</table>");
    }

    [Fact]
    public void HTMLCalendar_Formatmonth_WithoutYear()
    {
        var cal = new HTMLCalendar(0);
        string result = cal.Formatmonth(2026, 6, false);

        result.Should().Contain("June");
        result.Should().NotContain("June 2026");
    }

    [Fact]
    public void HTMLCalendar_CustomFirstweekday()
    {
        var cal = new HTMLCalendar(6); // Sunday first
        string result = cal.Formatmonth(2026, 6);

        // First th after month header should be Sun
        result.Should().Contain("<th class=\"sun\">Sun</th>");
    }

    [Fact]
    public void HTMLCalendar_Formatyear_ProducesHtml()
    {
        var cal = new HTMLCalendar(0);
        string result = cal.Formatyear(2026);

        result.Should().Contain("<table");
        result.Should().Contain("class=\"year\"");
        result.Should().Contain("2026");
        result.Should().Contain("</table>");
    }

    // --- Edge cases ---

    [Fact]
    public void Weekday_EdgeCase_LeapDay()
    {
        // Feb 29, 2024 was a Thursday (3 in 0=Mon scheme)
        CalendarModule.Weekday(2024, 2, 29).Should().Be(3);
    }

    [Fact]
    public void Monthrange_December()
    {
        var (firstDay, numDays) = CalendarModule.Monthrange(2026, 12);
        numDays.Should().Be(31);
        firstDay.Should().Be(1); // Tuesday
    }
}
