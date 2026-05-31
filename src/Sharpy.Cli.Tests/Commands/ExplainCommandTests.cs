using FluentAssertions;
using Sharpy.Cli.Commands;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

public class ExplainCommandTests
{
    [Fact]
    public void Parses_WithCodeArgument()
    {
        var result = CliTestHarness.Parse("explain SPY0200");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("explain");
        result.GetValue<string?>("code").Should().Be("SPY0200");
    }

    [Fact]
    public void Parses_WithoutCode_ArgumentIsOptional()
    {
        var result = CliTestHarness.Parse("explain --list");

        result.Errors.Should().BeEmpty();
        result.GetValue<bool>("--list").Should().BeTrue();
    }

    [Fact]
    public void List_PrintsDocumentedCodes()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = ExplainCommand.HandleExplainCommand(null, list: true, stdout, stderr);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Documented Diagnostic Codes:");
        stdout.ToString().Should().Contain("Total:");
    }

    [Fact]
    public void SpecificCode_PrintsExplanation()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = ExplainCommand.HandleExplainCommand("SPY0200", list: false, stdout, stderr);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("SPY0200");
        stdout.ToString().Should().Contain("Category:");
    }

    [Fact]
    public void SpecificCode_IsCaseInsensitive()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = ExplainCommand.HandleExplainCommand("spy0200", list: false, stdout, stderr);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("SPY0200");
    }

    // ---- Invocation-level error tests ----

    [Fact]
    public void UnknownCode_ReturnsExitCode1()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = ExplainCommand.HandleExplainCommand("SPY9999", list: false, stdout, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("No explanation found");
    }

    [Fact]
    public void NoCodeAndNoList_ReturnsExitCode1()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = ExplainCommand.HandleExplainCommand(null, list: false, stdout, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Usage:");
    }
}
