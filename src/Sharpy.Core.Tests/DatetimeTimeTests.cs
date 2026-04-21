using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional Time and Timedelta tests not covered by DatetimeTests.cs.
/// </summary>
public class DatetimeTimeTests
{
    // --- Time ---

    [Fact]
    public void Time_HourOnly_OtherDefaultToZero()
    {
        var t = new Sharpy.Time(14);

        t.Hour.Should().Be(14);
        t.Minute.Should().Be(0);
        t.Second.Should().Be(0);
        t.Microsecond.Should().Be(0);
    }

    [Fact]
    public void Time_MaximumValue_IsValid()
    {
        var t = new Sharpy.Time(23, 59, 59, 999999);

        t.Hour.Should().Be(23);
        t.Minute.Should().Be(59);
        t.Second.Should().Be(59);
        t.Microsecond.Should().Be(999999);
    }

    [Fact]
    public void Time_MinimumValue_IsZero()
    {
        var t = new Sharpy.Time(0, 0, 0, 0);

        t.Hour.Should().Be(0);
        t.Minute.Should().Be(0);
        t.Second.Should().Be(0);
        t.Microsecond.Should().Be(0);
    }

    [Fact]
    public void Time_Isoformat_WithoutMicroseconds_HasNoDecimal()
    {
        var t = new Sharpy.Time(10, 30, 0);

        t.Isoformat().Should().Be("10:30:00");
        t.Isoformat().Should().NotContain(".");
    }

    [Fact]
    public void Time_Isoformat_WithMicroseconds_HasSixDecimalDigits()
    {
        var t = new Sharpy.Time(10, 30, 0, 5);

        t.Isoformat().Should().Be("10:30:00.000005");
    }

    [Fact]
    public void Time_Strftime_HoursMinutesSeconds()
    {
        var t = new Sharpy.Time(14, 30, 45);

        t.Strftime("%H:%M:%S").Should().Be("14:30:45");
    }

    [Fact]
    public void Time_Strftime_TwelveHourClock()
    {
        var t = new Sharpy.Time(14, 30, 0);

        t.Strftime("%I:%M %p").Should().Be("02:30 PM");
    }

    [Fact]
    public void Time_Comparison_GreaterThan()
    {
        var t1 = new Sharpy.Time(12, 0, 0);
        var t2 = new Sharpy.Time(11, 59, 59);

        (t1 > t2).Should().BeTrue();
    }

    [Fact]
    public void Time_Comparison_Equal()
    {
        var t1 = new Sharpy.Time(10, 30, 0, 123456);
        var t2 = new Sharpy.Time(10, 30, 0, 123456);

        (t1 == t2).Should().BeTrue();
    }

    [Fact]
    public void Time_Comparison_NotEqual()
    {
        var t1 = new Sharpy.Time(10, 0, 0);
        var t2 = new Sharpy.Time(10, 0, 1);

        (t1 != t2).Should().BeTrue();
    }

    [Fact]
    public void Time_Comparison_LessOrEqual()
    {
        var t1 = new Sharpy.Time(10, 0, 0);
        var t2 = new Sharpy.Time(10, 0, 0);

        (t1 <= t2).Should().BeTrue();
    }

    [Fact]
    public void Time_Comparison_GreaterOrEqual()
    {
        var t1 = new Sharpy.Time(12, 0, 0);
        var t2 = new Sharpy.Time(12, 0, 0);

        (t1 >= t2).Should().BeTrue();
    }

    // --- Timedelta ---

    [Fact]
    public void Timedelta_OneDayInSeconds_Is86400()
    {
        var td = new Sharpy.Timedelta(days: 1);

        td.TotalSeconds.Should().Be(86400.0);
    }

    [Fact]
    public void Timedelta_3600Seconds_IsOneHour()
    {
        var td = new Sharpy.Timedelta(seconds: 3600);

        td.TotalSeconds.Should().Be(3600.0);
        td.Days.Should().Be(0);
        td.Seconds.Should().Be(3600);
    }

    [Fact]
    public void Timedelta_1000000Microseconds_IsOneSecond()
    {
        var td = new Sharpy.Timedelta(microseconds: 1000000);

        td.TotalSeconds.Should().Be(1.0);
        td.Days.Should().Be(0);
        td.Seconds.Should().Be(1);
        td.Microseconds.Should().Be(0);
    }

    [Fact]
    public void Timedelta_90Seconds_NormalizesDaysAndSeconds()
    {
        var td = new Sharpy.Timedelta(seconds: 90);

        td.Days.Should().Be(0);
        td.Seconds.Should().Be(90);
    }

    [Fact]
    public void Timedelta_90000Seconds_OverflowsIntoDays()
    {
        // 90000 seconds = 1 day + 3600 seconds
        var td = new Sharpy.Timedelta(seconds: 90000);

        td.Days.Should().Be(1);
        td.Seconds.Should().Be(3600);
        td.TotalSeconds.Should().Be(90000.0);
    }

    [Fact]
    public void Timedelta_NegativeDay_IsValid()
    {
        var td = new Sharpy.Timedelta(days: -1);

        td.Days.Should().Be(-1);
        td.TotalSeconds.Should().Be(-86400.0);
    }

    [Fact]
    public void Timedelta_Subtract_ProducesPositiveResult()
    {
        var td = new Sharpy.Timedelta(days: 2) - new Sharpy.Timedelta(days: 1);

        td.Days.Should().Be(1);
        td.TotalSeconds.Should().Be(86400.0);
    }

    [Fact]
    public void Timedelta_Comparison_Equal()
    {
        var td1 = new Sharpy.Timedelta(days: 1);
        var td2 = new Sharpy.Timedelta(hours: 24);

        (td1 == td2).Should().BeTrue();
    }

    [Fact]
    public void Timedelta_Comparison_NotEqual()
    {
        var td1 = new Sharpy.Timedelta(days: 1);
        var td2 = new Sharpy.Timedelta(days: 2);

        (td1 != td2).Should().BeTrue();
    }

    [Fact]
    public void Timedelta_Comparison_LessOrEqual()
    {
        var td1 = new Sharpy.Timedelta(hours: 12);
        var td2 = new Sharpy.Timedelta(days: 1);

        (td1 <= td2).Should().BeTrue();
    }

    [Fact]
    public void Timedelta_Comparison_GreaterOrEqual()
    {
        var td1 = new Sharpy.Timedelta(days: 1);
        var td2 = new Sharpy.Timedelta(hours: 12);

        (td1 >= td2).Should().BeTrue();
    }

    [Fact]
    public void Timedelta_NegativeTotalSeconds_IsNegative()
    {
        var td = new Sharpy.Timedelta(days: -2);

        td.TotalSeconds.Should().BeLessThan(0);
        td.TotalSeconds.Should().Be(-172800.0);
    }

    [Fact]
    public void Timedelta_ZeroDelta_AllComponentsZero()
    {
        var td = new Sharpy.Timedelta();

        td.Days.Should().Be(0);
        td.Seconds.Should().Be(0);
        td.Microseconds.Should().Be(0);
        td.TotalSeconds.Should().Be(0.0);
    }
}
