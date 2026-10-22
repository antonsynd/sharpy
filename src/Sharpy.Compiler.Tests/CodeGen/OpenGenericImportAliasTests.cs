using FluentAssertions;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// A from-import of a CLR-namespace name emits a <c>using</c> alias only when the name denotes no
/// generic type definition — C# cannot alias an open generic (CS0305) — and that is decided from
/// the imported symbol's materialized <c>CodeGenInfo.IsOpenGenericDefinition</c>, against the
/// compilation registry's OWN visibility (#2320, ruling R-HM (a)).
///
/// <para><b>Cells.</b> Visibility {framework, referenced package in a <c>System.*</c> namespace} ×
/// spelling {bare, <c>as</c> alias} × name {arity group with a generic member, non-generic only}.
/// The emitter used to ask reflection against the FRAMEWORK set alone, so a referenced
/// <c>System.Widgets.Box</c> beside <c>Box&lt;T&gt;</c> was aliased while the framework's
/// <c>Action</c> beside <c>Action&lt;T&gt;</c> was not; and the aliased spelling's fact rides
/// <c>SemanticBinding.MergeFrom</c>, where import resolution's earlier entry for the clone wins.</para>
///
/// <para>Every cell runs and prints: the alias set is the observable, but a program that stopped
/// running would be the regression. The non-generic names are the positive controls — an alias IS
/// emitted for them, so "no alias" cannot pass because the alias path never ran.</para>
/// </summary>
public class OpenGenericImportAliasTests : IntegrationTestBase
{
    public OpenGenericImportAliasTests(ITestOutputHelper output) : base(output) { }

    private const string WidgetsSource = """
        namespace System.Widgets
        {
            public class Box { public static string Describe() => "box"; }
            public class Box<T> { public T Value; public Box(T value) { Value = value; } }
            public class Pair<A, B> { public A First; public Pair(A first, B second) { First = first; } }
            public class Gadget { public static int Size() => 7; }
        }
        """;

    private static readonly Lazy<string> WidgetsAssembly =
        new(() => IsNoneReferenceCheckMatrixTests.BuildSpecimenAssembly("SystemWidgets2320", WidgetsSource));

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => base.GetAdditionalReferenceAssemblyPaths().Append(WidgetsAssembly.Value);

    [Fact]
    public void Framework_ArityGroupIsNotAliased_BareOrAsAlias()
    {
        var result = CompileAndExecute(
            "from system import Action, Math\n"
            + "from system import Action as A\n\n"
            + "def main() -> None:\n"
            + "    act: Action[int] = lambda x: print(x)\n"
            + "    act(9)\n"
            + "    print(Math.Max(3, 4))\n");

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Should().Be("9\n4\n");
        result.GeneratedCSharp.Should().Contain("using Math = global::System.Math;",
            "positive control: a non-generic framework name is aliased, so the alias path ran");
        result.GeneratedCSharp.Should().NotContain("using Action =",
            "Action is the Action / Action<T>… group — no alias for an open generic name");
        result.GeneratedCSharp.Should().NotContain("using A =",
            "the `as` spelling binds a clone whose import-resolution entry precedes the fact; the "
            + "fact must survive the merge");
    }

    [Fact]
    public void ReferencedSystemNamespace_ArityGroupIsNotAliased()
    {
        var result = CompileAndExecute(
            "from system.widgets import Box, Pair, Gadget\n\n"
            + "def main() -> None:\n"
            + "    p = Pair[str, int](\"a\", 1)\n"
            + "    print(p.First)\n"
            + "    print(Box.Describe())\n"
            + "    print(Gadget.Size())\n");

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Should().Be("a\nbox\n7\n");
        result.GeneratedCSharp.Should().Contain("using Gadget = global::System.Widgets.Gadget;",
            "positive control: a non-generic referenced name is aliased");
        result.GeneratedCSharp.Should().NotContain("using Box =",
            "Box<T> is visible to this compilation through its reference — the registry's "
            + "visibility, not the framework set alone, decides the alias");
        result.GeneratedCSharp.Should().NotContain("using Pair =");
    }
}
