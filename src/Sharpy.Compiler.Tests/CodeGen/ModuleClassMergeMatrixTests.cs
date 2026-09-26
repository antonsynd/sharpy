using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// A type named like its file (#1919 → #2039): every module is a C# namespace, its functions,
/// variables and constants live in its members class <c>&lt;X&gt;</c> (<c>ThingModule</c>) and its
/// types are declared BESIDE <c>&lt;X&gt;</c> — so a type named like its file is an ordinary type in
/// that namespace (<c>Merge.Thing.Thing</c>), in every kind and every mode. There is no merge (ruling
/// M2) and no refusal: the 16 cells that were SPY0520 (generic class/dataclass, struct, union,
/// interface, enum × project/unimported, plus the four <c>run</c> cells of the same kinds) all run.
///
/// <para><b>Cells.</b> colliding kind {class, dataclass, generic class, generic dataclass, struct,
/// union, interface, enum} × mode {<c>run</c> — the colliding file is the entry and has
/// <c>main</c>; <c>project</c> — the colliding file is an imported library module without
/// <c>main</c>, whose function <c>main.spy</c> calls; <c>project_unimported</c> — the same library
/// module, which nothing imports}. The layout is asserted on the tree: <c>Thing</c> is a direct
/// member of the module namespace (never nested in <c>ThingModule</c>), and the module's function
/// is a member of <c>ThingModule</c>.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ModuleClassMergeMatrixTests : IntegrationTestBase
{
    public ModuleClassMergeMatrixTests(ITestOutputHelper output) : base(output) { }

    // kind → (declaration, whether the program constructs it, its arity)
    private static readonly Dictionary<string, (string Declaration, bool Constructs, int Arity)> Kinds = new()
    {
        ["class"] = ("class Thing:\n    v: int\n\n    def __init__(self, v: int) -> None:\n        self.v = v\n", true, 0),
        ["dataclass"] = ("@dataclass\nclass Thing:\n    v: int\n", true, 0),
        ["generic_class"] = ("class Thing[T]:\n    v: T\n\n    def __init__(self, v: T) -> None:\n        self.v = v\n", true, 1),
        ["generic_dataclass"] = ("@dataclass\nclass Thing[T]:\n    v: T\n", true, 1),
        ["struct"] = ("struct Thing:\n    v: int\n", true, 0),
        ["union"] = ("union Thing:\n    case A(v: int)\n    case B()\n", false, 0),
        ["interface"] = ("interface Thing:\n    def f(self) -> int: ...\n", false, 0),
        ["enum"] = ("enum Thing:\n    A = 1\n", false, 0),
    };

    private static string Construction(string kind) => kind.StartsWith("generic") ? "Thing[int](7).v" : "Thing(7).v";

    public static IEnumerable<object[]> Cells()
        => from kind in Kinds.Keys
           from mode in new[] { "run", "project", "project_unimported" }
           select new object[] { kind, mode };

    [Theory]
    [MemberData(nameof(Cells))]
    public void TypeNamedLikeItsFile_IsASiblingOfTheMembersClass(string kind, string mode)
    {
        var (declaration, constructs, arity) = Kinds[kind];
        var use = constructs ? $"print({Construction(kind)})" : "print(7)";

        IReadOnlyList<(string Code, string Message)> errors;
        bool success;
        string stdout;
        string csharp;
        if (mode == "run")
        {
            var source = declaration + "\ndef helper() -> int:\n    return 7\n\ndef main() -> None:\n    " + use + "\n";
            var result = CompileAndExecute(source, fileName: "thing.spy");
            success = result.Success;
            stdout = result.StandardOutput;
            csharp = result.GeneratedCSharp ?? "";
            errors = result.RawDiagnostics.Select(d => (d.Code ?? "", d.Message)).ToList();
        }
        else
        {
            using var helper = new ProjectCompilationHelper(Output);
            helper.WithRootNamespace("Merge").WithEntryPoint("main.spy");
            // The library module also declares a function; under `project` main imports and calls it,
            // under `project_unimported` nothing reaches the module at all.
            helper.AddSourceFile("thing.spy", declaration + "\ndef helper() -> int:\n    return 7\n");
            helper.AddSourceFile("main.spy", mode == "project_unimported"
                ? "def main() -> None:\n    print(7)\n"
                : (constructs ? "from thing import Thing, helper\n\n" : "from thing import helper\n\n")
                    + "def main() -> None:\n    " + (constructs ? use : "print(helper())") + "\n");
            helper.CreateProjectFile();
            var exec = helper.CompileAndExecute();
            success = exec.Success;
            stdout = exec.StandardOutput;
            csharp = helper.LastCompilationResult != null
                ? string.Concat(helper.LastCompilationResult.GeneratedCSharpFiles
                    .Where(kv => Path.GetFileName(kv.Key) == "thing.cs").Select(kv => kv.Value)) : "";
            errors = helper.LastCompilationResult?.Diagnostics.GetErrors()
                .Select(d => (d.Code ?? "", d.Message)).ToList() ?? new List<(string, string)>();
        }

        success.Should().BeTrue($"[{kind}×{mode}] {string.Join(" | ", errors.Select(e => e.Code + " " + e.Message))}");
        stdout.Trim().Should().Be("7", $"[{kind}×{mode}]");

        // The layout is visible in the tree: `Thing` is declared directly in the module namespace,
        // beside the members class `ThingModule` that holds `Helper` — never nested in it, never
        // merged into it.
        var ns = CSharpSyntaxTree.ParseText(csharp).GetRoot().DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>().Single(n => n.Name.ToString().EndsWith("Thing", StringComparison.Ordinal));
        var thing = ns.Members.OfType<BaseTypeDeclarationSyntax>().Where(t => t.Identifier.Text == "Thing").ToList();
        var delegateThing = ns.Members.OfType<DelegateDeclarationSyntax>().Where(t => t.Identifier.Text == "Thing").ToList();
        (thing.Count + delegateThing.Count).Should().Be(1, $"[{kind}×{mode}] Thing is a sibling in the module namespace\n{csharp}");
        if (thing.Count == 1 && thing[0] is TypeDeclarationSyntax typed)
            (typed.TypeParameterList?.Parameters.Count ?? 0).Should().Be(arity, $"[{kind}×{mode}]");
        var members = ns.Members.OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "ThingModule");
        members.Members.OfType<MethodDeclarationSyntax>().Should().Contain(m => m.Identifier.Text == "Helper", $"[{kind}×{mode}]");
        members.Members.OfType<BaseTypeDeclarationSyntax>().Should().BeEmpty($"[{kind}×{mode}] no type is nested in the members class");
    }

    /// <summary>
    /// A library module holding ONLY the class (no module-level member) — the #2039 repro. At
    /// d17ddb956 <c>class Thing[T]</c> alone printed <c>7</c>; the uniform SPY0520 refusal (ruled
    /// 2026-09-24) turned it into refused; the module-as-namespace layout makes it run again, in the
    /// same layout as every other module: <c>Merge.Thing.Thing&lt;T&gt;</c> beside an empty
    /// <c>[SharpyModule] ThingModule</c>. The non-generic class is the control.
    /// </summary>
    [Theory]
    [InlineData("generic_class")]
    [InlineData("class")]
    public void LibraryModuleHoldingOnlyTheType_Runs(string kind)
    {
        var (declaration, _, _) = Kinds[kind];
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Merge").WithEntryPoint("main.spy");
        helper.AddSourceFile("thing.spy", declaration);
        helper.AddSourceFile("main.spy", "from thing import Thing\n\ndef main() -> None:\n    print(" + Construction(kind) + ")\n");
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();
        var errors = helper.LastCompilationResult?.Diagnostics.GetErrors()
            .Select(d => (Code: d.Code ?? "", d.Message)).ToList() ?? new List<(string Code, string Message)>();

        exec.Success.Should().BeTrue(string.Join(" | ", errors.Select(e => e.Code + " " + e.Message)));
        exec.StandardOutput.Trim().Should().Be("7");
    }

    [Fact]
    public void Matrix_IsTotal() => Cells().Should().HaveCount(24, "8 kinds × 3 modes");
}
