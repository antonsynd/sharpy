using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #2242 (P53 D6): the checker records WHICH export a module-qualified member access bound
/// (<see cref="SemanticInfo.GetModuleMemberResolution"/>), so the editor's navigation handlers read
/// one resolution instead of re-deriving the export lookup (the #1328 escape-identity rule, the
/// .NET PascalCase fallback).
///
/// <para><b>Matrix.</b> spelling {call <c>lib.build("x")</c>, value <c>f = lib.build</c>, type
/// <c>lib.Widget()</c>, generic type <c>lib.Box[int](5)</c> and generic function
/// <c>lib.ident[int](3)</c> (both bound by the generic-reference arm, which never visits
/// CheckMemberAccess), aliased <c>l.build</c>, nested <c>pkg.lib.build</c> and its
/// qualifier <c>pkg.lib</c>, a .NET sub-namespace <c>`System`.`Text`</c> and the type under it}
/// × view {the project-MERGED SemanticInfo (<c>ProjectModel.SemanticInfo</c>, what codegen reads,
/// fed by <c>MergeFrom</c>), the project per-file view (<c>GetFileResult(path).SemanticInfo</c>,
/// what the LSP reads), the single-file door (<c>CompilerApi.Analyze(source, path, …)</c>), the
/// editor-document door (<c>CompilerApi.AnalyzeDocument</c>)}.</para>
///
/// <para><b>Controls.</b> Each cell asserts the recorded member by reference against the symbol the
/// module actually exports (not merely non-null), and a member access on a NON-module receiver
/// (<c>w.x</c>, <c>sb.Length</c>) must record nothing — paired with the positive cells so the absence
/// assertion cannot pass vacuously on a table that records nothing at all.</para>
/// </summary>
public class ModuleMemberResolutionFactTests
{
    private readonly ITestOutputHelper _output;

    public ModuleMemberResolutionFactTests(ITestOutputHelper output) => _output = output;

    private const string LibSource =
        "class Widget:\n"
        + "    x: int = 1\n"
        + "\n"
        + "class Box[T]:\n"
        + "    v: T\n"
        + "\n"
        + "    def __init__(self, v: T):\n"
        + "        self.v = v\n"
        + "\n"
        + "def build(name: str) -> str:\n"
        + "    return name\n"
        + "\n"
        + "def ident[T](v: T) -> T:\n"
        + "    return v\n";

    private const string MainSource =
        "import lib\n"
        + "import lib as l\n"
        + "import pkg.lib\n"
        + "\n"
        + "def main() -> None:\n"
        + "    print(lib.build(\"x\"))\n"
        + "    f = lib.build\n"
        + "    print(f(\"y\"))\n"
        + "    w = lib.Widget()\n"
        + "    print(w.x)\n"
        + "    b = lib.Box[int](5)\n"
        + "    print(b.v)\n"
        + "    print(lib.ident[int](3))\n"
        + "    print(l.build(\"z\"))\n"
        + "    print(pkg.lib.build(\"q\"))\n"
        + "    sb = `System`.`Text`.StringBuilder()\n"
        + "    print(sb.Length)\n";

    /// <summary>One expected binding: the spelling of the MemberAccess and the member name it must
    /// record.</summary>
    private sealed record Cell(string Spelling, string Member);

    private static readonly Cell[] Cells =
    {
        new("lib.build", "build"),          // call AND value spellings (two nodes)
        new("lib.Widget", "Widget"),
        new("lib.Box", "Box"),
        new("lib.ident", "ident"),
        new("l.build", "build"),
        new("pkg.lib", "lib"),
        new("pkg.lib.build", "build"),
        new("`System`.`Text`", "Text"),
        new("`System`.`Text`.StringBuilder", "StringBuilder"),
    };

    private ProjectCompilationHelper CreateProject()
    {
        var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("ModuleMemberFact")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .AddSourceFile("lib.spy", LibSource)
            .AddSourceFile("pkg/__init__.spy", "")
            .AddSourceFile("pkg/lib.spy", LibSource)
            .AddSourceFile("main.spy", MainSource);
        helper.CreateProjectFile();
        return helper;
    }

    private static string MainPath(ProjectCompilationHelper helper)
        => helper.SourceFiles.Single(p => Path.GetFileName(p) == "main.spy");

    public static IEnumerable<object[]> Views() => new[]
    {
        new object[] { "project-merged" },
        new object[] { "project-file" },
        new object[] { "single-file" },
        new object[] { "document" },
    };

    private static (Module Ast, SemanticInfo Info) Analyze(string view, ProjectCompilationHelper helper)
    {
        var mainPath = MainPath(helper);
        var api = NewApi();
        switch (view)
        {
            case "project-merged":
            case "project-file":
                {
                    var config = ProjectFileParser.Load(
                        Path.Combine(helper.ProjectDirectory, "ModuleMemberFact.spyproj"));
                    var analysis = api.AnalyzeProject(config);
                    AssertNoErrors(view, analysis.Diagnostics.GetAll());
                    var file = analysis.GetFileResult(mainPath);
                    file.Should().NotBeNull("main.spy is a project file");
                    var info = view == "project-merged" ? analysis.ProjectModel.SemanticInfo : file!.SemanticInfo;
                    info.Should().NotBeNull();
                    if (view == "project-merged")
                        ReferenceEquals(info, file!.SemanticInfo).Should().BeFalse(
                            "the merged project SemanticInfo is a distinct instance from the per-file one — "
                            + "otherwise this view would not exercise MergeFrom");
                    return (file!.Ast!, info!);
                }
            case "single-file":
                {
                    var result = api.Analyze(MainSource, mainPath, CompilerOptionsFactory.ForLibraryAnalysis());
                    AssertNoErrors(view, result.Diagnostics);
                    return (result.Ast!, result.SemanticInfo!);
                }
            case "document":
                {
                    var result = api.AnalyzeDocument(MainSource, mainPath, CompilerOptionsFactory.ForLibraryAnalysis(),
                        stageMetrics: null, cancellationToken: default);
                    AssertNoErrors(view, result.Diagnostics);
                    return (result.Ast!, result.SemanticInfo!);
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(view), view, null);
        }
    }

    /// <summary>Sharpy.Core makes the module registry (builtins, inline CLR namespaces) available.</summary>
    private static CompilerApi NewApi() => new(null, new[] { SharpyCoreReference.Location });

    private static void AssertNoErrors(string view, IEnumerable<CompilerDiagnostic> diagnostics)
    {
        var errors = diagnostics.Where(d => d.IsError).Select(d => $"{d.Code}: {d.Message}").ToList();
        errors.Should().BeEmpty($"{view}: the probe program must type-check");
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void ModuleQualifiedMemberAccess_RecordsTheBoundExport(string view)
    {
        using var helper = CreateProject();
        var (ast, info) = Analyze(view, helper);

        var accesses = Descendants(ast).OfType<MemberAccess>().ToList();
        var failures = new List<string>();

        foreach (var cell in Cells)
        {
            var nodes = accesses.Where(m => Spell(m) == cell.Spelling).ToList();
            if (nodes.Count == 0)
            {
                failures.Add($"{cell.Spelling}: no such MemberAccess in the AST (the probe is broken)");
                continue;
            }

            foreach (var node in nodes)
            {
                var recorded = info.GetModuleMemberResolution(node);
                if (recorded is not { } r)
                {
                    failures.Add($"{cell.Spelling} @ {node.LineStart}:{node.ColumnStart}: nothing recorded");
                    continue;
                }

                if (r.Member.Name != cell.Member)
                    failures.Add($"{cell.Spelling}: recorded member '{r.Member.Name}', expected '{cell.Member}'");

                // The recorded member is the module's OWN export, by reference — the symbol a
                // navigation handler would land on, not a look-alike.
                if (!r.Module.Exports.TryGetValue(r.Member.Name, out var exported)
                    || !ReferenceEquals(exported, r.Member))
                    failures.Add($"{cell.Spelling}: recorded member is not '{r.Module.Name}'s export "
                                 + $"'{r.Member.Name}' by reference");
            }
        }

        // lib.build appears twice (call and value spelling) — both nodes must be covered.
        accesses.Count(m => Spell(m) == "lib.build").Should().Be(2, "the call and the value spelling");

        failures.Should().BeEmpty($"{view}: every module-qualified access must record its binding");

        // The aliased and the plain spelling bind THE SAME export of THE SAME module.
        var plain = info.GetModuleMemberResolution(accesses.First(m => Spell(m) == "lib.build"))!.Value;
        var aliased = info.GetModuleMemberResolution(accesses.Single(m => Spell(m) == "l.build"))!.Value;
        ReferenceEquals(plain.Member, aliased.Member).Should().BeTrue($"{view}: `l.build` is `lib.build`");
        plain.Member.Should().BeOfType<FunctionSymbol>();

        // The nested spelling binds the PACKAGE module's export, a different module from top-level lib.
        var nested = info.GetModuleMemberResolution(accesses.Single(m => Spell(m) == "pkg.lib.build"))!.Value;
        var qualifier = info.GetModuleMemberResolution(accesses.Single(m => Spell(m) == "pkg.lib"))!.Value;
        ReferenceEquals(nested.Module, qualifier.Member).Should().BeTrue(
            $"{view}: `pkg.lib.build` is a member of the module `pkg.lib` bound");
        ReferenceEquals(nested.Member, plain.Member).Should().BeFalse(
            $"{view}: pkg/lib.spy's build is not lib.spy's build");

        info.GetModuleMemberResolution(accesses.Single(m => Spell(m) == "lib.Widget"))!.Value.Member
            .Should().BeOfType<TypeSymbol>();

        // Negative control: a member access on an instance receiver records no module binding.
        var instanceAccess = accesses.Single(m => Spell(m) == "w.x");
        info.GetExpressionType(instanceAccess).Should().NotBeNull(
            $"{view}: positive control — the checker did visit `w.x`");
        info.GetModuleMemberResolution(instanceAccess).Should().BeNull(
            $"{view}: `w` is an instance, not a module");
        info.GetModuleMemberResolution(accesses.Single(m => Spell(m) == "sb.Length")).Should().BeNull(
            $"{view}: `sb` is an instance of a CLR type, not a module");
    }

    /// <summary>
    /// The <c>builtins.</c>-qualified CALLEE arms bind a registry type that is not among the
    /// module's discovered exports: <c>builtins.dict()</c> (the registry-type arm) and
    /// <c>builtins.int("5")</c> (the primitive-overload carve-out fallback, #1667). Both record the
    /// symbol the checker typed the access with, by reference.
    /// </summary>
    [Fact]
    public void BuiltinsQualifiedCallee_RecordsTheRegistryType()
    {
        const string source =
            "import builtins\n"
            + "\n"
            + "def main() -> None:\n"
            + "    d: dict[str, int] = builtins.dict()\n"
            + "    n = builtins.int(\"5\")\n"
            + "    print(len(d) + n)\n";

        var result = NewApi().Analyze(source);
        AssertNoErrors("builtins", result.Diagnostics);
        var info = result.SemanticInfo!;
        var accesses = Descendants(result.Ast!).OfType<MemberAccess>().ToList();

        foreach (var spelling in new[] { "builtins.dict", "builtins.int" })
        {
            var node = accesses.Single(m => Spell(m) == spelling);
            var recorded = info.GetModuleMemberResolution(node);
            recorded.Should().NotBeNull($"{spelling}: the callee arm binds a registry type");
            recorded!.Value.Module.CanonicalModuleName.Should().Be("builtins");
            info.GetExpressionType(node).Should().BeOfType<UserDefinedType>()
                .Which.Symbol.Should().BeSameAs(recorded.Value.Member,
                    $"{spelling}: the recorded member is the type the checker bound");
        }
    }

    private static string Spell(Expression expression) => expression switch
    {
        Identifier id => id.IsNameBacktickEscaped ? "`" + id.Name + "`" : id.Name,
        MemberAccess ma => Spell(ma.Object) + "." + (ma.IsMemberBacktickEscaped ? "`" + ma.Member + "`" : ma.Member),
        _ => "<" + expression.GetType().Name + ">",
    };

    private static IEnumerable<Node> Descendants(Node root)
    {
        yield return root;
        foreach (var child in root.GetChildNodes())
            foreach (var descendant in Descendants(child))
                yield return descendant;
    }
}
