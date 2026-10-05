using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// #2039 (P14c): every module is a C# namespace — the root namespace, its directories, its stem. When
/// that namespace is also the full name of a .NET type the module uses, C# resolves the name to the
/// namespace declared in the compiled source, and no spelling reaches the type (short of an extern
/// alias): <c>foo.spy</c> in RootNamespace <c>App</c> importing <c>App.Foo</c> from a referenced
/// assembly. Refused by name, SPY0615, never CS0234 behind SPY0908. The control is the same import
/// from a module whose stem differs (<c>bar.spy</c> is <c>App.Bar</c>): it runs. The Sharpy-declared
/// twin of the shape (a root <c>__init__.spy</c> type <c>A</c> beside a module <c>a.spy</c>) is
/// SPY0526 refusal 2, not this check.
/// </summary>
public class ModuleNamespaceShadowsClrTypeTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sharpy_clrshadow_{Guid.NewGuid():N}");

    public ModuleNamespaceShadowsClrTypeTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_dir, recursive: true); }
        catch { /* best effort */ }
    }

    /// <summary>
    /// A referenced assembly declaring the .NET type <c>App.Foo</c>, the nested <c>App.Outer.Inner</c>,
    /// and <c>App.Maker</c> whose factories return them (a type a file reaches only as an inferred type
    /// argument). One source for every test: the process loads the assembly by identity.
    /// </summary>
    private string BuildReferencedAssembly()
    {
        var path = Path.Combine(_dir, "AppTypes.dll");
        var compilation = CSharpCompilation.Create(
            "AppTypes",
            new[] { CSharpSyntaxTree.ParseText(
                "namespace App { public class Foo { public static int Value() => 4; } " +
                "public class Outer { public class Inner { } } " +
                "public static class Maker { public static Foo Make() => new Foo(); " +
                "public static Outer.Inner MakeInner() => new Outer.Inner(); } }") },
            IntegrationTestBase.GetSharedReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(path);
        emit.Success.Should().BeTrue(string.Join("\n", emit.Diagnostics));
        return path;
    }

    [Theory]
    [InlineData("foo.spy", true)]
    [InlineData("bar.spy", false)]
    public void ModuleNamedLikeAnImportedClrType_IsRefusedByName(string module, bool refused)
    {
        var dll = BuildReferencedAssembly();
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("App").WithEntryPoint("main.spy").WithAssemblyReference(dll);
        helper.AddSourceFile(module, "from app import Foo\n\ndef value() -> int:\n    return Foo.value()\n");
        var stem = Path.GetFileNameWithoutExtension(module);
        helper.AddSourceFile("main.spy", $"from {stem} import value\n\ndef main() -> None:\n    print(value())\n");
        helper.CreateProjectFile();

        var exec = helper.CompileAndExecute();
        var errors = helper.LastCompilationResult!.Diagnostics.GetErrors().ToList();
        errors.Should().NotContain(e => e.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            "never CS0234 behind SPY0908");

        if (refused)
        {
            exec.Success.Should().BeFalse();
            errors.Should().Contain(e => e.Code == DiagnosticCodes.SemanticOverflow.ModuleNamespaceShadowsClrType
                && e.Message.Contains("'App.Foo'", StringComparison.Ordinal)
                && e.Line == 1 && e.Column == 1,
                string.Join("\n", errors.Select(e => $"{e.Code} {e.Line}:{e.Column} {e.Message}")));
        }
        else
        {
            exec.Success.Should().BeTrue(string.Join("\n", errors.Select(e => $"{e.Code} {e.Message}")) + exec.Exception);
            exec.StandardOutput.Trim().Should().Be("4");
        }
    }

    /// <summary>
    /// The shadow is compilation-wide (classcure-b F2, cells-c F7, factcheck F1): a namespace one module
    /// declares hides the type from EVERY file's C#, so the check compares each module's namespaces
    /// (own and enclosing) with the types any file spells. Cells: <c>{m}</c> is the shadowing module's
    /// stem (or directory), replaced by <c>baz</c> for the control, which must run.
    /// <list type="bullet">
    /// <item><c>own</c> — the shadowing module itself imports <c>App.Foo</c> (the per-file arm).</item>
    /// <item><c>sibling</c> — only <c>main.spy</c> uses <c>App.Foo</c>.</item>
    /// <item><c>inferred_arg</c> — no file writes the type: <c>[Maker.make()]</c> is <c>List&lt;App.Foo&gt;</c>.</item>
    /// <item><c>alias</c> — <c>from app import Foo as F</c> in <c>main.spy</c>.</item>
    /// <item><c>declaring_type</c> — <c>outer.spy</c> is <c>App.Outer</c>, the declaring type of the
    /// <c>App.Outer.Inner</c> main reaches only as an inferred type argument.</item>
    /// <item><c>enclosing</c> (project only) — <c>foo/bar.spy</c> is <c>App.Foo.Bar</c>, which declares
    /// the namespace <c>App.Foo</c>; <c>run</c> reaches a module only through an import, and a directory
    /// without <c>__init__.spy</c> is not importable.</item>
    /// </list>
    /// Routes: <c>project</c> (RootNamespace <c>App</c>) and <c>run</c> (the single-file compile with the
    /// CLI's <c>-n App</c>). Prior commit: every non-<c>own</c> cell was SPY0908 CS0118/CS0234.
    /// </summary>
    public static IEnumerable<object[]> ShadowCells()
    {
        var cells = new (string Name, string ShadowFile, string ShadowSource, string Main, string Shadowed, string Output)[]
        {
            ("own", "{m}.spy", "from app import Foo\n\ndef value() -> int:\n    return Foo.value()\n",
                "from {m} import value\n\ndef main() -> None:\n    print(value())\n", "App.Foo", "4"),
            ("sibling", "{m}.spy", "def ff() -> int:\n    return 1\n",
                "from app import Foo\nimport {m}\n\ndef main() -> None:\n    print(Foo.value() + {m}.ff())\n", "App.Foo", "5"),
            ("inferred_arg", "{m}.spy", "def ff() -> int:\n    return 1\n",
                "from app import Maker\nimport {m}\n\ndef main() -> None:\n    xs = [Maker.make()]\n    print(len(xs) + {m}.ff())\n",
                "App.Foo", "2"),
            ("alias", "{m}.spy", "def ff() -> int:\n    return 1\n",
                "from app import Foo as F\nimport {m}\n\ndef main() -> None:\n    print(F.value() + {m}.ff())\n", "App.Foo", "5"),
            ("declaring_type", "{m}.spy", "def ff() -> int:\n    return 1\n",
                "from app import Maker\nimport {m}\n\ndef main() -> None:\n    ys = [Maker.make_inner()]\n    print(len(ys) + {m}.ff())\n",
                "App.Outer", "2"),
            ("enclosing", "{m}/bar.spy", "def ff() -> int:\n    return 1\n",
                "from app import Foo\n\ndef main() -> None:\n    print(Foo.value())\n", "App.Foo", "4"),
        };
        foreach (var route in new[] { "project", "run" })
        {
            foreach (var cell in cells)
            {
                if (route == "run" && cell.Name == "enclosing")
                    continue;
                var stem = cell.Name == "declaring_type" ? "outer" : "foo";
                foreach (var refused in new[] { true, false })
                {
                    var m = refused ? stem : "baz";
                    yield return new object[] { route, cell.Name, cell.ShadowFile.Replace("{m}", m),
                        cell.ShadowSource, cell.Main.Replace("{m}", m), refused ? cell.Shadowed : null!, cell.Output };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(ShadowCells))]
    public void ModuleNamespaceShadowingATypeAnyFileUses_IsRefusedByName(
        string route, string cell, string shadowFile, string shadowSource, string main, string? shadowed, string output)
    {
        var dll = BuildReferencedAssembly();
        IReadOnlyList<CompilerDiagnostic> diagnostics;
        bool success;
        string? stdout = null;
        if (route == "project")
        {
            using var helper = new ProjectCompilationHelper(_output);
            helper.WithRootNamespace("App").WithEntryPoint("main.spy").WithAssemblyReference(dll);
            helper.AddSourceFile(shadowFile, shadowSource);
            helper.AddSourceFile("main.spy", main);
            helper.CreateProjectFile();
            var exec = helper.CompileAndExecute();
            success = exec.Success;
            stdout = exec.StandardOutput;
            diagnostics = helper.LastCompilationResult!.Diagnostics.GetAll();
        }
        else
        {
            var dir = Path.Combine(_dir, $"run_{cell}_{shadowed != null}");
            Directory.CreateDirectory(Path.Combine(dir, Path.GetDirectoryName(shadowFile)!));
            File.WriteAllText(Path.Combine(dir, shadowFile), shadowSource);
            var mainPath = Path.Combine(dir, "main.spy");
            File.WriteAllText(mainPath, main);
            // The CLI's `run main.spy -n App -r AppTypes.dll`: a single-file compile to an assembly.
            var result = new CompilerApi().Compile(main,
                CompilerOptionsFactory.ForCli(references: new[] { dll }, @namespace: "App",
                    assemblyName: $"ShadowRun_{cell}_{shadowed != null}",
                    outputAssemblyPath: Path.Combine(dir, "out", "main.dll")),
                mainPath);
            success = result.Success;
            diagnostics = result.Diagnostics;
        }

        var report = string.Join("\n", diagnostics.Where(d => d.IsError).Select(d => $"{d.Code} {d.FilePath}:{d.Line}:{d.Column} {d.Message}"));
        diagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{route}/{cell}] never CS0118/CS0234 behind SPY0908\n{report}");
        if (shadowed != null)
        {
            success.Should().BeFalse($"[{route}/{cell}] is refused");
            diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.SemanticOverflow.ModuleNamespaceShadowsClrType,
                $"[{route}/{cell}] one SPY0615\n{report}");
            var spy0615 = diagnostics.Single(d => d.Code == DiagnosticCodes.SemanticOverflow.ModuleNamespaceShadowsClrType);
            spy0615.Message.Should().Contain($"the .NET type '{shadowed}'", $"[{route}/{cell}]");
            // Reported at the SHADOWING module's first line, naming the using file when it is another.
            spy0615.FilePath!.Replace('\\', '/').Should().EndWith(shadowFile, $"[{route}/{cell}] {report}");
            (spy0615.Line, spy0615.Column).Should().Be((1, 1), $"[{route}/{cell}]");
            if (cell != "own")
                spy0615.Message.Should().Contain("that 'main.spy' uses", $"[{route}/{cell}]");
        }
        else
        {
            success.Should().BeTrue($"[{route}/{cell}] control\n{report}");
            if (stdout != null)
                stdout.Trim().Should().Be(output, $"[{route}/{cell}] control");
        }
    }

    [Fact]
    public void ShadowMatrix_IsTotal() => ShadowCells().Should().HaveCount(22);

    /// <summary>
    /// Warm: the shadowing module is ADDED after a cold build, so the file that uses the type is
    /// served from the incremental cache — its type names ride the cache entry (schema v40), or the
    /// warm build would ICE where the cold one refuses.
    /// </summary>
    [Fact]
    public void ShadowingModuleAddedInAWarmBuild_IsRefusedAgainstACacheServedUser()
    {
        var dll = BuildReferencedAssembly();
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("App").WithEntryPoint("main.spy").WithAssemblyReference(dll).WithIncremental();
        helper.AddSourceFile("main.spy", "from app import Foo\n\ndef main() -> None:\n    print(Foo.value())\n");
        helper.AddSourceFile("other.spy", "def ff() -> int:\n    return 1\n");
        helper.CreateProjectFile();
        helper.AssertCompilationSucceeded(helper.Compile());

        helper.AddSourceFile("foo.spy", "def ff() -> int:\n    return 1\n");
        var warm = helper.Compile();
        helper.AssertWarmBuildSkipped(warm, "main.spy", "other.spy");
        var errors = warm.Diagnostics.GetErrors().ToList();
        var report = string.Join("\n", errors.Select(e => $"{e.Code} {e.FilePath}:{e.Line}:{e.Column} {e.Message}"));
        errors.Should().NotContain(e => e.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, report);
        errors.Should().ContainSingle(e => e.Code == DiagnosticCodes.SemanticOverflow.ModuleNamespaceShadowsClrType
            && e.FilePath!.EndsWith("foo.spy", StringComparison.Ordinal)
            && e.Message.Contains("that 'main.spy' uses", StringComparison.Ordinal), report);
    }
}
