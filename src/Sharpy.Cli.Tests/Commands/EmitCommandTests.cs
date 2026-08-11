using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

public class EmitCommandTests
{
    // Module-level executable statements are an error in Sharpy (SPY0340), so use a
    // declaration-only module that produces zero diagnostics.
    private const string ValidSource = "def greet() -> str:\n    return \"hello\"\n";

    // Uses the gated `defer` statement, which requires the `defer` experimental feature. Without
    // the feature the gate rejects it (SPY0331); with it the emitter lowers it to try/finally.
    private const string GatedDeferSource =
        "def main() -> None:\n    defer:\n        print(\"cleanup\")\n    print(\"body\")\n";

    // ---- Parse-level tests ----

    [Theory]
    [InlineData("tokens")]
    [InlineData("ast")]
    [InlineData("csharp")]
    [InlineData("parse")]
    [InlineData("diagnostics")]
    public void Subcommands_ParseWithInput(string sub)
    {
        var result = CliTestHarness.Parse($"emit {sub} main.spy");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be(sub);
    }

    [Fact]
    public void Hover_RequiresLineAndCol()
    {
        var missing = CliTestHarness.Parse("emit hover main.spy");
        var present = CliTestHarness.Parse("emit hover main.spy --line 1 --col 1");

        missing.Errors.Should().NotBeEmpty();
        present.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Diagnostics_ParsesFormatOption()
    {
        var result = CliTestHarness.Parse("emit diagnostics main.spy --format json");

        result.Errors.Should().BeEmpty();
        result.GetValue<string?>("--format").Should().Be("json");
    }

    [Fact]
    public void CSharp_ParsesNamespaceAndTypeOptions()
    {
        var result = CliTestHarness.Parse("emit csharp main.spy --namespace Game.Scripts --type library");

        result.Errors.Should().BeEmpty();
        result.GetValue<string?>("--namespace").Should().Be("Game.Scripts");
        result.GetValue<string?>("--type").Should().Be("library");
    }

    [Fact]
    public void UnknownSubcommand_ProducesError()
    {
        var result = CliTestHarness.Parse("emit bogus main.spy");

        result.Errors.Should().NotBeEmpty();
    }

    // ---- Invocation tests (valid source, success paths only) ----

    [Fact]
    public void Tokens_EmitsTokenListing()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(ValidSource);

        var invocation = CliTestHarness.Invoke($"emit tokens \"{spy}\"");

        invocation.ExitCode.Should().Be(0,
            "the command should succeed; stderr=<{0}> stdout=<{1}>", invocation.StdErr, invocation.StdOut);
        invocation.StdOut.Should().Contain("Tokens for");
        invocation.StdOut.Should().Contain("Total tokens:");
    }

    [Fact]
    public void Ast_EmitsTree()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(ValidSource);

        var invocation = CliTestHarness.Invoke($"emit ast \"{spy}\"");

        invocation.ExitCode.Should().Be(0,
            "the command should succeed; stderr=<{0}> stdout=<{1}>", invocation.StdErr, invocation.StdOut);
        invocation.StdOut.Should().Contain("AST for");
    }

    [Fact]
    public void Parse_EmitsParseOk_ForValidSource()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(ValidSource);

        var invocation = CliTestHarness.Invoke($"emit parse \"{spy}\"");

        invocation.ExitCode.Should().Be(0,
            "the command should succeed; stderr=<{0}> stdout=<{1}>", invocation.StdErr, invocation.StdOut);
        invocation.StdOut.Should().Contain("PARSE_OK");
    }

    [Fact]
    public void Diagnostics_NoErrors_ForValidSource()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(ValidSource);

        var invocation = CliTestHarness.Invoke($"emit diagnostics \"{spy}\"");

        invocation.ExitCode.Should().Be(0,
            "the command should succeed; stderr=<{0}> stdout=<{1}>", invocation.StdErr, invocation.StdOut);
        invocation.StdOut.Should().Contain("No diagnostics.");
    }

    [Fact]
    public void Diagnostics_JsonFormat_ProducesJsonArray()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(ValidSource);

        var invocation = CliTestHarness.Invoke($"emit diagnostics \"{spy}\" --format json");

        invocation.ExitCode.Should().Be(0,
            "the command should succeed; stderr=<{0}> stdout=<{1}>", invocation.StdErr, invocation.StdOut);
        invocation.StdOut.TrimStart().Should().StartWith("[");
    }

    [Fact]
    public void CSharp_WritesGeneratedFile()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(ValidSource);
        var outPath = ws.PathFor("generated.cs");

        // Use library output to avoid the exe entry-point (main()) requirement.
        var invocation = CliTestHarness.Invoke($"emit csharp \"{spy}\" --output \"{outPath}\" --type library");

        invocation.ExitCode.Should().Be(0,
            "the command should succeed; stderr=<{0}> stdout=<{1}>", invocation.StdErr, invocation.StdOut);
        File.Exists(outPath).Should().BeTrue();
        File.ReadAllText(outPath).Should().Contain("class");
    }

    [Fact]
    public void CSharp_GatedSyntax_WithoutFeature_ReportsSPY0331()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(GatedDeferSource);
        var outPath = ws.PathFor("gated.cs");

        var invocation = CliTestHarness.Invoke($"emit csharp \"{spy}\" --output \"{outPath}\"");

        invocation.ExitCode.Should().Be(1);
        invocation.StdErr.Should().Contain("SPY0331");
        File.Exists(outPath).Should().BeFalse("emit should not write output when compilation fails");
    }

    [Fact]
    public void CSharp_GatedSyntax_WithEnableFeature_Emits()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(GatedDeferSource);
        var outPath = ws.PathFor("gated.cs");

        var invocation = CliTestHarness.Invoke($"emit csharp \"{spy}\" --output \"{outPath}\" --enable-feature defer");

        invocation.ExitCode.Should().Be(0,
            "the gated feature is enabled; stderr=<{0}> stdout=<{1}>", invocation.StdErr, invocation.StdOut);
        File.Exists(outPath).Should().BeTrue();
        // defer lowers to a try/finally envelope.
        File.ReadAllText(outPath).Should().Contain("finally");
    }

    // ---- Sibling imports (#1377) ----

    // A two-file program: `main.spy` imports the sibling `calc.spy`, and the only mistake in it
    // is the argument type at the call. Every diagnostic below is a statement about a program
    // that spans both files, so a front door that cannot see calc.spy cannot produce any of them.
    private const string SiblingLibrarySource = "def twice(x: int) -> int:\n    return x * 2\n";

    private const string SiblingConsumerSource =
        "from calc import twice\n\n\ndef main() -> None:\n    print(twice(21))\n";

    private const string SiblingConsumerBadArgSource =
        "from calc import twice\n\n\ndef main() -> None:\n    print(twice(\"nope\"))\n";

    [Fact]
    public void Diagnostics_ResolvesSiblingImports_NoFalseModuleNotFound()
    {
        using var ws = new TempWorkspace();
        ws.WriteSpy(SiblingLibrarySource, "calc.spy");
        var spy = ws.WriteSpy(SiblingConsumerSource);

        var invocation = CliTestHarness.Invoke($"emit diagnostics \"{spy}\"");

        // Before #1377 this reported `SPY0300: Cannot find module 'calc' (in <source>)` and exited
        // 1, because the analyze seam pinned the entry path to "<source>" and the import-closure
        // walk therefore started from the process working directory, not the file's own.
        invocation.ExitCode.Should().Be(0,
            "the program is correct; stderr=<{0}> stdout=<{1}>", invocation.StdErr, invocation.StdOut);
        invocation.StdOut.Should().NotContain("SPY0300");
        invocation.StdOut.Should().Contain("No diagnostics.");
    }

    [Fact]
    public void Diagnostics_ResolvesSiblingImports_ReportsTheRealError()
    {
        using var ws = new TempWorkspace();
        ws.WriteSpy(SiblingLibrarySource, "calc.spy");
        var spy = ws.WriteSpy(SiblingConsumerBadArgSource);

        var invocation = CliTestHarness.Invoke($"emit diagnostics \"{spy}\"");

        // The failure mode #1377 records is not a missing diagnostic but a WRONG one: the door
        // used to blame the import it could not resolve instead of the argument that is actually
        // ill-typed, so a user would have gone looking for a module that was right there.
        invocation.ExitCode.Should().Be(1);
        invocation.StdOut.Should().Contain("SPY0220");
        invocation.StdOut.Should().NotContain("SPY0300");
    }

    [Fact]
    public void Diagnostics_ReportsDiagnosticsRaisedInTheSiblingFile()
    {
        using var ws = new TempWorkspace();
        // SPY0483 is raised where the declaration is — in the SIBLING file, which the entry file
        // never mentions. A door that only ever sees one file structurally cannot report it.
        ws.WriteSpy("def len(x: list[int]) -> int:\n    return 999\n", "shadowlib.spy");
        var spy = ws.WriteSpy(
            "from shadowlib import len\n\n\ndef main() -> None:\n    xs: list[int] = [1, 2, 3]\n    print(len(xs))\n");

        var invocation = CliTestHarness.Invoke($"emit diagnostics \"{spy}\"");

        invocation.ExitCode.Should().Be(0,
            "rebinding a builtin by explicit import is a warning, not an error; stderr=<{0}> stdout=<{1}>",
            invocation.StdErr, invocation.StdOut);
        invocation.StdOut.Should().Contain("SPY0483", "the declaration warning belongs to shadowlib.spy");
        invocation.StdOut.Should().Contain("SPY0484", "the rebinding warning belongs to the entry file");
    }

    [Fact]
    public void Hover_Invoke_ProducesOutput()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(ValidSource);

        var invocation = CliTestHarness.Invoke($"emit hover \"{spy}\" --line 1 --col 5");

        invocation.ExitCode.Should().Be(0,
            "the command should succeed; stderr=<{0}> stdout=<{1}>", invocation.StdErr, invocation.StdOut);
    }

    // ---- Invocation-level error tests (nonexistent file → exit code 1) ----

    [Fact]
    public void Tokens_FileNotFound_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var missing = ws.PathFor("nope.spy");

        var invocation = CliTestHarness.Invoke($"emit tokens \"{missing}\"");

        invocation.ExitCode.Should().Be(1);
    }

    [Fact]
    public void Ast_FileNotFound_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var missing = ws.PathFor("nope.spy");

        var invocation = CliTestHarness.Invoke($"emit ast \"{missing}\"");

        invocation.ExitCode.Should().Be(1);
    }

    [Fact]
    public void Parse_FileNotFound_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var missing = ws.PathFor("nope.spy");

        var invocation = CliTestHarness.Invoke($"emit parse \"{missing}\"");

        invocation.ExitCode.Should().Be(1);
    }

    [Fact]
    public void CSharp_FileNotFound_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var missing = ws.PathFor("nope.spy");

        var invocation = CliTestHarness.Invoke($"emit csharp \"{missing}\"");

        invocation.ExitCode.Should().Be(1);
    }

    [Fact]
    public void Diagnostics_FileNotFound_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var missing = ws.PathFor("nope.spy");

        var invocation = CliTestHarness.Invoke($"emit diagnostics \"{missing}\"");

        invocation.ExitCode.Should().Be(1);
    }

    [Fact]
    public void Hover_FileNotFound_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var missing = ws.PathFor("nope.spy");

        var invocation = CliTestHarness.Invoke($"emit hover \"{missing}\" --line 1 --col 1");

        invocation.ExitCode.Should().Be(1);
    }
}
