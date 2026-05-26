using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

public class RunCommandTests
{
    [Fact]
    public void Parses_MinimalInvocation()
    {
        var result = CliTestHarness.Parse("run main.spy");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("run");
        result.GetValue<FileInfo>("input")!.Name.Should().Be("main.spy");
    }

    [Fact]
    public void RequiresInputArgument()
    {
        var result = CliTestHarness.Parse("run");

        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Parses_OutputOption_LongAndShort()
    {
        var longForm = CliTestHarness.Parse("run main.spy --output out.dll");
        var shortForm = CliTestHarness.Parse("run main.spy -o out.dll");

        longForm.Errors.Should().BeEmpty();
        shortForm.Errors.Should().BeEmpty();
        longForm.GetValue<FileInfo>("--output")!.Name.Should().Be("out.dll");
        shortForm.GetValue<FileInfo>("--output")!.Name.Should().Be("out.dll");
    }

    [Fact]
    public void Parses_ArgsPassthrough()
    {
        var result = CliTestHarness.Parse("run main.spy --args alpha beta gamma");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>("--args").Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public void Parses_SelfContainedFlag()
    {
        var result = CliTestHarness.Parse("run main.spy --self-contained");

        result.Errors.Should().BeEmpty();
        result.GetValue<bool>("--self-contained").Should().BeTrue();
    }

    [Fact]
    public void SelfContained_DefaultsToFalse()
    {
        var result = CliTestHarness.Parse("run main.spy");

        result.GetValue<bool>("--self-contained").Should().BeFalse();
    }

    [Fact]
    public void Parses_MultipleReferences()
    {
        var result = CliTestHarness.Parse("run main.spy --reference a.dll b.dll -r c.dll");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>("--reference").Should().Contain(new[] { "a.dll", "b.dll", "c.dll" });
    }

    [Fact]
    public void Parses_ProjectReferenceAndModulePath()
    {
        var result = CliTestHarness.Parse("run main.spy --project-reference lib.csproj --module-path mods");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>("--project-reference").Should().Contain("lib.csproj");
        result.GetValue<string[]>("--module-path").Should().Contain("mods");
    }

    [Fact]
    public void Rejects_UnknownOption()
    {
        var result = CliTestHarness.Parse("run main.spy --does-not-exist");

        result.Errors.Should().NotBeEmpty();
    }

    // ---- Invocation-level error/success tests ----

    [Fact]
    public void Run_FileNotFound_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var missing = ws.PathFor("nope.spy");

        var invocation = CliTestHarness.Invoke($"run \"{missing}\"");

        invocation.ExitCode.Should().Be(1);
        invocation.StdErr.Should().Contain("does not exist");
    }

    [Fact]
    public void Run_CompilationFailure_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy("def 123invalid():\n    return 0\n");

        var invocation = CliTestHarness.Invoke($"run \"{spy}\"");

        invocation.ExitCode.Should().Be(1);
        invocation.StdErr.Should().Contain("Compilation failed:");
    }

    [Fact]
    public void Run_ValidProgram_ReturnsExitCode0()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy("def main():\n    print(\"hello\")\n");
        var outPath = ws.PathFor("app.dll");

        var invocation = CliTestHarness.Invoke($"run \"{spy}\" --output \"{outPath}\"");

        invocation.ExitCode.Should().Be(0);
    }
}
