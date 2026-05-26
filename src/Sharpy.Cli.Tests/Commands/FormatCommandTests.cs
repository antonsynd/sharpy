using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

public class FormatCommandTests
{
    private const string Source = "x: int = 1\n";

    [Fact]
    public void Parses_CheckFlag()
    {
        var result = CliTestHarness.Parse("format main.spy --check");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("format");
        result.GetValue<bool>("--check").Should().BeTrue();
    }

    [Fact]
    public void Parses_DiffFlag()
    {
        var result = CliTestHarness.Parse("format main.spy --diff");

        result.Errors.Should().BeEmpty();
        result.GetValue<bool>("--diff").Should().BeTrue();
    }

    [Fact]
    public void Parses_IndentAndTabsOptions()
    {
        var result = CliTestHarness.Parse("format main.spy --indent 2 --tabs");

        result.Errors.Should().BeEmpty();
        result.GetValue<int?>("--indent").Should().Be(2);
        result.GetValue<bool>("--tabs").Should().BeTrue();
    }

    [Fact]
    public void RequiresInputArgument()
    {
        var result = CliTestHarness.Parse("format");

        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void CheckAndDiff_Combined_IsUsageError()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(Source);

        var invocation = CliTestHarness.Invoke($"format \"{spy}\" --check --diff");

        invocation.ExitCode.Should().Be(2);
        invocation.StdErr.Should().Contain("cannot be combined");
    }

    [Fact]
    public void MissingPath_IsUsageError()
    {
        using var ws = new TempWorkspace();
        var missing = ws.PathFor("does_not_exist.spy");

        var invocation = CliTestHarness.Invoke($"format \"{missing}\"");

        invocation.ExitCode.Should().Be(2);
        invocation.StdErr.Should().Contain("does not exist");
    }

    [Fact]
    public void OutputOption_RejectedForDirectoryInput()
    {
        using var ws = new TempWorkspace();
        var outPath = ws.PathFor("out.spy");

        var invocation = CliTestHarness.Invoke($"format \"{ws.Root}\" --output \"{outPath}\"");

        invocation.ExitCode.Should().Be(2);
        invocation.StdErr.Should().Contain("--output is only supported");
    }

    [Fact]
    public void Check_OnFormattedFile_ReportsClean()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(Source);

        // Normalize the file first (write mode), then verify --check considers it clean.
        var write = CliTestHarness.Invoke($"format \"{spy}\"");
        write.ExitCode.Should().Be(0);

        var check = CliTestHarness.Invoke($"format \"{spy}\" --check");

        check.ExitCode.Should().Be(0);
        check.StdOut.Should().Contain("All files are already formatted.");
    }
}
