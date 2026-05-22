using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional Date tests not covered by DatetimeTests.cs.
/// </summary>
public class DatetimeDateTests
{
    [Fact]
    public void Date_Today_YearIsRecent()
    {
        var today = Sharpy.Date.Today();

        today.Year.Should().BeGreaterThanOrEqualTo(2024);
    }

    [Fact]
    public void Date_Weekday_SundayIsSix()
    {
        // 2024-01-14 was a Sunday
        var d = new Sharpy.Date(2024, 1, 14);

        d.Weekday().Should().Be(6);
    }

    [Fact]
    public void Date_Weekday_KnownMondayIsZero()
    {
        // 2024-01-01 was a Monday
        var d = new Sharpy.Date(2024, 1, 1);

        d.Weekday().Should().Be(0);
    }

    [Fact]
    public void Date_Isoweekday_MondayIsOne()
    {
        // 2024-01-01 is Monday
        var d = new Sharpy.Date(2024, 1, 1);

        d.Isoweekday().Should().Be(1);
    }

    [Fact]
    public void Date_Isoweekday_SundayIsSeven()
    {
        // 2024-01-14 was a Sunday
        var d = new Sharpy.Date(2024, 1, 14);

        d.Isoweekday().Should().Be(7);
    }

    [Fact]
    public void Date_Isoformat_ReturnsYYYYMMDD()
    {
        var d = new Sharpy.Date(2024, 3, 5);

        d.Isoformat().Should().Be("2024-03-05");
    }

    [Fact]
    public void Date_Replace_OnlyYear()
    {
        var d = new Sharpy.Date(2024, 6, 20);

        var result = d.Replace(year: 2025);

        result.Year.Should().Be(2025);
        result.Month.Should().Be(6);
        result.Day.Should().Be(20);
    }

    [Fact]
    public void Date_Replace_OnlyDay()
    {
        var d = new Sharpy.Date(2024, 6, 20);

        var result = d.Replace(day: 1);

        result.Year.Should().Be(2024);
        result.Month.Should().Be(6);
        result.Day.Should().Be(1);
    }

    [Fact]
    public void Date_Toordinal_Fromordinal_RoundTrip()
    {
        var original = new Sharpy.Date(2024, 6, 15);

        int ordinal = original.Toordinal();
        var restored = Sharpy.Date.Fromordinal(ordinal);

        restored.Year.Should().Be(original.Year);
        restored.Month.Should().Be(original.Month);
        restored.Day.Should().Be(original.Day);
    }

    [Fact]
    public void Date_Fromisoformat_ParsesCorrectly()
    {
        var d = Sharpy.Date.Fromisoformat("2024-01-15");

        d.Year.Should().Be(2024);
        d.Month.Should().Be(1);
        d.Day.Should().Be(15);
    }

    [Fact]
    public void Date_Strftime_YearMonthDay()
    {
        var d = new Sharpy.Date(2024, 3, 5);

        d.Strftime("%Y/%m/%d").Should().Be("2024/03/05");
    }

    [Fact]
    public void Date_Plus_Timedelta_IncrementDay()
    {
        var d = new Sharpy.Date(2024, 6, 30);

        var result = d + new Sharpy.Timedelta(days: 1);

        result.Year.Should().Be(2024);
        result.Month.Should().Be(7);
        result.Day.Should().Be(1);
    }

    [Fact]
    public void Date_Minus_Timedelta_DecrementDay()
    {
        var d = new Sharpy.Date(2024, 3, 1);

        var result = d - new Sharpy.Timedelta(days: 1);

        result.Year.Should().Be(2024);
        result.Month.Should().Be(2);
        result.Day.Should().Be(29); // 2024 is a leap year
    }

    [Fact]
    public void Date_Minus_Date_ReturnsTimedelta()
    {
        var d1 = new Sharpy.Date(2024, 2, 10);
        var d2 = new Sharpy.Date(2024, 1, 1);

        var td = d1 - d2;

        td.Days.Should().Be(40);
    }

    [Fact]
    public void Date_Comparison_GreaterThan()
    {
        var d1 = new Sharpy.Date(2024, 6, 15);
        var d2 = new Sharpy.Date(2024, 1, 1);

        (d1 > d2).Should().BeTrue();
    }

    [Fact]
    public void Date_Comparison_LessOrEqual()
    {
        var d1 = new Sharpy.Date(2024, 1, 1);
        var d2 = new Sharpy.Date(2024, 1, 1);

        (d1 <= d2).Should().BeTrue();
    }

    [Fact]
    public void Date_Comparison_GreaterOrEqual()
    {
        var d1 = new Sharpy.Date(2024, 6, 1);
        var d2 = new Sharpy.Date(2024, 1, 1);

        (d1 >= d2).Should().BeTrue();
    }

    [Fact]
    public void Date_Comparison_NotEqual()
    {
        var d1 = new Sharpy.Date(2024, 1, 1);
        var d2 = new Sharpy.Date(2024, 1, 2);

        (d1 != d2).Should().BeTrue();
    }

    [Fact]
    public void Date_LeapYear_Feb29_IsValid()
    {
        // 2024 is a leap year
        var act = () => new Sharpy.Date(2024, 2, 29);

        act.Should().NotThrow();
        var d = act();
        d.Month.Should().Be(2);
        d.Day.Should().Be(29);
    }

    [Fact]
    public void Date_NonLeapYear_Feb29_ThrowsArgumentOutOfRange()
    {
        // 2023 is not a leap year
        FluentActions.Invoking(() => new Sharpy.Date(2023, 2, 29))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}
