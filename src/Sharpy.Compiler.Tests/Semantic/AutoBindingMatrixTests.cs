using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1675: an inline <c>out x: auto</c>/<c>ref x: auto</c> binding takes the RESOLVED callee's
/// parameter type — a Sharpy-declared function (via <c>ValidateCallArguments</c>/
/// <c>ValidateKeywordArguments</c>), a CLR static overload set, or a CLR instance method (both via
/// <c>CheckClrBindingArguments</c>) — instead of staying <c>Unknown</c> under a standing
/// <c>DeliberatelyPermissive</c> mark; a callee that never resolves at all is refused BY NAME
/// (SPY0203) rather than silently permitted. Matrix: callee kind × probe.
///
/// <para>Direction (measured @ plan-d35e69 baseline 41dd19de7, before this fix): a02's <c>b: bool
/// = v</c> was SPY0908/CS0029 with NO SPY0220 — the checker never objected because <c>v</c> was
/// still <c>Unknown</c>, so the mismatch reached Roslyn as an internal-error ICE instead of a
/// semantic-time diagnostic naming the real type. a08's <c>v.nonexistent()</c> was CS1061 behind
/// SPY0908 for the same reason. Both cells assert the AFTER state (SPY0220/SPY0203 respectively,
/// no SPY0908) as the acceptance.</para>
/// </summary>
public class AutoBindingMatrixTests : IntegrationTestBase
{
    public AutoBindingMatrixTests(ITestOutputHelper output) : base(output) { }

    private static bool HasCode(ExecutionResult result, string code)
        => result.RawDiagnostics.Exists(d => d.Code == code);

    private static bool MentionsCs(ExecutionResult result, string csCode)
        => result.CompilationErrors.Exists(e => e.Contains(csCode));

    // ═══════════════════════════════════════════════════════════════════════
    // CLR static overload callee: int.try_parse("42", out v: auto)  (a01, a02, a06, a08)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ClrStatic_AutoOutBinding_Runs()
    {
        var result = CompileAndExecute("""
            def main() -> None:
                ok: bool = int.try_parse("42", out v: auto)
                print(ok)
                print(v)
            """);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("True\n42\n", result.StandardOutput);
    }

    [Fact]
    public void ClrStatic_AutoOutBinding_TypedUseInABoolSlot_IsSpy0220_NotSpy0908()
    {
        // a02: the direct-fix acceptance. v took int32 from try_parse's out formal, so assigning
        // it to a bool is a NAMED type mismatch — not a silent Unknown that reaches Roslyn as
        // CS0029 behind SPY0908.
        var result = CompileAndExecute("""
            def main() -> None:
                ok: bool = int.try_parse("42", out v: auto)
                b: bool = v
                print(b)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.TypeMismatch),
            $"Expected SPY0220, got: {string.Join(", ", result.CompilationErrors)}");
        Assert.False(HasCode(result, DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError),
            "auto must not reach Roslyn as an ICE — the checker types it now (#1675)");
        Assert.False(MentionsCs(result, "CS0029"));
    }

    [Fact]
    public void ClrStatic_ExplicitOutInt_TypedUseInABoolSlot_IsSpy0220_PositiveControl()
    {
        // a06: the explicit-annotation control, unaffected by this change (it was already typed).
        // Same outcome as the auto cell above proves the checker treats both spellings alike.
        var result = CompileAndExecute("""
            def main() -> None:
                ok: bool = int.try_parse("42", out v: int)
                b: bool = v
                print(b)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.TypeMismatch),
            $"Expected SPY0220, got: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void ClrStatic_AutoOutBinding_NonexistentMember_IsSpy0203_NotSpy0908()
    {
        // a08: v is int32 now, so a member access it doesn't have is a NAMED "no member" refusal —
        // not a permissive Unknown that reaches Roslyn as CS1061 behind SPY0908.
        var result = CompileAndExecute("""
            def main() -> None:
                ok: bool = int.try_parse("42", out v: auto)
                v.nonexistent()
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.UndefinedMember),
            $"Expected SPY0203, got: {string.Join(", ", result.CompilationErrors)}");
        Assert.False(HasCode(result, DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError));
        Assert.False(MentionsCs(result, "CS1061"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CLR instance callee: Dictionary[str, int].try_get_value(k, out v: auto)  (a05)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ClrInstance_AutoOutBinding_Runs()
    {
        var result = CompileAndExecute("""
            from system.collections.generic import Dictionary

            def main() -> None:
                d: Dictionary[str, int] = Dictionary[str, int]()
                d.add("a", 1)
                ok: bool = d.try_get_value("a", out v: auto)
                print(ok)
                print(v)
            """);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("True\n1\n", result.StandardOutput);
    }

    [Fact]
    public void ClrInstance_AutoOutBinding_TypedUseInABoolSlot_IsSpy0220()
    {
        var result = CompileAndExecute("""
            from system.collections.generic import Dictionary

            def main() -> None:
                d: Dictionary[str, int] = Dictionary[str, int]()
                d.add("a", 1)
                ok: bool = d.try_get_value("a", out v: auto)
                b: bool = v
                print(b)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.TypeMismatch),
            $"Expected SPY0220, got: {string.Join(", ", result.CompilationErrors)}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // User callee with an `out` parameter: def g(r: out int)  (a03b, a04b)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void UserOutCallee_AutoOutBinding_Runs()
    {
        var result = CompileAndExecute("""
            def g(r: out int) -> None:
                r = 7

            def main() -> None:
                g(out r: auto)
                print(r)
            """);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("7\n", result.StandardOutput);
    }

    [Fact]
    public void UserOutCallee_AutoOutBinding_TypedUseInABoolSlot_IsSpy0220()
    {
        var result = CompileAndExecute("""
            def g(r: out int) -> None:
                r = 7

            def main() -> None:
                g(out r: auto)
                b: bool = r
                print(b)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.TypeMismatch),
            $"Expected SPY0220, got: {string.Join(", ", result.CompilationErrors)}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Chained rebinding: `out v: auto` on an already-bound v takes the EXISTING binding's type,
    // not the callee's (#1301's rebinding rule) — a10, a positive control that this feature does
    // not disturb.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Rebinding_AutoOutOnAlreadyTypedVariable_KeepsExistingType_IsSpy0220()
    {
        var result = CompileAndExecute("""
            def main() -> None:
                v: int = 5
                ok: bool = int.try_parse("42", out v: auto)
                b: bool = v
                print(b)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.TypeMismatch),
            $"Expected SPY0220, got: {string.Join(", ", result.CompilationErrors)}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Unresolved callee: the new named refusal (SPY0203) that replaces the standing DP permit —
    // the mutation-test target (removing the write-back/refusal turns this cell either silently
    // permissive again or an SPY0908 ICE, depending on what unravels first).
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void UnresolvedCallee_AutoOutBinding_IsRefusedByName_NotSilentlyPermitted()
    {
        var result = CompileAndExecute("""
            def main() -> None:
                ok: bool = totally_undefined_function("42", out v: auto)
                print(v)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Semantic.UndefinedMember),
            $"Expected SPY0203 for the un-inferable 'auto', got: {string.Join(", ", result.CompilationErrors)}");
        Assert.False(HasCode(result, DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError),
            "an unresolved callee's auto binding must never reach codegen as an Unknown-typed leak");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // The `ref` twin is a parse refusal, not a semantic one — `ref x: auto` is not a form
    // (N/A-by-parser, plan-d35e69 §Phase 5 Current State). Recorded so the cell is not silently
    // missing from the matrix.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void RefAutoTwin_IsAParseRefusal_NotAFormAtAll()
    {
        var result = CompileAndExecute("""
            def g(r: int) -> None:
                pass

            def main() -> None:
                g(ref n: auto)
            """);

        Assert.False(result.Success);
        Assert.True(HasCode(result, DiagnosticCodes.Parser.ExpectedToken),
            $"Expected SPY0104 (parse refusal — 'ref x: auto' is not a form), got: {string.Join(", ", result.CompilationErrors)}");
    }
}
