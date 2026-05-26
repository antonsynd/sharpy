using FluentAssertions;
using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Cli.Tests;

public class GlobalOptionsTests
{
    [Theory]
    [InlineData("None", CompilerLogLevel.None)]
    [InlineData("Error", CompilerLogLevel.Error)]
    [InlineData("Warning", CompilerLogLevel.Warning)]
    [InlineData("Info", CompilerLogLevel.Info)]
    [InlineData("Debug", CompilerLogLevel.Debug)]
    [InlineData("Trace", CompilerLogLevel.Trace)]
    public void LogLevel_ParsesAllEnumValues(string text, CompilerLogLevel expected)
    {
        var (result, globals) = CliTestHarness.ParseWithGlobals($"build a.spy --log-level {text}");

        result.Errors.Should().BeEmpty();
        result.GetValue(globals.LogLevel).Should().Be(expected);
    }

    [Fact]
    public void LogLevel_RejectsInvalidValue()
    {
        var result = CliTestHarness.Parse("build a.spy --log-level Verbose");

        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void LogLevel_DefaultsToNull_WhenAbsent()
    {
        var (result, globals) = CliTestHarness.ParseWithGlobals("build a.spy");

        result.Errors.Should().BeEmpty();
        result.GetValue(globals.LogLevel).Should().BeNull();
    }

    [Fact]
    public void LogFile_ParsesPath()
    {
        var (result, globals) = CliTestHarness.ParseWithGlobals("build a.spy --log-file out.log");

        result.Errors.Should().BeEmpty();
        result.GetValue(globals.LogFile)!.Name.Should().Be("out.log");
    }

    [Fact]
    public void WarnAsError_DefaultsToFalse()
    {
        var (result, globals) = CliTestHarness.ParseWithGlobals("build a.spy");

        result.GetValue(globals.WarnAsError).Should().BeFalse();
    }

    [Fact]
    public void WarnAsError_SetByFlag()
    {
        var (result, globals) = CliTestHarness.ParseWithGlobals("build a.spy --warn-as-error");

        result.Errors.Should().BeEmpty();
        result.GetValue(globals.WarnAsError).Should().BeTrue();
    }

    [Fact]
    public void Nowarn_ParsesCommaSeparatedString()
    {
        var (result, globals) = CliTestHarness.ParseWithGlobals("build a.spy --nowarn SPY0451,SPY0452");

        result.Errors.Should().BeEmpty();
        result.GetValue(globals.Nowarn).Should().Be("SPY0451,SPY0452");
    }

    [Fact]
    public void MaxErrors_ParsesInteger()
    {
        var (result, globals) = CliTestHarness.ParseWithGlobals("build a.spy --max-errors 50");

        result.Errors.Should().BeEmpty();
        result.GetValue(globals.MaxErrors).Should().Be(50);
    }

    [Fact]
    public void MaxErrors_RejectsNonInteger()
    {
        var result = CliTestHarness.Parse("build a.spy --max-errors notanumber");

        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("text")]
    [InlineData("json")]
    public void MetricsFormat_ParsesValue(string format)
    {
        var (result, globals) = CliTestHarness.ParseWithGlobals($"build a.spy --metrics-format {format}");

        result.Errors.Should().BeEmpty();
        result.GetValue(globals.MetricsFormat).Should().Be(format);
    }

    [Fact]
    public void MetricsOutput_ParsesPath()
    {
        var (result, globals) = CliTestHarness.ParseWithGlobals("build a.spy --metrics-output metrics.json");

        result.Errors.Should().BeEmpty();
        result.GetValue(globals.MetricsOutput)!.Name.Should().Be("metrics.json");
    }

    [Fact]
    public void GlobalOptions_AreRecursive_AndApplyToSubcommands()
    {
        // The same global option is accepted on a different subcommand.
        var (result, globals) = CliTestHarness.ParseWithGlobals("emit tokens a.spy --log-level Info");

        result.Errors.Should().BeEmpty();
        result.GetValue(globals.LogLevel).Should().Be(CompilerLogLevel.Info);
    }

    [Fact]
    public void MultipleGlobalOptions_ParseTogether()
    {
        var (result, globals) = CliTestHarness.ParseWithGlobals(
            "build a.spy --log-level Debug --warn-as-error --max-errors 5 --nowarn SPY0451");

        result.Errors.Should().BeEmpty();
        result.GetValue(globals.LogLevel).Should().Be(CompilerLogLevel.Debug);
        result.GetValue(globals.WarnAsError).Should().BeTrue();
        result.GetValue(globals.MaxErrors).Should().Be(5);
        result.GetValue(globals.Nowarn).Should().Be("SPY0451");
    }
}
