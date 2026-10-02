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

    [Fact]
    public void DeclinedFormatting_PrintsSpy0912Once_AndLeavesTheFileUnchanged()
    {
        // P22b: a program the unparser cannot write back unchanged is declined. The damaged shape is
        // a still-open #2169 cell — a constraint intersection `[T: A & B]` the formatter writes as
        // something that does not re-parse. (Until #2166 the cell was `case `_`:`, whose escape the
        // parser ignored; it now formats — see the twin below. When #2169's intersection cell is
        // fixed this test needs another damaged shape.) The CLI prints the refusal with its code on
        // the error line (once — not repeated as a sub-bullet) and exits 2.
        using var ws = new TempWorkspace();
        var source = "def f[T: A & B](x: T) -> T:\n    return x\n";
        var spy = ws.WriteSpy(source);

        var invocation = CliTestHarness.Invoke($"format \"{spy}\"");

        invocation.ExitCode.Should().Be(2);
        invocation.StdErr.Should().Contain(
            "SPY0912: formatting declined: the output would not re-parse (first error at formatted line 1: SPY0104 Expected RightBracket, got Colon); the file was left unchanged");
        invocation.StdErr.Split("formatting declined").Should().HaveCount(2, "the refusal is printed exactly once");
        File.ReadAllText(spy).Should().Be(source);
    }

    [Fact]
    public void EscapedWildcardCapture_FormatsAndKeepsTheEscape()
    {
        // #2166: `case `_`:` binds a local named `_` (the escape is honoured), so the formatter has
        // nothing to drop and writes the file with the escape intact — the cell the test above used
        // to decline.
        using var ws = new TempWorkspace();
        var source = "def main():\n    x = 1\n    match x:\n        case `_`:\n            print(`_`)\n";
        var spy = ws.WriteSpy(source);

        var invocation = CliTestHarness.Invoke($"format \"{spy}\"");

        invocation.ExitCode.Should().Be(0, invocation.StdErr);
        invocation.StdErr.Should().NotContain("SPY0912");
        File.ReadAllText(spy).Should().Contain("case `_`:").And.Contain("print(`_`)");
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
