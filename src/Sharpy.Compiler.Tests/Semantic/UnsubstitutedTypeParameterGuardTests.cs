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
        var offenders = result.RawDiagnostics
            .Where(d => d.Message.Contains(TypeParameterToken, StringComparison.Ordinal))
            .Select(d => $"{d.Code}:{d.Message}")
            .ToList();

        offenders.Should().BeEmpty(
            $"{cell} must not report a message naming an unsubstituted type parameter");
    }
}
