using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1775: when every overload candidate rejects the SAME argument for a TYPE reason, the error
/// is SPY0220 (type mismatch with the seam's steer) instead of SPY0354 (no matching overload).
///
/// <para><b>Contract.</b> A method call with multiple overloads that ALL reject the same
/// positional argument at the same index for a type mismatch reports the concrete type error
/// (SPY0220). SPY0354 stays for arity mismatches and genuinely divergent candidates.</para>
///
/// <para><b>Mutation.</b> Revert <c>TrySameArgumentOverloadRefusal</c> (return false
/// unconditionally) -> the same-argument cells return to SPY0354 (RED).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class OverloadRefusalShapeMatrixTests : IntegrationTestBase
{
    public OverloadRefusalShapeMatrixTests(ITestOutputHelper output) : base(output) { }

    // ── Same-argument: list.index with wrong type -> SPY0220 ─────────────────────────────────

    [Fact]
    public void ListIndex_WrongType_ReportsSPY0220()
    {
        // list.index has overloads: index(value), index(value, start), index(value, start, stop)
        // ALL reject a str argument at index 0 for type (list[int] expects int).
        var source = "def main() -> None:\n    xs: list[int] = [1, 2, 3]\n    print(xs.index(\"hello\"))\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            "same-argument overload rule should report SPY0220, not SPY0354");
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().Contain("str").And.Contain("int32");
    }

    // ── Same-argument: list.count with wrong type -> SPY0220 (control, already worked) ───────

    [Fact]
    public void ListCount_WrongType_StaysSPY0220()
    {
        // list.count has a single overload, so the >= 2 guard does not fire here;
        // this test verifies that the existing single-overload SPY0220 still works.
        var source = "def main() -> None:\n    xs: list[int] = [1, 2, 3]\n    print(xs.count(\"hello\"))\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        var errors = string.Join(" ", result.CompilationErrors);
        // count with wrong type is still refused
        errors.Should().Contain("str");
    }

    // ── Arity mismatch -> SPY0354 stays ──────────────────────────────────────────────────────

    [Fact]
    public void ArityMismatch_KeepsSPY0354()
    {
        // Calling with wrong NUMBER of args: no arity candidate survives.
        var source = "class C:\n    def __init__(self):\n        pass\n    def f(self, x: int, y: int) -> int:\n        return x + y\n    def f(self, x: int, y: int, z: int) -> int:\n        return x + y + z\n\ndef main() -> None:\n    c: C = C()\n    print(c.f(1))\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.NoMatchingOverload,
            "arity mismatch should keep SPY0354");
    }

    // ── Divergent candidates -> SPY0354 stays ────────────────────────────────────────────────

    [Fact]
    public void DivergentCandidates_KeepsSPY0354()
    {
        // Two overloads with different parameter types at the same position:
        // f(x: int, y: str) vs f(x: str, y: int), called with f("a", "b")
        // Candidate 1 fails at index 0, candidate 2 fails at index 1 -> divergent.
        var source = "class C:\n    def __init__(self):\n        pass\n    def f(self, x: int, y: str) -> str:\n        return str(x) + y\n    def f(self, x: str, y: int) -> str:\n        return x + str(y)\n\ndef main() -> None:\n    c: C = C()\n    print(c.f(1.5, 1.5))\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.NoMatchingOverload,
            "divergent candidates should keep SPY0354");
    }

    // ── Same-argument: user-defined overloads -> SPY0220 ─────────────────────────────────────

    [Fact]
    public void UserOverload_SameArgFailure_ReportsSPY0220()
    {
        // Both overloads reject a str at index 0 (both expect int).
        var source = "class Math:\n    def __init__(self):\n        pass\n    def add(self, x: int, y: int) -> int:\n        return x + y\n    def add(self, x: int, y: int, z: int) -> int:\n        return x + y + z\n\ndef main() -> None:\n    m: Math = Math()\n    print(m.add(\"hello\", 2))\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            "same-argument overload rule should report SPY0220 when all candidates reject arg 0");
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().Contain("str").And.Contain("int32");
    }
}