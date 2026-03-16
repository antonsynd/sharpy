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
}
