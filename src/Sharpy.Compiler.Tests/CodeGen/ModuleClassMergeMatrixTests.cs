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
/// A type named like its file (#1919): the merge decision lives ONCE on
/// <c>ModuleShape.MergedClassName</c> (computed by <c>ComputeModuleShape</c> with the arity axis) and
/// <c>GenerateModuleMembers</c> reads it — the arity-less second copy that merged the module into
/// <c>Thing&lt;T&gt;</c> (CS5001 under <c>run</c>, CS0305 under <c>project</c>, both SPY0908) is gone.
///
/// <para><b>Cells.</b> colliding kind {class, dataclass, generic class, generic dataclass, struct,
/// union, interface, enum} × mode {<c>run</c> — the colliding file is the entry and has
/// <c>main</c>; <c>project</c> — the colliding file is an imported library module without
/// <c>main</c>, whose function <c>main.spy</c> calls}. A non-generic class or dataclass MERGES and runs (the module class IS the user
/// class). A generic one cannot merge (the module class has arity 0) and cannot coexist either: it
/// is nested inside the module class, and C# compares a nested type's name without its arity
/// (CS0542, measured) — so it is refused by name (SPY0520) with the other kinds that cannot merge.
/// struct/union/interface/enum stay SPY0520 (control).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ModuleClassMergeMatrixTests : IntegrationTestBase
{
    public ModuleClassMergeMatrixTests(ITestOutputHelper output) : base(output) { }

    private static readonly Dictionary<string, (string Declaration, bool Merges)> Kinds = new()
    {
        ["class"] = ("class Thing:\n    v: int\n\n    def __init__(self, v: int) -> None:\n        self.v = v\n", true),
        ["dataclass"] = ("@dataclass\nclass Thing:\n    v: int\n", true),
        ["generic_class"] = ("class Thing[T]:\n    v: T\n\n    def __init__(self, v: T) -> None:\n        self.v = v\n", false),
        ["generic_dataclass"] = ("@dataclass\nclass Thing[T]:\n    v: T\n", false),
        ["struct"] = ("struct Thing:\n    v: int\n", false),
        ["union"] = ("union Thing:\n    case A(v: int)\n    case B()\n", false),
        ["interface"] = ("interface Thing:\n    def f(self) -> int: ...\n", false),
        ["enum"] = ("enum Thing:\n    A = 1\n", false),
    };

    private static string Construction(string kind) => kind.StartsWith("generic") ? "Thing[int](7).v" : "Thing(7).v";

    public static IEnumerable<object[]> Cells()
        => from kind in Kinds.Keys
           from mode in new[] { "run", "project" }
           select new object[] { kind, mode };

    [Theory]
    [MemberData(nameof(Cells))]
    public void TypeNamedLikeItsFile_MergesOnlyWhenNonGenericClass(string kind, string mode)
    {
        var (declaration, merges) = Kinds[kind];
        var use = merges ? $"print({Construction(kind)})" : "print(7)";

        IReadOnlyList<(string Code, string Message)> errors;
        bool success;
        string stdout;
        string csharp;
        if (mode == "run")
        {
            var source = declaration + "\ndef main() -> None:\n    " + use + "\n";
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
            // The library module also declares a function main imports, so the module is reached (an
            // un-imported module's codegen diagnostics are not surfaced today — #2028).
            helper.AddSourceFile("thing.spy", declaration + "\ndef helper() -> int:\n    return 7\n");
            helper.AddSourceFile("main.spy", (merges ? "from thing import Thing, helper\n\n" : "from thing import helper\n\n")
                + "def main() -> None:\n    " + (merges ? use : "print(helper())") + "\n");
            helper.CreateProjectFile();
            var exec = helper.CompileAndExecute();
            success = exec.Success;
            stdout = exec.StandardOutput;
            csharp = helper.LastCompilationResult != null
                ? string.Concat(helper.LastCompilationResult.GeneratedCSharpFiles.Values) : "";
            errors = helper.LastCompilationResult?.Diagnostics.GetErrors()
                .Select(d => (d.Code ?? "", d.Message)).ToList() ?? new List<(string, string)>();
        }

        errors.Should().NotContain(e => e.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{kind}×{mode}] never CS5001/CS0305/CS0542 behind SPY0908");

        if (merges)
        {
            success.Should().BeTrue($"[{kind}×{mode}] {string.Join(" | ", errors.Select(e => e.Message))}");
            stdout.Trim().Should().Be("7", $"[{kind}×{mode}]");
            // The merge is visible in the tree: ONE class `Thing` (arity 0) declares both the user
            // member `V` and the module's members.
            var things = CSharpSyntaxTree.ParseText(csharp).GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>().Where(c => c.Identifier.Text == "Thing").ToList();
            things.Should().ContainSingle($"[{kind}×{mode}] the user class IS the module class\n{csharp}");
            things[0].TypeParameterList.Should().BeNull();
        }
        else
        {
            success.Should().BeFalse($"[{kind}×{mode}] must be refused");
            errors.Should().Contain(e => e.Code == DiagnosticCodes.CodeGen.NameCollision
                && e.Message.StartsWith("Type 'Thing' conflicts with module class name 'Thing'")
                && e.Message.EndsWith("Rename the type or the source file to avoid this collision."),
                $"[{kind}×{mode}] {string.Join(" | ", errors.Select(e => e.Code + " " + e.Message))}");
        }
    }

    [Fact]
    public void Matrix_IsTotal() => Cells().Should().HaveCount(16, "8 kinds × 2 modes");
}
