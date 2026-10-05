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

    // ── An import resolving to a .spy outside the source set (#2234, R-EQ) ─────────────────────

    /// <summary>How the import reaches its target, in the resolver's search order.</summary>
    public enum Route { ImportingFileDirectory, ModuleRoot, ModulePath, ProjectDirectory }

    /// <summary>Every arm the import resolver dispatches a <c>.spy</c> resolution on.</summary>
    public enum Form { From, Plain, Alias, Star, Relative, Package, Unused }

    public enum Target { Listed, Unlisted, Missing }

    private static string FormProgram(Form form) => form switch
    {
        Form.From => "from helper import helper_value\n\ndef main():\n    print(helper_value())\n",
        Form.Plain or Form.Package => "import helper\n\ndef main():\n    print(helper.helper_value())\n",
        Form.Alias => "import helper as h\n\ndef main():\n    print(h.helper_value())\n",
        Form.Star => "from helper import *\n\ndef main():\n    print(helper_value())\n",
        Form.Relative => "from .helper import helper_value\n\ndef main():\n    print(helper_value())\n",
        _ => "import helper\n\ndef main():\n    print(1)\n",
    };

    /// <summary>
    /// Lays out one cell and returns the target's path (written unless missing). Each route puts the
    /// target where only that route finds it: beside the importing file (the root is pinned one level
    /// up by a second source), below the pinned module root, below a <c>&lt;ModulePath&gt;</c>, or in
    /// the project directory outside every source. A listed target is one of the project's sources.
    /// </summary>
    private static string LayOut(ProjectCompilationHelper helper, Route route, Form form, Target target)
    {
        helper.WithRootNamespace("App").WithOutputType("exe").WithEntryPoint("main.spy");
        string entry, targetDir;
        switch (route)
        {
            case Route.ImportingFileDirectory:
                entry = "sub/main.spy";
                targetDir = Path.Combine(helper.SourceDirectory, "sub");
                helper.AddSourceFile("other/main_other.spy", "def other() -> int:\n    return 0\n");
                helper.Options.SourceFilePattern = target == Target.Listed ? "src/**/*.spy" : "src/**/main*.spy";
                break;
            case Route.ModuleRoot:
                entry = "app/main.spy";
                targetDir = helper.SourceDirectory;
                helper.Options.SourceRoot = "src";
                helper.Options.SourceFilePattern = target == Target.Listed ? "src/**/*.spy" : "src/app/main.spy";
                break;
            case Route.ModulePath:
                entry = "main.spy";
                targetDir = Path.Combine(helper.SourceDirectory, "mods");
                Directory.CreateDirectory(targetDir);
                helper.Options.ModulePaths.Add("src/mods");
                helper.Options.SourceFilePattern = target == Target.Listed ? "src/**/*.spy" : "src/main.spy";
                break;
            default:
                entry = "main.spy";
                targetDir = helper.ProjectDirectory;
                helper.Options.SourceFilePattern = target == Target.Listed ? "**/*.spy" : "src/main.spy";
                break;
        }

        helper.AddSourceFile(entry, FormProgram(form));
        var targetPath = form == Form.Package
            ? Path.Combine(targetDir, "helper", "__init__.spy")
            : Path.Combine(targetDir, "helper.spy");
        if (target != Target.Missing)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllText(targetPath, Helper);
        }
        return targetPath;
    }

    public static IEnumerable<object[]> RouteFormCells()
    {
        foreach (var route in Enum.GetValues<Route>())
            foreach (var form in Enum.GetValues<Form>())
            {
                // A relative import names the importing file's own package and nothing else.
                if (form == Form.Relative && route != Route.ImportingFileDirectory)
                    continue;
                yield return new object[] { route, form };
            }
    }

    /// <summary>
    /// The class contract: an import a project resolves is compiled with the project or refused —
    /// never bound to a <c>.spy</c> the compilation does not contain. Until #2234 every cell here
    /// resolved, type-checked, and failed at C# compile as SPY0908 (CS0234: the generated code named
    /// the target's namespace, which no unit emits), measured @ 746004125; the unused import compiled
    /// (nothing read the binding). Now each is SPY0313 at the import, naming the file it resolved to.
    /// </summary>
    [Theory]
    [MemberData(nameof(RouteFormCells))]
    public void ImportOfAModuleOutsideTheSourceSet_IsRefusedWithSPY0313(Route route, Form form)
    {
        using var helper = new ProjectCompilationHelper(_output);
        var targetPath = LayOut(helper, route, form, Target.Unlisted);
        var cell = $"[{route}/{form}]";

        var result = helper.Compile();

        result.Success.Should().BeFalse(cell);
        var errors = result.Diagnostics.GetErrors().ToList();
        errors.Should().NotContain(d => d.Code == "SPY0908", $"{cell} the refusal precedes code generation");
        // The import still binds the module's extraction, so nothing cascades from it.
        var refusal = errors.Should().ContainSingle($"{cell} exactly the refusal").Subject;
        refusal.Code.Should().Be("SPY0313", cell);
        refusal.Message.Should().Contain($"'{targetPath}'", $"{cell} the diagnostic names the resolved file");
        refusal.Message.Should().Contain("not a source of this project", cell);
        refusal.Message.Should().Contain("<SpyFile>", cell);
        refusal.Line.Should().Be(1, $"{cell} reported at the import statement");
        Path.GetFileName(refusal.FilePath).Should().Be("main.spy", cell);
    }

    /// <summary>
    /// The unused-import cell whose target has a module-level statement. python3 runs the target's
    /// top-level <c>print</c> on import; listed as a source the target is SPY0340 (no executable
    /// statements at module level). Unlisted, nothing compiled the target, so the program ran and the
    /// statement vanished — silent-wrong, measured with the refusal disabled. Refused at the import now
    /// (R-EQ applies with no usage condition).
    /// </summary>
    [Fact]
    public void UnusedImportOfAnUnlistedModuleWithAModuleLevelStatement_IsRefused_NotSilentlyDropped()
    {
        using var helper = new ProjectCompilationHelper(_output);
        var targetPath = LayOut(helper, Route.ModulePath, Form.Unused, Target.Unlisted);
        File.WriteAllText(targetPath, "print(\"side effect\")\n\n" + Helper);

        var result = helper.Compile();

        result.Success.Should().BeFalse();
        result.Diagnostics.GetErrors().Should().ContainSingle(d => d.Code == "SPY0313")
            .Which.Message.Should().Contain($"'{targetPath}'");
    }

    /// <summary>
    /// The refused module is still loaded for its extraction (hover and go-to-definition read it), and
    /// its OWN import of another unlisted module is not a second refusal: only an import written in a
    /// compiled source is refused, so the user sees one SPY0313, at the import they wrote.
    /// </summary>
    [Fact]
    public void UnlistedModuleImportingAnotherUnlistedModule_IsOneRefusal_AtTheCompiledImport()
    {
        using var helper = new ProjectCompilationHelper(_output);
        var targetPath = LayOut(helper, Route.ModulePath, Form.From, Target.Unlisted);
        File.WriteAllText(targetPath,
            "from inner import inner_value\n\ndef helper_value() -> int:\n    return inner_value()\n");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(targetPath)!, "inner.spy"),
            "def inner_value() -> int:\n    return 42\n");

        var result = helper.Compile();

        result.Success.Should().BeFalse();
        var errors = result.Diagnostics.GetErrors().ToList();
        errors.Should().ContainSingle("one refusal, no cascade").Which.Code.Should().Be("SPY0313");
        errors[0].Message.Should().Contain($"'{targetPath}'");
        Path.GetFileName(errors[0].FilePath).Should().Be("main.spy");
    }

    /// <summary>
    /// Positive control for the refusal: the same layout with the target listed as a source compiles
    /// and runs, so the refusal is carried by source-set membership and not by the route or form.
    /// </summary>
    [Theory]
    [MemberData(nameof(RouteFormCells))]
    public void ImportOfAListedSource_OnEveryRoute_CompilesAndRuns(Route route, Form form)
    {
        using var helper = new ProjectCompilationHelper(_output);
        LayOut(helper, route, form, Target.Listed);
        var cell = $"[{route}/{form}]";

        var result = helper.CompileAndExecute();

        result.CompilationErrors.Should().BeEmpty(cell);
        result.Success.Should().BeTrue($"{cell} {result.Exception}");
        result.StandardOutput.Should().Be(form == Form.Unused ? "1\n" : "42\n", cell);
    }

    /// <summary>A target that exists nowhere stays SPY0300, not the new refusal.</summary>
    [Theory]
    [MemberData(nameof(RouteFormCells))]
    public void ImportOfAMissingModule_OnEveryRoute_StaysSPY0300(Route route, Form form)
    {
        using var helper = new ProjectCompilationHelper(_output);
        LayOut(helper, route, form, Target.Missing);
        var cell = $"[{route}/{form}]";

        var result = helper.Compile();

        result.Success.Should().BeFalse(cell);
        var errors = result.Diagnostics.GetErrors().ToList();
        errors.Should().Contain(d => d.Code == "SPY0300", cell);
        errors.Should().NotContain(d => d.Code == "SPY0313", cell);
    }

    /// <summary>
    /// Warm equals cold: an incremental rebuild of the refused project reports the same refusal, and
    /// so does a warm build after the target is dropped from the sources of a project that built.
    /// </summary>
    [Theory]
    [InlineData(Route.ModulePath)]
    [InlineData(Route.ProjectDirectory)]
    public void ImportOfAModuleOutsideTheSourceSet_WarmBuild_IsTheSameRefusal(Route route)
    {
        using var helper = new ProjectCompilationHelper(_output);
        var targetPath = LayOut(helper, route, Form.From, Target.Unlisted);
        helper.WithIncremental();

        var cold = helper.Compile();
        var warm = helper.Compile();

        foreach (var (name, result) in new[] { ("cold", cold), ("warm", warm) })
        {
            result.Success.Should().BeFalse($"[{route}/{name}]");
            result.Diagnostics.GetErrors().Should().ContainSingle(d => d.Code == "SPY0313", $"[{route}/{name}]")
                .Which.Message.Should().Contain($"'{targetPath}'");
        }
    }

    /// <summary>
    /// A target that leaves the sources between two incremental builds — dropped from the items, or
    /// deleted — while the importing file is unchanged. The importer was served from the cache with
    /// the generated C# that names the target's namespace, so the warm build was SPY0908 (CS0234)
    /// where a cold one refuses (SPY0313) or cannot find the module (SPY0300); measured @ 746004125
    /// for the <c>&lt;ModulePath&gt;</c> route (a project-directory target also moves the module root,
    /// which already invalidated the whole cache).
    /// </summary>
    [Theory]
    [InlineData(Route.ModulePath, "dropped")]
    [InlineData(Route.ProjectDirectory, "dropped")]
    [InlineData(Route.ModulePath, "deleted")]
    public void TargetLeavesTheSources_WarmBuild_ResolvesTheImportAgain(Route route, string how)
    {
        using var helper = new ProjectCompilationHelper(_output);
        var targetPath = LayOut(helper, route, Form.From, Target.Listed);
        helper.WithIncremental();
        helper.AssertCompilationSucceeded(helper.Compile());

        if (how == "deleted")
        {
            File.Delete(targetPath);
        }
        else
        {
            helper.Options.SourceFilePattern = "src/main.spy";
            helper.CreateProjectFile();
        }
        var warm = helper.Compile();

        var cell = $"[{route}/{how}]";
        warm.Success.Should().BeFalse(cell);
        var errors = warm.Diagnostics.GetErrors().ToList();
        errors.Should().NotContain(d => d.Code == "SPY0908", cell);
        if (how == "deleted")
            errors.Should().Contain(d => d.Code == "SPY0300", cell);
        else
            errors.Should().ContainSingle(d => d.Code == "SPY0313", cell).Which.Message.Should().Contain($"'{targetPath}'");
    }
}
