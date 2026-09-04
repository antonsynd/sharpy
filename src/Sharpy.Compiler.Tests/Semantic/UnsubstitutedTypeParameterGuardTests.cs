using System;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// No diagnostic may render an UNSUBSTITUTED type parameter (#1728). A generic call whose
/// argument type is still <c>Unknown</c> — which is what a forward reference to a not-yet-checked
/// <c>const</c> produces — used to answer with the callee's raw return type, so
/// <c>const A: int = max(B, 1)</c> reported "Cannot assign type <c>'T'</c> to variable of type
/// <c>'int32'</c>": a message naming a type parameter of <c>Builtins.Max&lt;T&gt;</c>, which no
/// user program ever wrote. <c>InferGenericReturnType</c> now substitutes <c>Unknown</c> for any
/// parameter left unbound, and this is the guard that keeps it substituted.
///
/// <para><b>The absence assertion is paired with a positive control.</b> "No message contains
/// <c>'T'</c>" passes vacuously for a program that reports nothing at all, and it would also pass
/// if the scan looked for a token no message can contain. So the last test asserts the OPPOSITE
/// direction on a program where <c>T</c> is genuinely in scope: inside <c>class Box[T]</c>,
/// <c>x: int = self.value</c> must still name <c>'T'</c>, because there the type parameter IS the
/// user's own. Mutating <see cref="TypeParameterToken"/> to a token no diagnostic can hold turns
/// that control red while every absence assertion stays green — which is how this file was shown
/// falsifiable.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class UnsubstitutedTypeParameterGuardTests : IntegrationTestBase
{
    public UnsubstitutedTypeParameterGuardTests(ITestOutputHelper output) : base(output) { }

    /// <summary>The rendering the guard forbids: the display name of an unbound type parameter.</summary>
    private const string TypeParameterToken = "'T'";

    /// <summary>
    /// The cycle cells (#1728 e1, e2, e5): every one is refused by name — and the refusal is
    /// SPY0278 on both constants, never a type mismatch that names a callee's type parameter.
    /// </summary>
    [Theory]
    [InlineData("const A: int = max(B, 1)\nconst B: int = max(A, 1)\n", "e1: both sides through max()")]
    [InlineData("const A: int = max(B, 1)\nconst B: int = A + 1\n", "e2: one side through max()")]
    [InlineData("def ident(v: int) -> int:\n    return v\n\nconst A: int = ident(B)\nconst B: int = ident(A)\n",
        "e5: both sides through a user function")]
    [InlineData("const A: float = max(B, 1.0)\nconst B: float = max(A, 1.0)\n", "e1's float twin")]
    public void ConstCycle_IsRefusedBySPY0278_AndNoDiagnosticNamesATypeParameter(string decls, string cell)
    {
        var result = CompileAndExecute(decls + "\ndef main():\n    print(1)\n");

        result.Success.Should().BeFalse($"{cell} is a cycle");
        result.RawDiagnostics.Count(d => d.Code == DiagnosticCodes.Semantic.CircularConstantReference)
            .Should().Be(2, $"{cell} names both constants on the cycle");
        AssertNoDiagnosticNamesATypeParameter(result, cell);
    }

    /// <summary>
    /// The inference-failure cells: a generic callee whose argument is already <c>Unknown</c>
    /// (an undefined name) cannot bind its own type parameter, and the call's type must become
    /// <c>Unknown</c> rather than the raw <c>T</c> — otherwise the store one token later reports
    /// "Cannot assign type 'T' …". These are the cells the const forward references no longer
    /// reach (the const pre-pass types them by annotation), so they are what keeps
    /// <c>FinalizeCallReturnType</c> load-bearing: with it reverted, every row below renders 'T'
    /// (measured 2026-09-04 @ d5b4d4bb3 — both spellings leaked before the seam).
    /// </summary>
    [Theory]
    [InlineData("def main() -> None:\n    x: str = max(nope, 1)\n    print(x)\n",
        "builtin max: one Unknown argument beside a constant")]
    [InlineData("def main() -> None:\n    y: int = min(missing_a, missing_b)\n    print(y)\n",
        "builtin min: every argument Unknown")]
    [InlineData("def first[T](xs: list[T]) -> T:\n    return xs[0]\n\ndef main() -> None:\n    z: int = first(nope)\n    print(z)\n",
        "user generic function: Unknown argument")]
    public void GenericCallOverUnknownArgument_ReportsOnlyTheUndefinedName_NeverT(string program, string cell)
    {
        var result = CompileAndExecute(program);

        result.Success.Should().BeFalse($"{cell}: the undefined name is refused");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.UndefinedVariable,
            $"{cell}: the undefined name is the diagnostic (positive control that the program is checked)");
        AssertNoDiagnosticNamesATypeParameter(result, cell);
    }

    /// <summary>
    /// The opposite direction, and the reason the rule is scope-aware: a type parameter of the
    /// ENCLOSING class is in scope, so a generic call whose inference binds to it keeps that type
    /// and the mistyped store is refused by name — never erased to Unknown (which ICEd with CS0029
    /// between a1b22ed94 and this fix; SPY0220 naming 'U' at f7c7d3d97).
    /// </summary>
    [Fact]
    public void GenericCallBoundToAnEnclosingClassParameter_KeepsThatParameter_AndRefusesByName()
    {
        var result = CompileAndExecute(@"
def first[T](xs: list[T]) -> T:
    return xs[0]

class Box[U]:
    items: list[U]
    def __init__(self, items: list[U]) -> None:
        self.items = items
    def bad(self) -> int:
        x: int = first(self.items)
        return x

def main() -> None:
    b: Box[str] = Box([""a""])
    print(b.bad())
");
        result.Success.Should().BeFalse("storing a U into an int32 is a type mismatch");
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            "the class parameter is in scope; erasing it to Unknown is what ICEd");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch && d.Message.Contains("'U'", StringComparison.Ordinal),
            "the refusal names the in-scope parameter; got "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
    }

    /// <summary>
    /// The forward-reference cells (#1728 e4 and its float twin): acyclic, so they RUN — the leak
    /// was a forward-reference defect, not a cycle-detection one, and the value proves the const's
    /// declared type carried the inference.
    /// </summary>
    [Theory]
    [InlineData("const A: int = max(B, 1)\nconst B: int = 4\n", "4 4", "e4: int forward reference")]
    [InlineData("const A: float = max(B, 1.0)\nconst B: float = 4.0\n", "4.0 4.0", "e4's float twin")]
    public void ConstForwardReference_Runs_AndNoDiagnosticNamesATypeParameter(
        string decls, string expected, string cell)
    {
        var result = CompileAndExecute(decls + "\ndef main():\n    print(A, B)\n");

        AssertNoDiagnosticNamesATypeParameter(result, cell);
        result.Success.Should().BeTrue(
            $"{cell} has no cycle: " + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be(expected, cell);
    }

    /// <summary>
    /// The positive control. <c>T</c> in <c>class Box[T]</c> is the user's own type parameter, in
    /// scope at the store, so the message MUST name it — this is what makes the four absence
    /// assertions above measurements rather than vacuous truths, and what fails if the scanned
    /// token is changed to one no diagnostic can contain.
    /// </summary>
    [Fact]
    public void TypeParameterInScope_IsStillNamedByItsDiagnostic()
    {
        var result = CompileAndExecute(@"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

    def unwrap(self) -> None:
        x: int = self.value
        print(x)

def main():
    b: Box[int] = Box[int](3)
    b.unwrap()
");

        result.Success.Should().BeFalse("storing a T into an int32 is a type mismatch");
        result.RawDiagnostics.Should().Contain(
            d => d.Message.Contains(TypeParameterToken, StringComparison.Ordinal),
            "a type parameter that IS in scope is named by its diagnostic; got "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
    }

    private static void AssertNoDiagnosticNamesATypeParameter(ExecutionResult result, string cell)
    {
        // SPY0237 ("Type parameter 'T' cannot be inferred") is ABOUT the parameter it names — the
        // one diagnostic whose subject is the type parameter itself, not a type that leaked one.
        var offenders = result.RawDiagnostics
            .Where(d => d.Code != DiagnosticCodes.Semantic.CannotInferGenericType)
            .Where(d => d.Message.Contains(TypeParameterToken, StringComparison.Ordinal))
            .Select(d => $"{d.Code}:{d.Message}")
            .ToList();

        offenders.Should().BeEmpty(
            $"{cell} must not report a message naming an unsubstituted type parameter");
    }
}
