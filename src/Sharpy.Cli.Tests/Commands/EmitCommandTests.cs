using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

public class EmitCommandTests
{
    // Module-level executable statements are an error in Sharpy (SPY0340), so use a
    // declaration-only module that produces zero diagnostics.
    private const string ValidSource = "def greet() -> str:\n    return \"hello\"\n";

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
