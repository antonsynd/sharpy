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

    /// <summary>
    /// The program's argument vector still collects through <c>--args</c>, one value per
    /// occurrence. This used to read <c>--args alpha beta gamma</c>, which worked only because
    /// <c>--args</c> allowed multiple arguments per token — the greediness #1215 removes.
    /// </summary>
    [Fact]
    public void Parses_ArgsPassthrough()
    {
        var result = CliTestHarness.Parse("run main.spy --args alpha --args beta --args gamma");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>("--args").Should().Equal("alpha", "beta", "gamma");
    }

    /// <summary>
    /// The documented break (#1215): with <c>--args</c> non-greedy, its multi-token spelling stops
    /// working. It fails loudly rather than quietly dropping the extra values.
    /// </summary>
    [Fact]
    public void Args_MultiTokenSpelling_IsRejected()
    {
        var result = CliTestHarness.Parse("run main.spy --args alpha beta gamma");

        result.Errors.Should().NotBeEmpty();
        result.GetValue<string[]>("--args").Should().Equal("alpha");
    }

    [Fact]
    public void Args_BeforeThePositional_DoesNotSwallowTheInputFile()
    {
        var result = CliTestHarness.Parse("run --args alpha main.spy");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>("--args").Should().Equal("alpha");
        result.GetValue<FileInfo>("input")!.Name.Should().Be("main.spy");
    }

    /// <summary>
    /// The replacement spelling (#1215): everything after a bare <c>--</c> is the program's
    /// argument vector. The split happens before parsing, so those tokens never reach the option
    /// grammar and the positional input is untouched.
    /// </summary>
    [Fact]
    public void DoubleDash_PassesTokensToTheProgram()
    {
        var (compilerArguments, programArguments) = CliTestHarness.SplitCommandLine("run main.spy -- alpha beta gamma");

        programArguments.Should().Equal("alpha", "beta", "gamma");
        compilerArguments.Should().Equal("run", "main.spy");

        var result = CliTestHarness.Parse("run main.spy -- alpha beta gamma");
        result.Errors.Should().BeEmpty();
        result.GetValue<FileInfo>("input")!.Name.Should().Be("main.spy");
    }

    /// <summary>
    /// Tokens after <c>--</c> that look like compiler options must reach the program verbatim
    /// instead of being parsed — including a second <c>--</c>, which is not a second separator.
    /// </summary>
    [Fact]
    public void DoubleDash_ShieldsOptionLookingTokensFromTheParser()
    {
        var (_, programArguments) = CliTestHarness.SplitCommandLine("run main.spy -- --reference x -- --verbose");

        programArguments.Should().Equal("--reference", "x", "--", "--verbose");

        var (result, globals) = CliTestHarness.ParseWithGlobals("run main.spy -- --reference x -- --verbose");
        result.Errors.Should().BeEmpty();
        (result.GetValue<string[]>("--reference") ?? Array.Empty<string>()).Should().BeEmpty();
        result.GetValue(globals.Verbose).Should().BeFalse();
    }

    /// <summary>
    /// Compiler options keep working on the near side of the separator.
    /// </summary>
    [Fact]
    public void DoubleDash_LeavesCompilerOptionsBeforeItAlone()
    {
        var result = CliTestHarness.Parse("run --module-path mods main.spy -- alpha");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>("--module-path").Should().Equal("mods");
        result.GetValue<FileInfo>("input")!.Name.Should().Be("main.spy");
    }

    /// <summary>
    /// Both channels reach the program: <c>--args</c> values in the order written, then everything
    /// after <c>--</c>. Scripts mid-migration keep working.
    /// </summary>
    [Fact]
    public void ProgramArguments_CombineArgsOptionThenDoubleDash()
    {
        Sharpy.Cli.Commands.RunCommand
            .CombineProgramArguments(new[] { "from-args" }, new[] { "after-dash", "and-another" })
            .Should().Equal("from-args", "after-dash", "and-another");

        Sharpy.Cli.Commands.RunCommand
            .CombineProgramArguments(null, Array.Empty<string>())
            .Should().BeEmpty();
    }

    /// <summary>
    /// Only <c>run</c> executes anything, so only <c>run</c> can be handed a program argument
    /// vector. Before #1215 those tokens were a parse error on every command; they must not become
    /// silently discarded now that the separator is understood.
    /// </summary>
    [Fact]
    public void DoubleDash_OnACommandThatRunsNothing_IsRejected()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy("def main() -> None:\n    print(\"hi\")\n");

        var invocation = CliTestHarness.Invoke($"build \"{spy}\" -- alpha");

        invocation.ExitCode.Should().Be(1);
        invocation.StdErr.Should().Contain("'--'");
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

    /// <summary>
    /// References collect across repeated occurrences, and the short alias participates. This used
    /// to be spelled <c>--reference a.dll b.dll -r c.dll</c>, where <c>b.dll</c> reached the option
    /// only because <c>AllowMultipleArgumentsPerToken</c> let one occurrence keep eating bare
    /// tokens — the same greediness that swallowed positional input paths (#1215, #1179).
    /// </summary>
    [Fact]
    public void Parses_MultipleReferences()
    {
        var result = CliTestHarness.Parse("run main.spy --reference a.dll --reference b.dll -r c.dll");

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

    /// <summary>
    /// End-to-end proof that the separator reaches the executed program, not just the parser: the
    /// program exits with the number of arguments it received (#1215). The child process writes to
    /// the real console rather than the captured writers, so its exit code is the observable.
    /// </summary>
    [Fact]
    public void Run_ArgumentsAfterDoubleDash_ReachTheProgram()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy("import sys\n\ndef main() -> None:\n    sys.exit(len(sys.argv) - 1)\n");
        var outPath = ws.PathFor("app.dll");

        var invocation = CliTestHarness.Invoke($"run \"{spy}\" --output \"{outPath}\" -- alpha beta gamma");

        invocation.ExitCode.Should().Be(3);
    }

    /// <summary>
    /// The deprecated channel still works in its repeatable spelling, so scripts have a transition.
    /// </summary>
    [Fact]
    public void Run_RepeatedArgsOption_ReachesTheProgram()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy("import sys\n\ndef main() -> None:\n    sys.exit(len(sys.argv) - 1)\n");
        var outPath = ws.PathFor("app.dll");

        var invocation = CliTestHarness.Invoke($"run \"{spy}\" --output \"{outPath}\" --args alpha --args beta");

        invocation.ExitCode.Should().Be(2);
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
