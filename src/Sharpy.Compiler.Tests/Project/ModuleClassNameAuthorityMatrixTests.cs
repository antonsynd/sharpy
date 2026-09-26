using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// #2039 update: every module is a namespace and its members class is <c>&lt;Stem&gt;Module</c>
/// (<c>ModuleIdentifiers.LayoutMembersClassName</c>, recorded as the module layout), so the entry bit
/// no longer names a class — <c>main.spy</c> is <c>MainModule</c> either way — and it no longer
/// decides SPY0526. What the matrix still pins: one name every reader agrees on, the SPY0403 entry
/// requirement, the non-entry <c>MainFunc</c> rename (#2065), and warm ≡ cold. History below.
///
/// #2013: ONE module-class-name authority (<c>ModuleIdentifiers.ModuleClassName</c>) with ONE entry
/// predicate (<c>ModuleIdentifiers.DeclaresEntryMain</c>: a top-level <c>def main</c> that is NOT
/// backtick-escaped), read by the emitter, by SPY0523 (<c>CodeGenInfoComputer</c>), by SPY0403
/// (<c>ModuleLevelValidator</c>), by SPY0526 (<c>ProjectCompiler.ReportPackageModuleNameCollisions</c>)
/// and — for a unit served from the incremental cache — by the entry bit its cold build recorded.
/// </summary>
/// <remarks>
/// <para>
/// Before the fix SPY0523 asked a second helper that answered <c>Program</c> for EVERY
/// <c>main.spy</c>, while the emitter answers <c>Program</c> only when the file declares main():
/// a library or non-entry <c>main.spy</c> with <c>def program</c> was falsely refused (SPY0523), and
/// one with <c>def Main</c> / <c>def `Main`</c> was missed (CS0542 behind SPY0908). An escaped
/// <c>def `main`</c> counted as the entry point (CS5001 behind SPY0908 in an entry file, the
/// <c>MainFunc</c> rename elsewhere); the backticks mean the literal spelling, so it is an ordinary
/// function emitted verbatim and an entry file holding only it is SPY0403 with a steer.
/// </para>
/// <para>
/// <b>Cells.</b> project kind {<c>exe_main</c>: exe whose entry is <c>main.spy</c>; <c>exe_app</c>:
/// exe whose <c>&lt;EntryPoint&gt;</c> is <c>app.spy</c>, beside <c>main.spy</c>; <c>lib</c>: a
/// library holding <c>main.spy</c>} × content {<c>def main</c>, <c>def program</c>, <c>def Main</c>,
/// <c>def `Main`</c>, <c>def `main`</c>, a package <c>main/__init__.spy</c>, a <c>main</c> another
/// function calls, a package <c>main/__init__.spy</c> declaring and calling <c>main</c> (#2065: the
/// non-entry <c>main</c> is <c>MainFunc</c> at its declaration and at every reference, and does not
/// collide with the package's class <c>MainModule</c>, #1948)} × build {cold at the
/// source root; warm beside a <c>program/</c> package directory, where the module class spelling
/// decides SPY0526 (a <c>Program</c> module class beside the <c>Program</c> namespace, #1948) and a
/// cache-served unit has no AST to recompute the entry bit from}.
/// </para>
/// </remarks>
[Collection("HeavyCompilation")]
public class ModuleClassNameAuthorityMatrixTests
{
    private readonly ITestOutputHelper _output;

    public ModuleClassNameAuthorityMatrixTests(ITestOutputHelper output) => _output = output;

    private static readonly string[] Kinds = { "exe_main", "exe_app", "lib" };

    private static readonly Dictionary<string, string> Contents = new()
    {
        ["def_main"] = "def main() -> None:\n    print(\"main\")\n\ndef helper() -> int:\n    return 1\n",
        ["def_program"] = "def program() -> int:\n    return 1\n",
        ["def_Main"] = "def Main() -> int:\n    return 1\n",
        ["def_escaped_Main"] = "def `Main`() -> int:\n    return 1\n",
        ["def_escaped_main"] = "def `main`() -> int:\n    return 1\n",
        ["init"] = "def program() -> int:\n    return 1\n",
        // #2065: the non-entry main is named MainFunc at its declaration AND at a reference.
        ["calls_main"] = "def main() -> None:\n    print(\"main\")\n\ndef again() -> None:\n    main()\n",
        ["init_main"] = "def main() -> None:\n    print(\"main\")\n\ndef again() -> None:\n    main()\n",
    };

    /// <summary>
    /// The expected cold outcome per cell: the SPY codes of the build's errors (empty = builds), and
    /// for a build, the module class and the member the unit emits for its function.
    /// </summary>
    private static (string[] Codes, string? ModuleClass, string? Member) Expected(string kind, string content)
    {
        // #2039: every module's members class is <Stem>Module — main.spy and a main/__init__.spy are
        // both MainModule, entry point or not (the Program special case retired). So `def program`,
        // `def Main` and `def `Main`` in main.spy no longer meet their class (def Main/`Main` were
        // SPY0523 against class Main); the entry file without an unescaped main() is SPY0403 alone.
        var entry = kind == "exe_main";
        return content switch
        {
            "def_main" or "calls_main" or "init_main" => (Array.Empty<string>(), "MainModule", entry ? "Main" : "MainFunc"),
            "def_program" or "init" => entry
                ? (new[] { DiagnosticCodes.Validation.MissingMainFunction }, null, null)
                : (Array.Empty<string>(), "MainModule", "Program"),
            // exe_app: the static Main of the non-entry main.spy is no second entry point — the
            // compiler names the entry module's members class as THE entry (#2094); the backticked
            // spelling keeps its literal `Main` (ruling 13).
            "def_Main" or "def_escaped_Main" => entry
                ? (new[] { DiagnosticCodes.Validation.MissingMainFunction }, null, null)
                : (Array.Empty<string>(), "MainModule", "Main"),
            "def_escaped_main" => entry
                ? (new[] { DiagnosticCodes.Validation.MissingMainFunction }, null, null)
                : (Array.Empty<string>(), "MainModule", "main"),
            _ => throw new ArgumentOutOfRangeException(nameof(content), content, null)
        };
    }

    public static IEnumerable<object[]> Cells()
        => from kind in Kinds
           from content in Contents.Keys
           select new object[] { kind, content };

    [Theory]
    [MemberData(nameof(Cells))]
    public void Cold_ModuleClassAndEntryBit_AgreeAcrossEveryReader(string kind, string content)
    {
        using var helper = CreateProject(kind, content, besideProgramDirectory: false);
        var result = helper.Compile();
        var codes = ErrorCodes(result);
        var (expectedCodes, moduleClass, member) = Expected(kind, content);

        codes.Should().NotContain(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{kind}×{content}] never CS0542/CS5001 behind SPY0908\n{Describe(result)}");
        codes.Should().BeEquivalentTo(expectedCodes, $"[{kind}×{content}]\n{Describe(result)}");
        result.Success.Should().Be(expectedCodes.Length == 0, $"[{kind}×{content}]\n{Describe(result)}");

        if (content == "def_escaped_main" && kind == "exe_main")
        {
            result.Diagnostics.GetErrors().Should().Contain(d =>
                d.Code == DiagnosticCodes.Validation.MissingMainFunction
                && d.Message.EndsWith("did you mean `main()` (without backticks)?"),
                "an entry file whose only main is escaped is steered to the unescaped spelling");
        }

        if (moduleClass == null)
            return;

        // The emitted C#: the module class the one authority names, declaring the function under the
        // spelling every reader agrees on.
        var classes = result.GeneratedCSharpFiles.Values
            .SelectMany(cs => CSharpSyntaxTree.ParseText(cs).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Where(c => c.Identifier.Text == moduleClass)
            .ToList();
        classes.Should().ContainSingle($"[{kind}×{content}] module class '{moduleClass}'\n{Describe(result)}");
        classes[0].Members.OfType<MethodDeclarationSyntax>().Select(m => m.Identifier.Text)
            .Should().Contain(member!, $"[{kind}×{content}]");

        if (kind != "lib")
        {
            var exec = helper.CompileAndExecute();
            exec.Success.Should().BeTrue($"[{kind}×{content}] {exec.Exception}");
            exec.StandardOutput.Trim().Should().Be(kind == "exe_main" ? "main" : "app");
        }
    }

    /// <summary>
    /// Warm ≡ cold for the entry bit: beside a <c>program/</c> package directory the module class
    /// spelling decides SPY0526 (a <c>Program</c> module class beside the <c>Program</c> namespace —
    /// refusal 1, #1948), and on a warm
    /// build the unit is served from the cache without an AST. Before the fix the cache-served unit
    /// was ASSUMED to declare main(), so every cold-green cell without main() turned SPY0526 warm.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cells))]
    public void Warm_CacheServedEntryBit_MatchesTheColdBuild(string kind, string content)
    {
        using var helper = CreateProject(kind, content, besideProgramDirectory: true);
        helper.WithIncremental();
        var cold = helper.Compile();
        var coldCodes = ErrorCodes(cold);

        // #2039: main.spy is namespace …Main whatever it declares, and program/ is namespace
        // …Program — two names, as in python, so refusal 1 never fires here. (While the entry
        // module was class Program, a main.spy declaring main() was refused beside program/.)
        coldCodes.Should().NotContain(DiagnosticCodes.CodeGen.PackageModuleNameCollision,
            $"[{kind}×{content}] cold\n{Describe(cold)}");

        // An edit to a DIFFERENT file (content, not mtime) makes the unit under test cache-served.
        helper.UpdateSourceFile("other.spy", "def value() -> int:\n    return 2\n");
        var warm = helper.Compile();
        var warmCodes = ErrorCodes(warm);

        warm.Success.Should().Be(cold.Success, $"[{kind}×{content}] warm ≡ cold\n{Describe(warm)}");
        warmCodes.Should().BeEquivalentTo(coldCodes, $"[{kind}×{content}] warm ≡ cold\n{Describe(warm)}");
        if (cold.Success)
        {
            // The proof the warm verdict was measured on the cache-served unit, not a full rebuild.
            var unit = content is "init" or "init_main" ? "__init__.spy" : "main.spy";
            helper.AssertWarmBuildSkipped(warm, kind == "exe_app" ? new[] { unit, "x.spy", "app.spy" } : new[] { unit, "x.spy" });
        }
    }

    [Fact]
    public void Matrix_IsTotal() => Cells().Should().HaveCount(24, "3 kinds × 8 contents");

    /// <summary>
    /// #2094: a non-entry module's top-level function emitted as <c>Main</c> (<c>def Main</c>, or
    /// <c>def `Main`</c>, whose backticks keep the literal spelling) is a static <c>Main</c> C# would
    /// take as a second entry point (CS0017 behind SPY0908 at 855beadb6). The compiler names the exe's
    /// ONE entry point — the entry module's members class, from the recorded layout — so the program
    /// runs. The library cell is the control: it has no entry type at all.
    /// </summary>
    [Theory]
    [InlineData("def Main() -> int:\n    return 3\n", "Main", "exe")]
    [InlineData("def `Main`() -> int:\n    return 3\n", "`Main`", "exe")]
    [InlineData("def Main() -> int:\n    return 3\n", "Main", "library")]
    public void NonEntryFunctionEmittedAsMain_IsNotASecondEntryPoint(string util, string callee, string outputType)
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Entry").WithOutputType(outputType);
        if (outputType == "exe")
        {
            helper.WithEntryPoint("app.spy");
            helper.AddSourceFile("app.spy", $"from util import {callee}\n\ndef main() -> None:\n    print({callee}())\n");
        }
        helper.AddSourceFile("util.spy", util);
        helper.CreateProjectFile();
        var result = helper.Compile();

        result.Success.Should().BeTrue(Describe(result));
        if (outputType == "library")
        {
            result.EntryTypeName.Should().BeNull("a library has no entry point to name");
            return;
        }

        result.EntryTypeName.Should().Be("Entry.App.AppModule");
        var util_cs = result.GeneratedCSharpFiles.Single(kv => Path.GetFileName(kv.Key) == "util.cs").Value;
        util_cs.Should().Contain("public static int Main()", "the literal spelling is kept (ruling 13); only the entry choice changed");
        var exec = helper.CompileAndExecute();
        exec.Success.Should().BeTrue($"{exec.Exception} {string.Join("\n", exec.CompilationErrors)}");
        exec.StandardOutput.Trim().Should().Be("3");
    }

    private ProjectCompilationHelper CreateProject(string kind, string content, bool besideProgramDirectory)
    {
        var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Authority");
        var unitPath = content is "init" or "init_main" ? "main/__init__.spy" : "main.spy";
        switch (kind)
        {
            case "exe_main":
                helper.WithOutputType("exe").WithEntryPoint(Path.GetFileName(unitPath));
                break;
            case "exe_app":
                helper.WithOutputType("exe").WithEntryPoint("app.spy");
                helper.AddSourceFile("app.spy", "def main() -> None:\n    print(\"app\")\n");
                break;
            default:
                helper.WithOutputType("library");
                break;
        }

        helper.AddSourceFile(unitPath, Contents[content]);
        helper.AddSourceFile("other.spy", "def value() -> int:\n    return 1\n");
        if (besideProgramDirectory)
            helper.AddSourceFile("program/x.spy", "def x() -> int:\n    return 1\n");
        helper.CreateProjectFile();
        return helper;
    }

    private static List<string> ErrorCodes(ProjectCompilationResult result)
        => result.Diagnostics.GetErrors().Select(d => d.Code ?? "").Distinct().ToList();

    private static string Describe(ProjectCompilationResult result)
        => string.Join("\n", result.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.FilePath}: {d.Message}"));
}
