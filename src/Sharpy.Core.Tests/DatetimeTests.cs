using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Datetime_Tests
{
    // --- Date ---

    [Theory]
    [InlineData(2024, 1, 15)]
    [InlineData(2000, 12, 31)]
    [InlineData(1970, 1, 1)]
    public void Date_Constructor_SetsYearMonthDay(int year, int month, int day)
    {
        var date = new Sharpy.Date(year, month, day);

        date.Year.Should().Be(year);
        date.Month.Should().Be(month);
        date.Day.Should().Be(day);
    }

    [Fact]
    public void Date_ToString_ReturnsIsoFormat()
    {
        var date = new Sharpy.Date(2024, 1, 15);

        date.ToString().Should().Be("2024-01-15");
    }

    [Fact]
    public void Date_Today_ReturnsCurrentDate()
    {
        var today = Sharpy.Date.Today();

        today.Year.Should().BeGreaterThan(2020);
        today.Month.Should().BeInRange(1, 12);
        today.Day.Should().BeInRange(1, 31);
    }

    [Fact]
    public void Date_InvalidDate_ThrowsArgumentOutOfRange()
    {
        FluentActions.Invoking(() => new Sharpy.Date(2024, 13, 1))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    // --- Time ---

    [Theory]
    [InlineData(14, 30, 0, 0)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(23, 59, 59, 999999)]
    public void Time_Constructor_SetsComponents(int hour, int minute, int second, int microsecond)
    {
        var time = new Sharpy.Time(hour, minute, second, microsecond);

        time.Hour.Should().Be(hour);
        time.Minute.Should().Be(minute);
        time.Second.Should().Be(second);
        time.Microsecond.Should().Be(microsecond);
    }

    [Fact]
    public void Time_DefaultParameters_AreZero()
    {
        var time = new Sharpy.Time();

        time.Hour.Should().Be(0);
        time.Minute.Should().Be(0);
        time.Second.Should().Be(0);
        time.Microsecond.Should().Be(0);
    }

    [Fact]
    public void Time_ToString_ReturnsFormattedString()
    {
        var time = new Sharpy.Time(14, 30, 0, 0);

        time.ToString().Should().Be("14:30:00.000000");
    }

    // --- DateTime ---

    [Fact]
    public void DateTime_Constructor_SetsAllComponents()
    {
        var dt = new Sharpy.DateTime(2024, 6, 15, 10, 30, 45, 123456);

        dt.Year.Should().Be(2024);
        dt.Month.Should().Be(6);
        dt.Day.Should().Be(15);
        dt.Hour.Should().Be(10);
        dt.Minute.Should().Be(30);
        dt.Second.Should().Be(45);
        dt.Microsecond.Should().Be(123456);
    }

    [Fact]
    public void DateTime_DefaultTimeParameters_AreZero()
    {
        var dt = new Sharpy.DateTime(2024, 1, 1);

        dt.Hour.Should().Be(0);
        dt.Minute.Should().Be(0);
        dt.Second.Should().Be(0);
        dt.Microsecond.Should().Be(0);
    }

    [Fact]
    public void DateTime_Now_ReturnsCurrentDatetime()
    {
        var now = Sharpy.DateTime.Now();

        now.Year.Should().BeGreaterThan(2020);
    }

    [Fact]
    public void DateTime_Utcnow_ReturnsUtcDatetime()
    {
        var utcNow = Sharpy.DateTime.Utcnow();

        utcNow.Year.Should().BeGreaterThan(2020);
    }

    [Fact]
    public void DateTime_DateComponent_ReturnsDate()
    {
        var dt = new Sharpy.DateTime(2024, 3, 20, 14, 30, 0);

        var date = dt.DateComponent;

        date.Year.Should().Be(2024);
        date.Month.Should().Be(3);
        date.Day.Should().Be(20);
    }

    [Fact]
    public void DateTime_TimeComponent_ReturnsTime()
    {
        var dt = new Sharpy.DateTime(2024, 3, 20, 14, 30, 45);

        var time = dt.TimeComponent;

        time.Hour.Should().Be(14);
        time.Minute.Should().Be(30);
        time.Second.Should().Be(45);
    }

    [Fact]
    public void DateTime_Combine_MergesDateAndTime()
    {
        var date = new Sharpy.Date(2024, 6, 15);
        var time = new Sharpy.Time(10, 30, 45);

        var dt = Sharpy.DateTime.Combine(date, time);

        dt.Year.Should().Be(2024);
        dt.Month.Should().Be(6);
        dt.Day.Should().Be(15);
        dt.Hour.Should().Be(10);
        dt.Minute.Should().Be(30);
        dt.Second.Should().Be(45);
    }

    // --- Timedelta ---

    [Fact]
    public void Timedelta_Days_ReturnsCorrectValue()
    {
        var td = new Sharpy.Timedelta(days: 5);

        td.Days.Should().Be(5);
    }

    [Fact]
    public void Timedelta_Seconds_ReturnsSecondsComponent()
    {
        var td = new Sharpy.Timedelta(seconds: 30);

        td.Seconds.Should().Be(30);
    }

    [Fact]
    public void Timedelta_Microseconds_ReturnsCorrectValue()
    {
        var td = new Sharpy.Timedelta(microseconds: 500);

        td.Microseconds.Should().Be(500);
    }

    [Fact]
    public void Timedelta_Weeks_ConvertsTodays()
    {
        var td = new Sharpy.Timedelta(weeks: 2);

        td.Days.Should().Be(14);
    }

    [Fact]
    public void Timedelta_TotalSeconds_ReturnsCombinedSeconds()
    {
        var td = new Sharpy.Timedelta(days: 1, hours: 1, minutes: 1, seconds: 1);

        // 86400 + 3600 + 60 + 1 = 90061
        td.TotalSeconds.Should().Be(90061);
    }

    [Fact]
    public void Timedelta_DefaultParameters_AreZero()
    {
        var td = new Sharpy.Timedelta();

        td.Days.Should().Be(0);
        td.Seconds.Should().Be(0);
        td.Microseconds.Should().Be(0);
        td.TotalSeconds.Should().Be(0);
    }

    // --- Module ---

    [Fact]
    public void DatetimeModule_ExposesExpectedTypes()
    {
        Sharpy.Datetime.DateType.Should().Be(typeof(Sharpy.Date));
        Sharpy.Datetime.TimeType.Should().Be(typeof(Sharpy.Time));
        Sharpy.Datetime.DateTimeType.Should().Be(typeof(Sharpy.DateTime));
        Sharpy.Datetime.TimedeltaType.Should().Be(typeof(Sharpy.Timedelta));
    }
}
