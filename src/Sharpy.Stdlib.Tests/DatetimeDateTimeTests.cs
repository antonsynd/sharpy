using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional DateTime and Timezone tests not covered by DatetimeTests.cs.
/// </summary>
public class DatetimeDateTimeTests
{
    // --- DateTime Constructor ---

    [Fact]
    public void DateTime_DateOnlyConstructor_TimesAreZero()
    {
        var dt = new Sharpy.DateTime(2024, 6, 15);

        dt.Year.Should().Be(2024);
        dt.Month.Should().Be(6);
        dt.Day.Should().Be(15);
        dt.Hour.Should().Be(0);
        dt.Minute.Should().Be(0);
        dt.Second.Should().Be(0);
        dt.Microsecond.Should().Be(0);
    }

    [Fact]
    public void DateTime_WithHourAndMinute_SecondDefaultsZero()
    {
        var dt = new Sharpy.DateTime(2024, 1, 1, 12, 30);

        dt.Hour.Should().Be(12);
        dt.Minute.Should().Be(30);
        dt.Second.Should().Be(0);
    }

    // --- Replace ---

    [Fact]
    public void DateTime_Replace_OnlyHour()
    {
        var dt = new Sharpy.DateTime(2024, 6, 15, 10, 30, 45);

        var result = dt.Replace(hour: 12);

        result.Year.Should().Be(2024);
        result.Month.Should().Be(6);
        result.Day.Should().Be(15);
        result.Hour.Should().Be(12);
        result.Minute.Should().Be(30);
        result.Second.Should().Be(45);
    }

    [Fact]
    public void DateTime_Replace_YearMonthDay()
    {
        var dt = new Sharpy.DateTime(2024, 6, 15, 10, 30, 45);

        var result = dt.Replace(year: 2025, month: 1, day: 1);

        result.Year.Should().Be(2025);
        result.Month.Should().Be(1);
        result.Day.Should().Be(1);
        result.Hour.Should().Be(10);
        result.Minute.Should().Be(30);
        result.Second.Should().Be(45);
    }

    // --- DateComponent and TimeComponent ---

    [Fact]
    public void DateTime_DateComponent_HasCorrectFields()
    {
        var dt = new Sharpy.DateTime(2024, 11, 22, 8, 15, 0);

        var date = dt.DateComponent;

        date.Year.Should().Be(2024);
        date.Month.Should().Be(11);
        date.Day.Should().Be(22);
    }

    [Fact]
    public void DateTime_TimeComponent_HasCorrectFields()
    {
        var dt = new Sharpy.DateTime(2024, 11, 22, 8, 15, 30, 123456);

        var time = dt.TimeComponent;

        time.Hour.Should().Be(8);
        time.Minute.Should().Be(15);
        time.Second.Should().Be(30);
        time.Microsecond.Should().Be(123456);
    }

    // --- Timestamp ---

    [Fact]
    public void DateTime_Timestamp_EpochIsZero()
    {
        // 1970-01-01 00:00:00 UTC -> timestamp = 0
        var utc = Sharpy.Timezone.Utc;
        var epoch = new Sharpy.DateTime(1970, 1, 1, 0, 0, 0, tzinfo: utc);

        epoch.Timestamp().Should().Be(0.0);
    }

    [Fact]
    public void DateTime_Timestamp_IsPositiveForRecentDates()
    {
        var utc = Sharpy.Timezone.Utc;
        var dt = new Sharpy.DateTime(2024, 1, 1, 0, 0, 0, tzinfo: utc);

        dt.Timestamp().Should().BeGreaterThan(0.0);
    }

    // --- Isoformat ---

    [Fact]
    public void DateTime_Isoformat_WithMicroseconds()
    {
        var dt = new Sharpy.DateTime(2024, 6, 15, 10, 30, 45, 123456);

        dt.Isoformat().Should().Be("2024-06-15T10:30:45.123456");
    }

    [Fact]
    public void DateTime_Isoformat_WithTimezone_IncludesOffset()
    {
        var utc = Sharpy.Timezone.Utc;
        var dt = new Sharpy.DateTime(2024, 1, 1, 12, 0, 0, tzinfo: utc);

        dt.Isoformat().Should().Contain("+00:00");
    }

    [Fact]
    public void DateTime_Isoformat_CustomSeparatorSpace()
    {
        var dt = new Sharpy.DateTime(2024, 6, 15, 10, 30, 0);

        dt.Isoformat(" ").Should().Be("2024-06-15 10:30:00");
    }

    // --- Comparison ---

    [Fact]
    public void DateTime_Comparison_LessOrEqual()
    {
        var dt1 = new Sharpy.DateTime(2024, 1, 1);
        var dt2 = new Sharpy.DateTime(2024, 1, 1);

        (dt1 <= dt2).Should().BeTrue();
    }

    [Fact]
    public void DateTime_Comparison_GreaterOrEqual()
    {
        var dt1 = new Sharpy.DateTime(2024, 6, 1);
        var dt2 = new Sharpy.DateTime(2024, 1, 1);

        (dt1 >= dt2).Should().BeTrue();
    }

    // --- Astimezone ---

    [Fact]
    public void DateTime_Astimezone_PositiveOffset()
    {
        var utc = Sharpy.Timezone.Utc;
        var ist = new Sharpy.Timezone(new Sharpy.Timedelta(hours: 5, minutes: 30), "IST");
        var dt_utc = new Sharpy.DateTime(2024, 1, 15, 0, 0, 0, tzinfo: utc);

        var dt_ist = dt_utc.Astimezone(ist);

        dt_ist.Hour.Should().Be(5);
        dt_ist.Minute.Should().Be(30);
    }

    // --- Timezone ---

    [Fact]
    public void Timezone_Utc_StaticProperty_IsUTC()
    {
        var utc = Sharpy.Timezone.Utc;

        utc.Tzname().Should().Be("UTC");
        utc.Utcoffset().TotalSeconds.Should().Be(0.0);
    }

    [Fact]
    public void Timezone_PositiveOffset_NoName()
    {
        var tz = new Sharpy.Timezone(new Sharpy.Timedelta(hours: 5));

        tz.Utcoffset().TotalSeconds.Should().Be(18000.0);
    }

    [Fact]
    public void Timezone_NegativeOffset_WithName()
    {
        var pst = new Sharpy.Timezone(new Sharpy.Timedelta(hours: -8), "PST");

        pst.Tzname().Should().Be("PST");
        pst.Utcoffset().TotalSeconds.Should().Be(-28800.0);
    }

    [Fact]
    public void DateTime_WithTimezone_HasCorrectTzinfo()
    {
        var utc = Sharpy.Timezone.Utc;
        var dt = new Sharpy.DateTime(2024, 1, 15, 12, 0, 0, tzinfo: utc);

        dt.Tzinfo.Should().Be(utc);
    }

    [Fact]
    public void DateTime_WithoutTimezone_TzinfoIsNull()
    {
        var dt = new Sharpy.DateTime(2024, 1, 15, 12, 0, 0);

        dt.Tzinfo.Should().BeNull();
    }
}
