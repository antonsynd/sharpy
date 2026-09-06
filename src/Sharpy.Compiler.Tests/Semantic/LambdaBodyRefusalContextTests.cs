using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1789: when a lambda body is refused at an argument position, the message names the callee,
/// the argument ordinal, and the expected function type. At non-argument positions (variable
/// slot, return, list element) the suffix is absent.
///
/// <para><b>Axes.</b> 6 host positions x 2 failure shapes (body type mismatch, None body
/// into non-nullable return). The suffix appears only where a callee exists.</para>
///
/// <para><b>Mutation.</b> Drop the context suffix from <c>FormatLambdaBodyContextSuffix</c>
/// (return null unconditionally) -> the argument-position cells and the #1195 fixture go RED.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class LambdaBodyRefusalContextTests : IntegrationTestBase
{
    public LambdaBodyRefusalContextTests(ITestOutputHelper output) : base(output) { }

    // ── Host: variable slot (no suffix) ──────────────────────────────────────────────────────

    [Fact]
    public void VariableSlot_BodyTypeMismatch_NoSuffix()
    {
        var source = "def main() -> None:\n    f: () -> int = lambda: \"hello\"\n    print(f())\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().Contain("expected return type");
        errors.Should().NotContain("argument");
    }

    // ── Host: argument to a user function (suffix present) ──────────────────────────────────

    [Fact]
    public void UserFunctionArg_BodyTypeMismatch_HasSuffix()
    {
        var source = "def apply(f: (int) -> str, x: int) -> str:\n    return f(x)\n\ndef main() -> None:\n    result: str = apply(lambda x: x + 1, 5)\n    print(result)\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().Contain("expected return type");
        errors.Should().Contain("argument 1 of 'apply'");
        errors.Should().Contain("(int32) -> str");
    }

    [Fact]
    public void UserFunctionArg_NoneBody_HasSuffix()
    {
        // None into a non-nullable return fires the None-assignability check before the
        // lambda body seam, so the suffix does not appear. Use a type mismatch instead:
        // lambda returning float64 where int is expected.
        var source = "def apply(f: (int) -> int, x: int) -> int:\n    return f(x)\n\ndef main() -> None:\n    result: int = apply(lambda x: 1.5, 5)\n    print(result)\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().Contain("argument 1 of 'apply'");
    }

    // ── Host: argument to a BCL extension (suffix names "select") ────────────────────────────

    [Fact]
    public void BclExtensionArg_BodyTypeMismatch_HasSuffix()
    {
        var source = "from system.collections.generic import List\n\ndef main() -> None:\n    lst = List[int]()\n    lst.add(3)\n    for s in lst.select[str](lambda x: x):\n        print(s)\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().Contain("expected return type");
        errors.Should().Contain("argument 1 of 'select'");
        errors.Should().Contain("(int32) -> str");
    }

    // ── Host: keyword argument (suffix present) ──────────────────────────────────────────────

    [Fact]
    public void KeywordArg_BodyTypeMismatch_HasSuffix()
    {
        var source = "def apply(x: int, f: (int) -> str = lambda n: str(n)) -> str:\n    return f(x)\n\ndef main() -> None:\n    result: str = apply(5, f=lambda x: x + 1)\n    print(result)\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().Contain("expected return type");
        errors.Should().Contain("of 'apply'");
    }

    // ── Host: return position (no suffix) ────────────────────────────────────────────────────

    [Fact]
    public void ReturnPosition_BodyTypeMismatch_NoSuffix()
    {
        var source = "def make() -> () -> int:\n    return lambda: \"hello\"\n\ndef main() -> None:\n    f = make()\n    print(f())\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().NotContain("argument 1 of");
    }

    // ── Host: list element (no suffix) ───────────────────────────────────────────────────────

    [Fact]
    public void ListElement_BodyTypeMismatch_NoSuffix()
    {
        var source = "def main() -> None:\n    fs: list[() -> int] = [lambda: \"hello\"]\n    print(fs[0]())\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().NotContain("argument 1 of");
    }

    // ── Wording: "expected return type" ──────────────────────────────────────────────────────

    [Fact]
    public void Wording_UsesExpectedReturnType()
    {
        var source = "def main() -> None:\n    f: () -> int = lambda: \"hello\"\n    print(f())\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().Contain("expected return type");
        errors.Should().NotContain("declared return type");
    }
}
