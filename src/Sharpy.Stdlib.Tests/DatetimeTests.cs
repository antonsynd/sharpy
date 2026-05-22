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

    // --- Timedelta Seconds (Python semantics) ---

    [Fact]
    public void Timedelta_Seconds_ReturnsRemainingSecondsAfterDays()
    {
        // Python: timedelta(days=1, hours=2, minutes=3, seconds=4).seconds == 7384
        var td = new Sharpy.Timedelta(days: 1, hours: 2, minutes: 3, seconds: 4);

        td.Seconds.Should().Be(7384);
    }

    // --- DateTime Arithmetic ---

    [Fact]
    public void DateTime_Plus_Timedelta_ReturnsNewDateTime()
    {
        var dt = new Sharpy.DateTime(2024, 1, 1);
        var td = new Sharpy.Timedelta(days: 1);

        var result = dt + td;

        result.Year.Should().Be(2024);
        result.Month.Should().Be(1);
        result.Day.Should().Be(2);
    }

    [Fact]
    public void DateTime_Minus_Timedelta_ReturnsNewDateTime()
    {
        var dt = new Sharpy.DateTime(2024, 1, 5);
        var td = new Sharpy.Timedelta(days: 3);

        var result = dt - td;

        result.Day.Should().Be(2);
    }

    [Fact]
    public void DateTime_Minus_DateTime_ReturnsTimedelta()
    {
        var dt1 = new Sharpy.DateTime(2024, 1, 5);
        var dt2 = new Sharpy.DateTime(2024, 1, 1);

        var result = dt1 - dt2;

        result.Days.Should().Be(4);
        result.Seconds.Should().Be(0);
    }

    // --- Date Arithmetic ---

    [Fact]
    public void Date_Plus_Timedelta_ReturnsNewDate()
    {
        var date = new Sharpy.Date(2024, 1, 1);
        var td = new Sharpy.Timedelta(days: 10);

        var result = date + td;

        result.Day.Should().Be(11);
    }

    [Fact]
    public void Date_Minus_Timedelta_ReturnsNewDate()
    {
        var date = new Sharpy.Date(2024, 1, 15);
        var td = new Sharpy.Timedelta(days: 5);

        var result = date - td;

        result.Day.Should().Be(10);
    }

    [Fact]
    public void Date_Minus_Date_ReturnsTimedelta()
    {
        var d1 = new Sharpy.Date(2024, 1, 15);
        var d2 = new Sharpy.Date(2024, 1, 1);

        var result = d1 - d2;

        result.Days.Should().Be(14);
    }

    // --- Timedelta Arithmetic ---

    [Fact]
    public void Timedelta_Plus_Timedelta()
    {
        var td1 = new Sharpy.Timedelta(days: 1);
        var td2 = new Sharpy.Timedelta(hours: 12);

        var result = td1 + td2;

        result.Days.Should().Be(1);
        result.Seconds.Should().Be(43200);
    }

    [Fact]
    public void Timedelta_Minus_Timedelta()
    {
        var result = new Sharpy.Timedelta(days: 5) - new Sharpy.Timedelta(days: 2);

        result.Days.Should().Be(3);
    }

    [Fact]
    public void Timedelta_Multiply_Int()
    {
        var result = new Sharpy.Timedelta(days: 1) * 3;

        result.Days.Should().Be(3);
    }

    [Fact]
    public void Int_Multiply_Timedelta()
    {
        var result = 3 * new Sharpy.Timedelta(days: 1);

        result.Days.Should().Be(3);
    }

    [Fact]
    public void Timedelta_Negate()
    {
        var td = new Sharpy.Timedelta(days: 1);

        var result = -td;

        result.Days.Should().Be(-1);
    }

    [Fact]
    public void Timedelta_Divide_Int()
    {
        var result = new Sharpy.Timedelta(days: 10) / 3;

        result.Days.Should().Be(3);
        result.Seconds.Should().Be(28800); // 8 hours
    }

    [Fact]
    public void Timedelta_Abs()
    {
        var td = new Sharpy.Timedelta(days: -5);

        var result = td.Abs();

        result.Days.Should().Be(5);
    }

    // --- Comparison ---

    [Fact]
    public void DateTime_Comparison_Operators()
    {
        var dt1 = new Sharpy.DateTime(2024, 1, 1);
        var dt2 = new Sharpy.DateTime(2024, 1, 2);
        var dt3 = new Sharpy.DateTime(2024, 1, 1);

        (dt1 < dt2).Should().BeTrue();
        (dt2 > dt1).Should().BeTrue();
        (dt1 == dt3).Should().BeTrue();
        (dt1 != dt2).Should().BeTrue();
        (dt1 <= dt3).Should().BeTrue();
        (dt2 >= dt1).Should().BeTrue();
    }

    [Fact]
    public void Date_Comparison_Operators()
    {
        var d1 = new Sharpy.Date(2024, 1, 1);
        var d2 = new Sharpy.Date(2024, 1, 2);

        (d1 < d2).Should().BeTrue();
        (d1 == new Sharpy.Date(2024, 1, 1)).Should().BeTrue();
    }

    [Fact]
    public void Time_Comparison_Operators()
    {
        var t1 = new Sharpy.Time(10, 0);
        var t2 = new Sharpy.Time(12, 0);

        (t1 < t2).Should().BeTrue();
        (t2 > t1).Should().BeTrue();
    }

    [Fact]
    public void Timedelta_Comparison_Operators()
    {
        var td1 = new Sharpy.Timedelta(days: 1);
        var td2 = new Sharpy.Timedelta(hours: 12);

        (td1 > td2).Should().BeTrue();
    }

    // --- Strftime ---

    [Fact]
    public void DateTime_Strftime_BasicFormat()
    {
        var dt = new Sharpy.DateTime(2024, 1, 15, 14, 30, 45);

        ((string)dt.Strftime("%Y-%m-%d")).Should().Be("2024-01-15");
    }

    [Fact]
    public void DateTime_Strftime_DayNames()
    {
        var dt = new Sharpy.DateTime(2024, 1, 15); // Monday

        ((string)dt.Strftime("%A")).Should().Be("Monday");
    }

    [Fact]
    public void DateTime_Strftime_PercentLiteral()
    {
        var dt = new Sharpy.DateTime(2024, 1, 15);

        ((string)dt.Strftime("100%%")).Should().Be("100%");
    }

    [Fact]
    public void Date_Strftime()
    {
        var d = new Sharpy.Date(2024, 1, 15);

        ((string)d.Strftime("%Y-%m-%d")).Should().Be("2024-01-15");
    }

    // --- Strptime ---

    [Fact]
    public void DateTime_Strptime_ParsesDate()
    {
        var dt = Sharpy.DateTime.Strptime("2024-01-15", "%Y-%m-%d");

        dt.Year.Should().Be(2024);
        dt.Month.Should().Be(1);
        dt.Day.Should().Be(15);
    }

    // --- Utility Methods ---

    [Fact]
    public void DateTime_Weekday_MondayIsZero()
    {
        // 2024-01-15 is Monday
        var dt = new Sharpy.DateTime(2024, 1, 15);

        dt.Weekday().Should().Be(0);
    }

    [Fact]
    public void DateTime_Isoweekday_MondayIsOne()
    {
        var dt = new Sharpy.DateTime(2024, 1, 15);

        dt.Isoweekday().Should().Be(1);
    }

    [Fact]
    public void DateTime_Isoformat_Default()
    {
        var dt = new Sharpy.DateTime(2024, 1, 15);

        ((string)dt.Isoformat()).Should().Be("2024-01-15T00:00:00");
    }

    [Fact]
    public void DateTime_Isoformat_CustomSep()
    {
        var dt = new Sharpy.DateTime(2024, 1, 15);

        ((string)dt.Isoformat(" ")).Should().Be("2024-01-15 00:00:00");
    }

    [Fact]
    public void DateTime_Replace()
    {
        var dt = new Sharpy.DateTime(2024, 1, 15, 10, 30);

        var result = dt.Replace(year: 2025);

        result.Year.Should().Be(2025);
        result.Month.Should().Be(1);
        result.Day.Should().Be(15);
        result.Hour.Should().Be(10);
        result.Minute.Should().Be(30);
    }

    [Fact]
    public void DateTime_Fromisoformat()
    {
        var dt = Sharpy.DateTime.Fromisoformat("2024-01-15T14:30:00");

        dt.Year.Should().Be(2024);
        dt.Hour.Should().Be(14);
        dt.Minute.Should().Be(30);
    }

    [Fact]
    public void Date_Weekday_MondayIsZero()
    {
        var d = new Sharpy.Date(2024, 1, 15);

        d.Weekday().Should().Be(0);
    }

    [Fact]
    public void Date_Isoformat()
    {
        var d = new Sharpy.Date(2024, 1, 15);

        ((string)d.Isoformat()).Should().Be("2024-01-15");
    }

    [Fact]
    public void Date_Toordinal()
    {
        var d = new Sharpy.Date(2024, 1, 15);

        d.Toordinal().Should().Be(738900);
    }

    [Fact]
    public void Date_Fromordinal()
    {
        var d = Sharpy.Date.Fromordinal(738900);

        d.Year.Should().Be(2024);
        d.Month.Should().Be(1);
        d.Day.Should().Be(15);
    }

    [Fact]
    public void Date_Fromisoformat()
    {
        var d = Sharpy.Date.Fromisoformat("2024-01-15");

        d.Year.Should().Be(2024);
        d.Month.Should().Be(1);
        d.Day.Should().Be(15);
    }

    [Fact]
    public void Date_Replace()
    {
        var d = new Sharpy.Date(2024, 1, 15);

        var result = d.Replace(month: 6);

        result.Year.Should().Be(2024);
        result.Month.Should().Be(6);
        result.Day.Should().Be(15);
    }

    // --- Timezone ---

    [Fact]
    public void Timezone_Utc_HasZeroOffset()
    {
        var utc = Sharpy.Timezone.Utc;

        utc.Utcoffset().Days.Should().Be(0);
        utc.Utcoffset().Seconds.Should().Be(0);
        ((string)utc.Tzname()).Should().Be("UTC");
    }

    [Fact]
    public void Timezone_Custom_HasCorrectOffset()
    {
        var est = new Sharpy.Timezone(new Sharpy.Timedelta(hours: -5), "EST");

        est.Utcoffset().TotalSeconds.Should().Be(-18000);
        ((string)est.Tzname()).Should().Be("EST");
    }

    [Fact]
    public void DateTime_Astimezone_ConvertsCorrectly()
    {
        var utc = Sharpy.Timezone.Utc;
        var est = new Sharpy.Timezone(new Sharpy.Timedelta(hours: -5), "EST");
        var dt_utc = new Sharpy.DateTime(2024, 1, 15, 12, 0, 0, tzinfo: utc);

        var dt_est = dt_utc.Astimezone(est);

        dt_est.Hour.Should().Be(7);
        dt_est.Tzinfo.Should().Be(est);
    }

    [Fact]
    public void DateTime_WithTimezone_ToString_IncludesOffset()
    {
        var utc = Sharpy.Timezone.Utc;
        var dt = new Sharpy.DateTime(2024, 1, 15, 12, 0, 0, tzinfo: utc);

        dt.ToString().Should().Contain("+00:00");
    }

    // --- Edge Cases ---

    [Fact]
    public void DateTime_LeapYear_Feb29()
    {
        var dt = new Sharpy.DateTime(2024, 2, 29);
        var result = dt + new Sharpy.Timedelta(days: 1);

        result.Month.Should().Be(3);
        result.Day.Should().Be(1);
    }

    [Fact]
    public void Date_CrossMonthBoundary()
    {
        var d = new Sharpy.Date(2024, 1, 31);
        var result = d + new Sharpy.Timedelta(days: 1);

        result.Month.Should().Be(2);
        result.Day.Should().Be(1);
    }

    [Fact]
    public void DateTime_MidnightCrossing()
    {
        var dt = new Sharpy.DateTime(2024, 1, 15, 23, 30, 0);
        var result = dt + new Sharpy.Timedelta(hours: 1);

        result.Day.Should().Be(16);
        result.Hour.Should().Be(0);
        result.Minute.Should().Be(30);
    }

    [Fact]
    public void Time_Isoformat_WithoutMicroseconds()
    {
        var t = new Sharpy.Time(14, 30, 0);

        ((string)t.Isoformat()).Should().Be("14:30:00");
    }

    [Fact]
    public void Time_Isoformat_WithMicroseconds()
    {
        var t = new Sharpy.Time(14, 30, 0, 123456);

        ((string)t.Isoformat()).Should().Be("14:30:00.123456");
    }

    // --- Module ---

    [Fact]
    public void DatetimeModule_ExposesExpectedTypes()
    {
        Sharpy.Datetime.DateType.Should().Be(typeof(Sharpy.Date));
        Sharpy.Datetime.TimeType.Should().Be(typeof(Sharpy.Time));
        Sharpy.Datetime.DateTimeType.Should().Be(typeof(Sharpy.DateTime));
        Sharpy.Datetime.TimedeltaType.Should().Be(typeof(Sharpy.Timedelta));
        Sharpy.Datetime.TimezoneType.Should().Be(typeof(Sharpy.Timezone));
    }
}
