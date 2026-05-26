using FluentAssertions;
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
        var invocation = CliTestHarness.Invoke("explain --list");

        invocation.ExitCode.Should().Be(0);
        invocation.StdOut.Should().Contain("Documented Diagnostic Codes:");
        invocation.StdOut.Should().Contain("Total:");
    }

    [Fact]
    public void SpecificCode_PrintsExplanation()
    {
        var invocation = CliTestHarness.Invoke("explain SPY0200");

        invocation.ExitCode.Should().Be(0);
        invocation.StdOut.Should().Contain("SPY0200");
        invocation.StdOut.Should().Contain("Category:");
    }

    [Fact]
    public void SpecificCode_IsCaseInsensitive()
    {
        var invocation = CliTestHarness.Invoke("explain spy0200");

        invocation.ExitCode.Should().Be(0);
        invocation.StdOut.Should().Contain("SPY0200");
    }
}
