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

    // Owner ruling 2026-09-30 (P22b): Sharpy indentation is exactly 4 spaces per level, no tabs
    // (docs/language_specification/indentation.md) — `--indent N` with N != 4 and `--tabs` are usage
    // errors (exit 2, nothing written) instead of options that wrote a file that does not lex.
    private const string OverIndented = "def foo():\n        pass\n";

    [Theory]
    [InlineData("--indent 2", "--indent 2 is not supported")]
    [InlineData("--indent 8", "--indent 8 is not supported")]
    [InlineData("--tabs", "--tabs is not supported")]
    public void NonFourSpaceIndentation_IsUsageError_AndLeavesTheFileUntouched(string flag, string message)
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(OverIndented);

        var invocation = CliTestHarness.Invoke($"format \"{spy}\" {flag}");

        invocation.ExitCode.Should().Be(2);
        invocation.StdErr.Should().Contain(message);
        invocation.StdErr.Should().Contain("docs/language_specification/indentation.md");
        File.ReadAllText(spy).Should().Be(OverIndented);
    }

    [Fact]
    public void IndentFour_IsAccepted_AndWritesFourSpaces()
    {
        // Positive control for the usage error: the one supported width formats the file.
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(OverIndented);

        var invocation = CliTestHarness.Invoke($"format \"{spy}\" --indent 4");

        invocation.ExitCode.Should().Be(0);
        File.ReadAllText(spy).Should().Be("def foo():\n    pass\n");
    }
}
