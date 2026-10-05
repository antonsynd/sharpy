using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The <c>let</c> introduce seam (#1974, P21a Phase 2) in the positions the generative binding-law
/// matrix (<c>BlockKinds.BindingLawCells</c>) does not range:
/// <list type="bullet">
///   <item>the class-attribute rule (SPY0606): a <c>let</c> target inside a method is the explicit
///     new local, never refused, in the plain, annotated, tuple and starred spellings; the bare
///     store stays refused and its steer now offers <c>let</c> before the annotated shadow;</item>
///   <item>inline <c>out let v[: T]</c> (R-BM): a fresh binding typed by the callee, discriminated
///     from the rebinding <c>out v</c> / <c>out v: auto</c> controls by printing the OUTER value
///     after the block.</item>
/// </list>
/// Every run cell prints a value the other reading would not.
/// </summary>
public class LetIntroduceSeamTests : IntegrationTestBase
{
    public LetIntroduceSeamTests(ITestOutputHelper output) : base(output) { }

    // ── SPY0606: a `let` target in a method is the explicit new local ─────────────────────────

    private static string CounterProgram(string storeLines, string result) =>
        "class Counter:\n    count: int = 0\n\n"
        + "    def bump(self) -> int:\n"
        + string.Concat(storeLines.Split('\n').Select(l => "        " + l + "\n"))
        + $"        return {result}\n\n"
        + "def main():\n    c = Counter()\n    print(c.bump())\n    print(c.count)\n";

    [Theory]
    [InlineData("let count = 5", "count", "5")]
    [InlineData("let count: int = 5", "count", "5")]
    [InlineData("let count, other = 5, 6", "count + other", "11")]
    [InlineData("let count, *rest = [5, 6, 7]", "count + len(rest)", "7")]
    public void LetOverAClassAttribute_InAMethod_IsAFreshLocal_NotSpy0606(string store, string result, string expected)
    {
        var run = CompileAndExecute(CounterProgram(store, result));

        run.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.SemanticOverflow.ClassAttributeBareStore);
        run.Success.Should().BeTrue(Describe(run));
        run.StandardOutput.ReplaceLineEndings("\n").Trim().Should().Be($"{expected}\n0",
            "the let binds a local; the class attribute is untouched");
    }

    [Theory]
    [InlineData("count = 5", "Cannot assign to class attribute 'count'")]
    [InlineData("count, other = 5, 6", "Cannot unpack into class attribute 'count'")]
    public void BareStoreToAClassAttribute_StaysSpy0606_AndSteersToLetBeforeTheAnnotation(string store, string refusal)
    {
        var run = CompileAndExecute(CounterProgram(store, "count"));

        run.Success.Should().BeFalse();
        var diagnostic = run.RawDiagnostics.Should()
            .ContainSingle(d => d.Code == DiagnosticCodes.SemanticOverflow.ClassAttributeBareStore, Describe(run)).Subject;
        diagnostic.Message.Should().Contain(refusal);
        var letSteer = diagnostic.Message.IndexOf("'let count = ...' to declare a new local", StringComparison.Ordinal);
        var annotatedSteer = diagnostic.Message.IndexOf("'count: <type> = ...' to declare a shadowing local", StringComparison.Ordinal);
        letSteer.Should().BeGreaterThan(-1, diagnostic.Message);
        annotatedSteer.Should().BeGreaterThan(letSteer, "the let steer comes before the annotated shadow");
    }

    // ── out let (R-BM) ─────────────────────────────────────────────────────────────────────────

    private const string TryParse =
        "def try_parse(s: str, result: out int) -> bool:\n    result = int(s)\n    return True\n\n";

    [Theory]
    [InlineData("out let v", "42\n1")]         // fresh: the outer v is untouched
    [InlineData("out let v: int", "42\n1")]    // fresh, explicitly typed
    [InlineData("out v", "42\n42")]            // control: writes through to the outer v
    [InlineData("out v: auto", "42\n42")]      // control: rebinds the outer v
    public void OutArgument_OverAnOuterVariable_InABlock(string argument, string expected)
    {
        var run = CompileAndExecute(TryParse
            + "def main():\n    v: int = 1\n    if True:\n"
            + $"        ok = try_parse(\"42\", {argument})\n        print(v)\n    print(v)\n");

        run.Success.Should().BeTrue(Describe(run));
        run.StandardOutput.ReplaceLineEndings("\n").Trim().Should().Be(expected);
    }

    [Fact]
    public void OutLet_WithNoPredecessor_DeclaresAndTakesTheCalleesType()
    {
        var run = CompileAndExecute(TryParse
            + "def main():\n    if try_parse(\"41\", out let v):\n        print(v + 1)\n");

        run.Success.Should().BeTrue(Describe(run));
        run.StandardOutput.Trim().Should().Be("42");
    }

    [Fact]
    public void OutLet_InSiblingBlocks_IsFreshInEach()
    {
        var run = CompileAndExecute(TryParse
            + "def main():\n    if True:\n        ok = try_parse(\"42\", out let v)\n        print(v)\n"
            + "    if True:\n        ok = try_parse(\"7\", out let v)\n        print(v)\n");

        run.Success.Should().BeTrue(Describe(run));
        run.StandardOutput.ReplaceLineEndings("\n").Trim().Should().Be("42\n7");
    }

    [Fact]
    public void OutLet_OverASameScopeConst_IsSpy0225()
    {
        var run = CompileAndExecute(TryParse
            + "def main():\n    const v: int = 1\n    ok = try_parse(\"42\", out let v)\n    print(v)\n");

        run.Success.Should().BeFalse();
        run.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.InvalidAssignmentTarget
            && d.Message.Contains("Cannot redefine constant variable 'v'"), Describe(run));
    }

    [Fact]
    public void OutLet_WithAnUnresolvedCallee_IsRefusedByName()
    {
        var run = CompileAndExecute(
            "def main() -> None:\n    ok: bool = totally_undefined_function(\"42\", out let v)\n    print(v)\n");

        run.Success.Should().BeFalse();
        run.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.UndefinedMember
            && d.Message.Contains("Cannot infer the type of 'out let v'"), Describe(run));
        run.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
    }

    private static string Describe(ExecutionResult result)
        => string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Severity} {d.Code} ({d.Line}:{d.Column}) {d.Message}"));
}
