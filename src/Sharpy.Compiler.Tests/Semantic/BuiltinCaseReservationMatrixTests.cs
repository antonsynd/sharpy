using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The R-S reservation matrix (#1856). ONE classifier
/// (<see cref="Compiler.Semantic.BuiltinUnionCaseReservation"/>) keyed on the registry symbol's CLR
/// identity, consulted by every receiver spelling in call AND value position (SPY0608), and by every
/// declaration kind (SPY0212). A name-keyed test escaped every spelling but the bare one, and the
/// union-case declaration escaped the reservation entirely (a user <c>case Some</c> silently shadowed
/// the builtin pattern).
///
/// <para><b>Qualified-spelling axes.</b> Receiver spelling (5): {Bare <c>Optional</c>, Indexed
/// <c>Optional[int]</c>, AliasedName <c>O</c>, ModuleQualified <c>sharpy.Optional</c>, AliasedModule
/// <c>sh.Optional</c>} × case (4): {Some, None, Ok, Err} — each case paired with its own union
/// (Optional owns Some/None, Result owns Ok/Err) × position (2): {Call, Value}. Every cell -> SPY0608.</para>
///
/// <para><b>Declaration axes.</b> Declaration kind (9): seven that enter a bare or type namespace
/// (def, class, struct, variable, parameter, union case, type alias) -> SPY0212; two whose access is
/// always qualified and so cannot shadow the bare builtin (class member, enum member) -> allowed, a
/// naming warning only (controls) × case (3): {Some, Ok, Err}. <c>None</c> is a keyword, so a
/// declaration cannot spell it (parse SPY0101, N/A).</para>
///
/// <para><see cref="Cells_CoverEveryAxis_AnchoredToLiterals"/> anchors every axis size to a LITERAL,
/// not to the array it names, so the coverage claim cannot be vacuous.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class BuiltinCaseReservationMatrixTests : IntegrationTestBase
{
    public BuiltinCaseReservationMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string Spy0608 = DiagnosticCodes.SemanticOverflow.QualifiedTaggedUnionConstructor;
    private const string Spy0212 = DiagnosticCodes.Semantic.BuiltinNameShadowed;

    // ── Axis sizes, anchored to literals ─────────────────────────────────────────────────────
    private const int ReceiverSpellingCount = 5;
    private const int QualifiedCaseCount = 4;
    private const int PositionCount = 2;
    private const int RefusedDeclKindCount = 7;
    private const int ControlDeclKindCount = 2;
    private const int DeclCaseCount = 3; // None is a keyword, N/A as a declaration name

    /// <summary>A receiver spelling: its name, the prelude it needs, and the Optional/Result receiver
    /// text. The case's own union is chosen per case so Optional.Some and Result.Ok are both exercised.</summary>
    private sealed record Spelling(string Name, string Prelude, string OptionalReceiver, string ResultReceiver);

    private static readonly Spelling[] ReceiverSpellings =
    {
        new("Bare", "", "Optional", "Result"),
        new("Indexed", "", "Optional[int]", "Result[int, str]"),
        new("AliasedName",
            "from sharpy import Optional as O\nfrom sharpy import Result as R\n", "O", "R"),
        new("ModuleQualified", "import sharpy\n", "sharpy.Optional", "sharpy.Result"),
        new("AliasedModule", "import sharpy as sh\n", "sh.Optional", "sh.Result"),
    };

    /// <summary>A case name, the union it belongs to, and a valid single argument for its call form.</summary>
    private sealed record CaseName(string Name, bool OnResult, string Arg);

    private static readonly CaseName[] QualifiedCases =
    {
        new("Some", OnResult: false, Arg: "1"),
        new("None", OnResult: false, Arg: ""),
        new("Ok", OnResult: true, Arg: "1"),
        new("Err", OnResult: true, Arg: "\"x\""),
    };

    private static string Receiver(Spelling s, CaseName c) => c.OnResult ? s.ResultReceiver : s.OptionalReceiver;

    public static IEnumerable<object[]> QualifiedCallData =>
        from s in ReceiverSpellings from c in QualifiedCases select new object[] { s.Name, c.Name };

    public static IEnumerable<object[]> QualifiedValueData => QualifiedCallData;

    /// <summary>Every receiver spelling × case in CALL position is refused SPY0608.</summary>
    [Theory]
    [MemberData(nameof(QualifiedCallData))]
    public void QualifiedCase_CallPosition_IsRefused(string spellingName, string caseName)
    {
        var s = ReceiverSpellings.Single(x => x.Name == spellingName);
        var c = QualifiedCases.Single(x => x.Name == caseName);
        var source = $"{s.Prelude}def main() -> None:\n    print({Receiver(s, c)}.{c.Name}({c.Arg}))\n";

        AssertRefused(source, Spy0608, $"call {spellingName}.{caseName}");
    }

    /// <summary>Every receiver spelling × case in VALUE position is refused SPY0608.</summary>
    [Theory]
    [MemberData(nameof(QualifiedValueData))]
    public void QualifiedCase_ValuePosition_IsRefused(string spellingName, string caseName)
    {
        var s = ReceiverSpellings.Single(x => x.Name == spellingName);
        var c = QualifiedCases.Single(x => x.Name == caseName);
        var source = $"{s.Prelude}def main() -> None:\n    f = {Receiver(s, c)}.{c.Name}\n    print(f)\n";

        AssertRefused(source, Spy0608, $"value {spellingName}.{caseName}");
    }

    // ── Declaration matrix ───────────────────────────────────────────────────────────────────

    /// <summary>A declaration kind rendered with a case name in the type or bare namespace.</summary>
    private static readonly (string Kind, Func<string, string> Render)[] RefusedDeclKinds =
    {
        ("def", n => $"def {n}(x: int) -> int:\n    return x\n\ndef main() -> None:\n    print(1)\n"),
        ("class", n => $"class {n}:\n    x: int = 0\n\ndef main() -> None:\n    print(1)\n"),
        ("struct", n => $"struct {n}:\n    x: int\n\ndef main() -> None:\n    print(1)\n"),
        ("variable", n => $"def main() -> None:\n    {n}: int = 1\n    print({n})\n"),
        ("parameter", n => $"def f({n}: int) -> None:\n    print({n})\n\ndef main() -> None:\n    f(1)\n"),
        ("union-case", n => $"union U:\n    case {n}(v: int)\n    case Other()\n\ndef main() -> None:\n    print(1)\n"),
        ("type-alias", n => $"type {n} = int\n\ndef main() -> None:\n    print(1)\n"),
    };

    /// <summary>Declaration kinds whose access is always qualified — a class member and an enum member
    /// are reached as <c>Box.Ok</c> / <c>E.Ok</c>, never bare, so they cannot make the bare builtin
    /// ambiguous. Allowed (a naming warning only), exactly as any other member spelling.</summary>
    private static readonly (string Kind, Func<string, string> Render)[] ControlDeclKinds =
    {
        ("class-member", n => $"class Box:\n    def {n}(self) -> None:\n        pass\n\ndef main() -> None:\n    print(1)\n"),
        ("enum-member", n => $"enum E:\n    {n} = 1\n\ndef main() -> None:\n    print(1)\n"),
    };

    private static readonly string[] DeclCases = { "Some", "Ok", "Err" };

    public static IEnumerable<object[]> RefusedDeclData =>
        from k in RefusedDeclKinds from n in DeclCases select new object[] { k.Kind, n };

    public static IEnumerable<object[]> ControlDeclData =>
        from k in ControlDeclKinds from n in DeclCases select new object[] { k.Kind, n };

    /// <summary>Every refused declaration kind × case is SPY0212 at the declaration.</summary>
    [Theory]
    [MemberData(nameof(RefusedDeclData))]
    public void ReservedCaseName_Declaration_IsRefused(string kind, string caseName)
    {
        var render = RefusedDeclKinds.Single(k => k.Kind == kind).Render;
        AssertRefused(render(caseName), Spy0212, $"decl {kind} {caseName}");
    }

    /// <summary>A class member and an enum member named for a case are allowed (control): qualified
    /// access never shadows the bare builtin, so no SPY0212 and no SPY0608.</summary>
    [Theory]
    [MemberData(nameof(ControlDeclData))]
    public void ReservedCaseName_QualifiedMember_IsAllowed(string kind, string caseName)
    {
        var render = ControlDeclKinds.Single(k => k.Kind == kind).Render;
        var result = CompileAndExecute(render(caseName));

        result.Success.Should().BeTrue(
            $"[{kind} {caseName}] a qualified member spelling is allowed. Diagnostics: "
            + $"{string.Join(" | ", result.CompilationErrors)}");
        result.RawDiagnostics.Should().NotContain(d => d.Code == Spy0212,
            $"[{kind} {caseName}] a qualified member must not draw SPY0212");
        result.RawDiagnostics.Should().NotContain(d => d.Code == Spy0608,
            $"[{kind} {caseName}] a declaration must not draw SPY0608");
    }

    /// <summary>The positive control: a backtick-escaped case name is the sanctioned escape — it
    /// declares and runs (probe ebt).</summary>
    [Fact]
    public void BacktickEscapedCase_Runs()
    {
        var source = "union M:\n    case `Some`(v: int)\n    case Other()\n\ndef main() -> None:\n    print(\"declared\")\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"a backtick-escaped case name is the sanctioned escape. Diagnostics: "
            + $"{string.Join(" | ", result.CompilationErrors)}");
        result.StandardOutput.Should().Be("declared\n");
        result.RawDiagnostics.Should().NotContain(d => d.Code == Spy0212);
    }

    // ── Totality ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cells_CoverEveryAxis_AnchoredToLiterals()
    {
        ReceiverSpellings.Length.Should().Be(ReceiverSpellingCount);
        QualifiedCases.Length.Should().Be(QualifiedCaseCount);
        RefusedDeclKinds.Length.Should().Be(RefusedDeclKindCount);
        ControlDeclKinds.Length.Should().Be(ControlDeclKindCount);
        DeclCases.Length.Should().Be(DeclCaseCount);

        ReceiverSpellingCount.Should().Be(5);
        QualifiedCaseCount.Should().Be(4);
        PositionCount.Should().Be(2);
        RefusedDeclKindCount.Should().Be(7);
        ControlDeclKindCount.Should().Be(2);
        DeclCaseCount.Should().Be(3);

        ReceiverSpellings.Select(s => s.Name).Should().OnlyHaveUniqueItems();
        QualifiedCases.Select(c => c.Name).Should().OnlyHaveUniqueItems();
        RefusedDeclKinds.Select(k => k.Kind).Should().OnlyHaveUniqueItems();
        ControlDeclKinds.Select(k => k.Kind).Should().OnlyHaveUniqueItems();
        DeclCases.Should().OnlyHaveUniqueItems();

        // Both unions are exercised: two cases on Optional, two on Result.
        QualifiedCases.Count(c => c.OnResult).Should().Be(2);
        QualifiedCases.Count(c => !c.OnResult).Should().Be(2);

        // The four builtin case names, all reserved.
        QualifiedCases.Select(c => c.Name).Should()
            .BeEquivalentTo(new[] { "Some", "None", "Ok", "Err" });
    }

    private void AssertRefused(string source, string expectedCode, string label)
    {
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{label}] must be refused\n{source}");
        result.RawDiagnostics.Should().Contain(d => d.Code == expectedCode,
            $"[{label}] must report {expectedCode}. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] a typed refusal must never be SPY0908\n{source}");
    }
}
