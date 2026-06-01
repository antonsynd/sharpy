using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

public class CompileCommandTests
{
    // ---- Parse-level tests ----

    [Fact]
    public void Parses_MinimalInvocation()
    {
        var result = CliTestHarness.Parse("compile main.spy");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("compile");
        result.GetValue<FileInfo>("input")!.Name.Should().Be("main.spy");
    }

    [Fact]
    public void RequiresInputArgument()
    {
        var result = CliTestHarness.Parse("compile");

        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Parses_OutputOption_LongAndShort()
    {
        var longForm = CliTestHarness.Parse("compile main.spy --output out.dll");
        var shortForm = CliTestHarness.Parse("compile main.spy -o out.dll");

        longForm.Errors.Should().BeEmpty();
        shortForm.Errors.Should().BeEmpty();
        longForm.GetValue<FileInfo>("--output")!.Name.Should().Be("out.dll");
        shortForm.GetValue<FileInfo>("--output")!.Name.Should().Be("out.dll");
    }

    [Fact]
    public void Parses_ConfigurationOption_ShortAlias()
    {
        var result = CliTestHarness.Parse("compile main.spy -c Release");

        result.Errors.Should().BeEmpty();
        result.GetValue<string?>("--configuration").Should().Be("Release");
    }

    [Fact]
    public void Configuration_DefaultsToNull_WhenAbsent()
    {
        // The "Release" default is applied in the action handler, not during parsing.
        var result = CliTestHarness.Parse("compile main.spy");

        result.GetValue<string?>("--configuration").Should().BeNull();
    }

    [Fact]
    public void Parses_TypeOption_ShortAlias()
    {
        var result = CliTestHarness.Parse("compile main.spy -t library");

        result.Errors.Should().BeEmpty();
        result.GetValue<string?>("--type").Should().Be("library");
    }

    [Fact]
    public void Parses_ReferenceOption_ShortAlias()
    {
        var result = CliTestHarness.Parse("compile main.spy -r ref.dll");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>("--reference").Should().Contain("ref.dll");
    }

    [Fact]
    public void Parses_ModulePathOption_ShortAlias()
    {
        var result = CliTestHarness.Parse("compile main.spy -m mods");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>("--module-path").Should().Contain("mods");
    }

    [Fact]
    public void Parses_ProjectReferenceOption_ShortAlias()
    {
        var result = CliTestHarness.Parse("compile main.spy -p lib.csproj");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>("--project-reference").Should().Contain("lib.csproj");
    }

    [Fact]
    public void Parses_SelfContainedFlag()
    {
        var result = CliTestHarness.Parse("compile main.spy --self-contained");

        result.Errors.Should().BeEmpty();
        result.GetValue<bool>("--self-contained").Should().BeTrue();
    }

    [Fact]
    public void SelfContained_DefaultsToFalse()
    {
        var result = CliTestHarness.Parse("compile main.spy");

        result.GetValue<bool>("--self-contained").Should().BeFalse();
    }

    [Fact]
    public void Parses_NoDepsFlag()
    {
        var result = CliTestHarness.Parse("compile main.spy --no-deps");

        result.Errors.Should().BeEmpty();
        result.GetValue<bool>("--no-deps").Should().BeTrue();
    }

    [Fact]
    public void NoDeps_DefaultsToFalse()
    {
        var result = CliTestHarness.Parse("compile main.spy");

        result.GetValue<bool>("--no-deps").Should().BeFalse();
    }

    [Fact]
    public void Parses_SpyProjInput()
    {
        var result = CliTestHarness.Parse("compile project.spyproj");

        result.Errors.Should().BeEmpty();
        var input = result.GetValue<FileInfo>("input")!;
        input.Name.Should().Be("project.spyproj");
        input.Extension.Should().Be(".spyproj");
    }

    [Fact]
    public void Parses_IncrementalFlag()
    {
        var result = CliTestHarness.Parse("compile project.spyproj --incremental");

        result.Errors.Should().BeEmpty();
        result.GetValue<bool>("--incremental").Should().BeTrue();
    }

    [Fact]
    public void Parses_CleanFlag()
    {
        var result = CliTestHarness.Parse("compile project.spyproj --clean");

        result.Errors.Should().BeEmpty();
        result.GetValue<bool>("--clean").Should().BeTrue();
    }

    [Fact]
    public void Rejects_UnknownOption()
    {
        var result = CliTestHarness.Parse("compile main.spy --frobnicate");

        result.Errors.Should().NotBeEmpty();
    }

    // ---- Invocation-level tests ----

    [Fact]
    public void Compile_FileNotFound_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var missing = ws.PathFor("nope.spy");

        var invocation = CliTestHarness.Invoke($"compile \"{missing}\"");

        invocation.ExitCode.Should().Be(1);
        invocation.StdErr.Should().Contain("does not exist");
    }

    [Fact]
    public void Compile_CompilationFailure_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy("def 123invalid():\n    return 0\n");
        var outPath = ws.PathFor(Path.Combine("out", "broken.dll"));

        var invocation = CliTestHarness.Invoke($"compile \"{spy}\" -o \"{outPath}\"");

        invocation.ExitCode.Should().Be(1);
        invocation.StdErr.Should().Contain("Compilation failed:");
        File.Exists(outPath).Should().BeFalse();
    }

    [Fact]
    public void Compile_ValidSource_ProducesDll()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy("def main():\n    print(\"hello\")\n", "hello.spy");
        var outPath = ws.PathFor(Path.Combine("out", "hello.dll"));

        var invocation = CliTestHarness.Invoke($"compile \"{spy}\" -o \"{outPath}\"");

        invocation.ExitCode.Should().Be(0);
        File.Exists(outPath).Should().BeTrue();
    }

    [Fact]
    public void Compile_ValidSource_CopiesRuntimeDependencies()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy("def main():\n    print(\"hello\")\n", "hello.spy");
        var outDir = ws.PathFor("out");
        var outPath = Path.Combine(outDir, "hello.dll");

        var invocation = CliTestHarness.Invoke($"compile \"{spy}\" -o \"{outPath}\"");

        invocation.ExitCode.Should().Be(0);
        File.Exists(Path.Combine(outDir, "Sharpy.Core.dll")).Should().BeTrue();
    }

    [Fact]
    public void Compile_ValidSource_ProducesSupportFiles()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy("def main():\n    print(\"hello\")\n", "hello.spy");
        var outDir = ws.PathFor("out");
        var outPath = Path.Combine(outDir, "hello.dll");

        var invocation = CliTestHarness.Invoke($"compile \"{spy}\" -o \"{outPath}\"");

        invocation.ExitCode.Should().Be(0);
        File.Exists(Path.Combine(outDir, "hello.runtimeconfig.json")).Should().BeTrue();
        File.Exists(Path.Combine(outDir, "hello.deps.json")).Should().BeTrue();
    }

    [Fact]
    public void Compile_NoDeps_SkipsCopyingRuntimeDependencies()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy("def main():\n    print(\"hello\")\n", "hello.spy");
        var outDir = ws.PathFor("out");
        var outPath = Path.Combine(outDir, "hello.dll");

        var invocation = CliTestHarness.Invoke($"compile \"{spy}\" -o \"{outPath}\" --no-deps");

        invocation.ExitCode.Should().Be(0);
        File.Exists(outPath).Should().BeTrue();
        File.Exists(Path.Combine(outDir, "Sharpy.Core.dll")).Should().BeFalse();
    }

    [Theory]
    [InlineData("Debug")]
    [InlineData("Release")]
    public void Compile_ConfigurationOption_ProducesDll(string configuration)
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy("def main():\n    print(\"hello\")\n", "hello.spy");
        var outPath = ws.PathFor(Path.Combine("out", "hello.dll"));

        var invocation = CliTestHarness.Invoke($"compile \"{spy}\" -o \"{outPath}\" -c {configuration}");

        invocation.ExitCode.Should().Be(0);
        File.Exists(outPath).Should().BeTrue();
    }
}
