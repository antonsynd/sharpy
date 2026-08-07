using FluentAssertions;
using Sharpy.Cli.Commands;
using Sharpy.Compiler.Logging;
using Sharpy.Lsp;
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

    [Fact]
    public void ServerArgs_AbsentLevel_StaysAbsent()
    {
        // An absent flag must stay absent: the server then applies its own resolution
        // (SHARPY_LSP_LOG_LEVEL, then Information) rather than receiving an explicit "None".
        LspCommand.BuildServerArgs(null).Should().BeEmpty();
    }

    [Fact]
    public void ServerArgs_PresentLevel_BecomesTheServersFlag()
    {
        LspCommand.BuildServerArgs(CompilerLogLevel.Debug)
            .Should().Equal(ServerCommandLine.LogLevelFlag, "Debug");
    }

    [Fact]
    public void ServerArgs_Info_MapsToTheServersInformation()
    {
        // The one spelling that differs between the two vocabularies (#1225).
        LspCommand.BuildServerArgs(CompilerLogLevel.Info)
            .Should().Equal(ServerCommandLine.LogLevelFlag, "Information");
    }

    [Theory]
    [InlineData(CompilerLogLevel.None)]
    [InlineData(CompilerLogLevel.Error)]
    [InlineData(CompilerLogLevel.Warning)]
    [InlineData(CompilerLogLevel.Info)]
    [InlineData(CompilerLogLevel.Debug)]
    [InlineData(CompilerLogLevel.Trace)]
    public void ServerArgs_EveryCliLevel_IsASpellingTheServerAccepts(CompilerLogLevel level)
    {
        // The load-bearing invariant of the forwarding seam: a level the CLI parsed must never
        // reach the server as a spelling it rejects — that would silently fall back to
        // Information with a stderr warning the user never asked for.
        var warnings = new StringWriter();

        var logging = ServerCommandLine.ResolveLogging(
            LspCommand.BuildServerArgs(level), environmentValue: null, warnings);

        logging.IsConfigured.Should().BeTrue();
        warnings.ToString().Should().BeEmpty(
            $"the CLI spelling for {level} must parse cleanly on the server side");
    }
}
