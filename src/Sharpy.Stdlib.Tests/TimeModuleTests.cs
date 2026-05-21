using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class TimeModuleTests
{
    [Fact]
    public void Time_ReturnsReasonableEpoch()
    {
        double t = TimeModule.Time();
        // Should be after 2023-11-14 (1700000000)
        t.Should().BeGreaterThan(1700000000.0);
    }

    [Fact]
    public void TimeNs_ReturnsReasonableEpoch()
    {
        long ns = TimeModule.TimeNs();
        ns.Should().BeGreaterThan(1700000000L * 1_000_000_000L);
    }

    [Fact]
    public void PerfCounter_IsMonotonic()
    {
        double first = TimeModule.PerfCounter();
        double second = TimeModule.PerfCounter();
        second.Should().BeGreaterThanOrEqualTo(first);
    }

    [Fact]
    public void PerfCounterNs_IsMonotonic()
    {
        long first = TimeModule.PerfCounterNs();
        long second = TimeModule.PerfCounterNs();
        second.Should().BeGreaterThanOrEqualTo(first);
    }

    [Fact]
    public void Monotonic_IsMonotonic()
    {
        double first = TimeModule.Monotonic();
        double second = TimeModule.Monotonic();
        second.Should().BeGreaterThanOrEqualTo(first);
    }

    [Fact]
    public void MonotonicNs_IsMonotonic()
    {
        long first = TimeModule.MonotonicNs();
        long second = TimeModule.MonotonicNs();
        second.Should().BeGreaterThanOrEqualTo(first);
    }

    [Theory]
    [InlineData("%Y", "yyyy")]
    [InlineData("%m", "MM")]
    [InlineData("%d", "dd")]
    [InlineData("%H", "HH")]
    [InlineData("%M", "mm")]
    [InlineData("%S", "ss")]
    [InlineData("%A", "dddd")]
    [InlineData("%B", "MMMM")]
    [InlineData("%a", "ddd")]
    [InlineData("%b", "MMM")]
    [InlineData("%p", "tt")]
    [InlineData("%I", "hh")]
    public void ConvertFormat_MapsKnownCodes(string pythonFormat, string dotnetFormat)
    {
        string result = TimeModule.ConvertFormat(pythonFormat);
        result.Should().Be(dotnetFormat);
    }

    [Fact]
    public void ConvertFormat_DoublePercent_ProducesLiteralPercent()
    {
        string result = TimeModule.ConvertFormat("%%");
        result.Should().Be("%");
    }

    [Fact]
    public void ConvertFormat_CompoundFormat()
    {
        string result = TimeModule.ConvertFormat("%Y-%m-%d");
        result.Should().Be("yyyy-MM-dd");
    }

    [Fact]
    public void ConvertFormat_EscapesLiteralLetters()
    {
        // The letter 'T' between date and time should be escaped
        string result = TimeModule.ConvertFormat("%Y-%m-%dT%H:%M:%S");
        result.Should().Be("yyyy-MM-dd\\THH:mm:ss");
    }

    [Fact]
    public void Strftime_ReturnsNonEmptyString()
    {
        string result = TimeModule.Strftime("%Y-%m-%d");
        result.Should().NotBeNullOrEmpty();
        // Should match pattern like 2024-01-15
        result.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
    }

    [Fact]
    public void Sleep_DoesNotThrow()
    {
        // Sleep for a very short duration
        var act = () => TimeModule.Sleep(0.001);
        act.Should().NotThrow();
    }

    [Fact]
    public void Sleep_NegativeValue_ThrowsValueError()
    {
        var act = () => TimeModule.Sleep(-1);
        act.Should().Throw<ValueError>()
            .WithMessage("sleep length must be non-negative");
    }

    [Fact]
    public void Sleep_Zero_DoesNotThrow()
    {
        var act = () => TimeModule.Sleep(0);
        act.Should().NotThrow();
    }

    [Fact]
    public void Gmtime_ReturnsValidStructTime()
    {
        var t = TimeModule.Gmtime();
        t.TmYear.Should().BeGreaterThanOrEqualTo(2024);
        t.TmMon.Should().BeInRange(1, 12);
        t.TmMday.Should().BeInRange(1, 31);
        t.TmHour.Should().BeInRange(0, 23);
        t.TmMin.Should().BeInRange(0, 59);
        t.TmSec.Should().BeInRange(0, 61);
        t.TmWday.Should().BeInRange(0, 6);
        t.TmYday.Should().BeInRange(1, 366);
        t.TmIsdst.Should().Be(0); // UTC never has DST
    }

    [Fact]
    public void Localtime_ReturnsValidStructTime()
    {
        var t = TimeModule.Localtime();
        t.TmYear.Should().BeGreaterThanOrEqualTo(2024);
        t.TmMon.Should().BeInRange(1, 12);
        t.TmMday.Should().BeInRange(1, 31);
        t.TmHour.Should().BeInRange(0, 23);
        t.TmMin.Should().BeInRange(0, 59);
        t.TmSec.Should().BeInRange(0, 61);
        t.TmWday.Should().BeInRange(0, 6);
        t.TmYday.Should().BeInRange(1, 366);
        t.TmIsdst.Should().BeInRange(-1, 1);
    }

    [Fact]
    public void StructTime_ToString_MatchesPythonFormat()
    {
        var t = new StructTime(2024, 1, 15, 10, 30, 0, 0, 15, 0);
        t.ToString().Should().Contain("tm_year=2024");
        t.ToString().Should().Contain("tm_mon=1");
        t.ToString().Should().StartWith("time.struct_time(");
    }

    [Fact]
    public void StructTime_Wday_MondayIsZero()
    {
        // Monday, January 1, 2024 = Monday = tm_wday 0
        var dt = new System.DateTime(2024, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        var t = StructTime.FromDateTime(dt);
        t.TmWday.Should().Be(0); // Monday = 0 in Python
    }

    [Fact]
    public void StructTime_Wday_SundayIsSix()
    {
        // Sunday, January 7, 2024 = Sunday = tm_wday 6
        var dt = new System.DateTime(2024, 1, 7, 0, 0, 0, System.DateTimeKind.Utc);
        var t = StructTime.FromDateTime(dt);
        t.TmWday.Should().Be(6); // Sunday = 6 in Python
    }

    [Fact]
    public void Gmtime_UnixEpoch_Returns1970()
    {
        // Python: time.gmtime(0) → time.struct_time(tm_year=1970, tm_mon=1, tm_mday=1, ...)
        var t = TimeModule.Gmtime(0.0);
        t.TmYear.Should().Be(1970);
        t.TmMon.Should().Be(1);
        t.TmMday.Should().Be(1);
        t.TmHour.Should().Be(0);
        t.TmMin.Should().Be(0);
        t.TmSec.Should().Be(0);
        t.TmWday.Should().Be(3); // Thursday
        t.TmYday.Should().Be(1);
        t.TmIsdst.Should().Be(0);
    }

    [Fact]
    public void Gmtime_OneDayAfterEpoch()
    {
        // Python: time.gmtime(86400) → 1970-01-02
        var t = TimeModule.Gmtime(86400.0);
        t.TmYear.Should().Be(1970);
        t.TmMon.Should().Be(1);
        t.TmMday.Should().Be(2);
        t.TmHour.Should().Be(0);
        t.TmMin.Should().Be(0);
        t.TmSec.Should().Be(0);
    }

    [Fact]
    public void Localtime_UnixEpoch_ReturnsValidStructTime()
    {
        // localtime(0) should return a valid struct_time in the local timezone
        // In timezones west of UTC, epoch 0 maps to Dec 31, 1969
        var t = TimeModule.Localtime(0.0);
        t.TmYear.Should().BeInRange(1969, 1970);
        t.TmMon.Should().BeInRange(1, 12);
        t.TmMday.Should().BeInRange(1, 31);
        t.TmHour.Should().BeInRange(0, 23);
    }

    [Fact]
    public void Gmtime_LargeTimestamp()
    {
        // Python: time.gmtime(1000000000) → 2001-09-09 01:46:40
        var t = TimeModule.Gmtime(1000000000.0);
        t.TmYear.Should().Be(2001);
        t.TmMon.Should().Be(9);
        t.TmMday.Should().Be(9);
        t.TmHour.Should().Be(1);
        t.TmMin.Should().Be(46);
        t.TmSec.Should().Be(40);
    }
}
