using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

public class LspCommandTests
{
    [Fact]
    public void Lsp_IsRegistered_AndParses()
    {
        var result = CliTestHarness.Parse("lsp");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("lsp");
    }

    [Fact]
    public void Lsp_ParsesStdioFlag()
    {
        var result = CliTestHarness.Parse("lsp --stdio");

        result.Errors.Should().BeEmpty();
        result.GetValue<bool>("--stdio").Should().BeTrue();
    }

    [Fact]
    public void Lsp_StdioDefaultsToFalse_WhenAbsent()
    {
        var result = CliTestHarness.Parse("lsp");

        result.GetValue<bool>("--stdio").Should().BeFalse();
    }

    [Fact]
    public void Lsp_RejectsUnknownOption()
    {
        var result = CliTestHarness.Parse("lsp --pipe");

        result.Errors.Should().NotBeEmpty();
    }
}
