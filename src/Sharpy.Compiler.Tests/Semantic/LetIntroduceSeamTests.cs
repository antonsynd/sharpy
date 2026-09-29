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
///     store stays refused and its steer now offers <c>let</c> before the annotated shadow.</item>
/// </list>
/// Every run cell prints a value the other reading would not.
/// </summary>
[Collection("HeavyCompilation")]
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

    private static string Describe(ExecutionResult result)
        => string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Severity} {d.Code} ({d.Line}:{d.Column}) {d.Message}"));
}
