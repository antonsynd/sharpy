using FluentAssertions;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// A module search path the user supplies (<c>&lt;ModulePath&gt;</c> in a <c>.spyproj</c>, <c>-m</c> on
/// the CLI) is an import root: an absolute import spelled from it resolves (#2233).
/// </summary>
/// <remarks>
/// <para>
/// Until #2233 the path reached the module registry and single-file discovery but not import
/// resolution, which <see cref="ProjectCompiler"/> seeds with the module root and the project
/// directory only. A module discovery had already pulled into the compilation was then SPY0300 at
/// its own import (measured @ 21ca52b5d).
/// </para>
/// <para>
/// Cells: the path spelling {<c>&lt;ModulePath&gt;</c> relative, <c>&lt;ModulePath&gt;</c> absolute,
/// <c>&lt;SourceRoot&gt;</c> (control, 7636f2fef)} × the import form {<c>from m import f</c>,
/// <c>import m</c>}. Each program imports a flat module (<c>helper</c>) and a dotted package module
/// (<c>pkg.mod</c>). The same layout with no search path is still SPY0300. The CLI spellings
/// (<c>-m</c> relative/absolute on run, compile, emit csharp, and <c>compile app.spyproj -m</c>) are
/// pinned in <c>Sharpy.Cli.Tests.E2E.ModuleSearchPathCliTests</c>.
/// </para>
/// </remarks>
[Collection("HeavyCompilation")]
public class ModuleSearchPathTests
{
    private readonly ITestOutputHelper _output;

    public ModuleSearchPathTests(ITestOutputHelper output) => _output = output;

    private const string Helper = "def helper_value() -> int:\n    return 42\n";
    private const string PkgMod = "def f() -> int:\n    return 7\n";

    private const string FromImports =
        "from helper import helper_value\nfrom pkg.mod import f\n\n"
        + "def main():\n    print(helper_value())\n    print(f())\n";

    private const string PlainImports =
        "import helper\nimport pkg.mod\n\n"
        + "def main():\n    print(helper.helper_value())\n    print(pkg.mod.f())\n";

    public enum Spelling { ModulePathRelative, ModulePathAbsolute, SourceRootControl }

    private static string Program(string form) => form == "from" ? FromImports : PlainImports;

    [Theory]
    [InlineData(Spelling.ModulePathRelative, "from")]
    [InlineData(Spelling.ModulePathRelative, "plain")]
    [InlineData(Spelling.ModulePathAbsolute, "from")]
    [InlineData(Spelling.ModulePathAbsolute, "plain")]
    [InlineData(Spelling.SourceRootControl, "from")]
    [InlineData(Spelling.SourceRootControl, "plain")]
    public void ImportSpelledFromASearchPath_Resolves_AndRuns(Spelling spelling, string form)
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("App").WithOutputType("exe");

        if (spelling == Spelling.SourceRootControl)
        {
            // The entry sits below the module root, so only the root (not the entry's own
            // directory) can resolve the imports.
            helper.WithEntryPoint("app/main.spy");
            helper.Options.SourceRoot = "src";
            helper.AddSourceFile("app/main.spy", Program(form));
            helper.AddSourceFile("helper.spy", Helper);
            helper.AddSourceFile("pkg/mod.spy", PkgMod);
        }
        else
        {
            helper.WithEntryPoint("main.spy");
            helper.AddSourceFile("main.spy", Program(form));
            helper.AddSourceFile("mods/helper.spy", Helper);
            helper.AddSourceFile("mods/pkg/mod.spy", PkgMod);
            helper.Options.ModulePaths.Add(spelling == Spelling.ModulePathRelative
                ? "src/mods"
                : Path.Combine(helper.ProjectDirectory, "src", "mods"));
        }

        var result = helper.CompileAndExecute();

        result.CompilationErrors.Should().BeEmpty($"[{spelling}/{form}] the imports are spelled from a search path");
        result.Success.Should().BeTrue($"[{spelling}/{form}] {result.Exception}");
        result.StandardOutput.Should().Be("42\n7\n", $"[{spelling}/{form}]");
    }

    /// <summary>
    /// Control: the same layout with no search path cannot resolve either import, so the cells above
    /// are carried by the search path and not by the entry's own directory.
    /// </summary>
    [Theory]
    [InlineData("from")]
    [InlineData("plain")]
    public void SameLayout_WithoutASearchPath_IsSPY0300(string form)
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("App").WithOutputType("exe").WithEntryPoint("main.spy");
        helper.AddSourceFile("main.spy", Program(form));
        helper.AddSourceFile("mods/helper.spy", Helper);
        helper.AddSourceFile("mods/pkg/mod.spy", PkgMod);

        var result = helper.Compile();

        result.Success.Should().BeFalse($"[{form}]");
        var errors = result.Diagnostics.GetErrors().ToList();
        errors.Should().Contain(d => d.Code == "SPY0300" && d.Message.Contains("'helper'"), $"[{form}]");
        errors.Should().Contain(d => d.Code == "SPY0300" && d.Message.Contains("'pkg.mod'"), $"[{form}]");
    }

    /// <summary>
    /// Known-red row, deliberately not asserted: a <c>&lt;ModulePath&gt;</c> module that is NOT one of the
    /// project's sources. The import now resolves (the search path reaches resolution), but the
    /// resolved file is outside the compilation, so the generated C# names a namespace no unit emits:
    /// SPY0908 (CS0234). Before #2233 it was SPY0300. The project-directory fallback has the same
    /// outcome with no <c>&lt;ModulePath&gt;</c> at all, so this belongs to the "import resolves to a
    /// .spy outside the project's source set" class, which the #2233 round's findings ledger records
    /// for an owner decision (refuse cleanly, or compile the import closure).
    /// </summary>
    [Fact(Skip = "Known red, not asserted: an import resolving to a .spy outside the project's source set is SPY0908 (#2233 findings ledger)")]
    public void ModulePathModule_NotListedAsASource_KnownRed()
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("App").WithOutputType("exe").WithEntryPoint("main.spy");
        helper.Options.SourceFilePattern = "src/main.spy";
        helper.AddSourceFile("main.spy", FromImports);
        helper.AddSourceFile("mods/helper.spy", Helper);
        helper.AddSourceFile("mods/pkg/mod.spy", PkgMod);
        helper.Options.ModulePaths.Add("src/mods");

        var result = helper.CompileAndExecute();

        result.StandardOutput.Should().Be("42\n7\n");
    }
}
