using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The "a const is not an instance field" roster (#1794), over the four readers that answer that
/// question and the hosts each one governs.
///
/// <para><b>Contract.</b> Class-level storage — a <c>const</c> or a <c>@static</c> field — is not
/// per-instance state. It is not a synthesized constructor parameter, not a constructor assignment,
/// not a dataclass field, not an auto-property, and it takes no part in the "a non-default field
/// cannot follow a defaulted one" ordering rule. One predicate answers this on the semantic side
/// (<c>MemberClassification.IsInstanceField</c>) and one on the emitter side, reading the
/// materialized facts (<c>IsSynthesizedConstructorField</c>).</para>
///
/// <para><b>Why a matrix and not the repro.</b> Four readers disagreed and only one of them knew
/// about <c>const</c>: the struct validator's ordering rule, the struct validator's
/// "constructor must initialize all fields" roster, the dataclass field vector, and the emitter's
/// property-vs-field arm. Each row below is the host one of them governs, so a fix that teaches
/// only one reader leaves a red row.</para>
///
/// <para><see cref="ParameterDefaultConstantMatrixTests"/> owns the const FACT (does it emit as
/// <c>const</c> / <c>static readonly</c>, and does every consumer read it); this file owns the
/// ROSTER question (is it an instance field at all).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ConstInstanceFieldRosterTests : IntegrationTestBase
{
    public ConstInstanceFieldRosterTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    // ── the ordering rule: a const may precede a field with no default ──────────────────────
    [InlineData("StructConstBeforeField_SynthesizedCtor",
        "struct S:\n    const K: int = 7\n    x: int\n\ndef main():\n    s = S(5)\n    print(s.x)\n    print(S.K)\n",
        "5\n7\n")]
    [InlineData("StructConstBeforeField_ExplicitInit",
        "struct S:\n    const K: int = 7\n    x: int\n\n    def __init__(self, x: int):\n        self.x = x\n\ndef main():\n    s = S(5)\n    print(s.x)\n    print(S.K)\n",
        "5\n7\n")]
    [InlineData("StructConstBeforeTwoFields",
        "struct P:\n    const D: int = 2\n    a: float\n    b: float\n\ndef main():\n    p = P(1.5, 2.5)\n    print(p.a)\n    print(p.b)\n    print(P.D)\n",
        "1.5\n2.5\n2\n")]
    [InlineData("StructConstAfterField",
        "struct S:\n    x: int\n    const K: int = 7\n\ndef main():\n    s = S(5)\n    print(s.x)\n    print(S.K)\n",
        "5\n7\n")]
    [InlineData("StructAllConst_NoCtor",
        "struct S:\n    const A: int = 1\n    const B: int = 2\n\ndef main():\n    print(S.A)\n    print(S.B)\n",
        "1\n2\n")]
    // ── the dataclass field vector ──────────────────────────────────────────────────────────
    [InlineData("DataclassConstBeforeFields",
        "@dataclass\nclass P:\n    const SCALE: int = 3\n    x: int\n    y: int\n\ndef main():\n    p = P(1, 2)\n    print(p.x)\n    print(p.y)\n    print(P.SCALE)\n",
        "1\n2\n3\n")]
    [InlineData("DataclassConstAfterFields",
        "@dataclass\nclass P:\n    x: int\n    y: int\n    const SCALE: int = 3\n\ndef main():\n    p = P(1, 2)\n    print(p.x)\n    print(P.SCALE)\n",
        "1\n3\n")]
    // ── @static keeps its own answer (the arm that already worked — the positive control) ────
    [InlineData("StructStaticBeforeField",
        "struct S:\n    @static\n    count: int = 0\n    x: int\n\ndef main():\n    s = S(5)\n    print(s.x)\n    print(S.count)\n",
        "5\n0\n")]
    // ── the class host, which was never broken: the control for the whole roster ─────────────
    [InlineData("ClassConstBeforeField",
        "class C:\n    const K: int = 7\n    x: int = 1\n\ndef main():\n    c = C()\n    print(c.x)\n    print(C.K)\n",
        "1\n7\n")]
    // ── interface const, read through the interface ──────────────────────────────────────────
    [InlineData("InterfaceConstViaInterface",
        "interface I:\n    const K: int = 1\n\nclass App(I):\n    def go(self) -> int:\n        return I.K\n\ndef main():\n    print(I.K)\n    a = App()\n    print(a.go())\n",
        "1\n1\n")]
    public void ConstHost_IsNotAnInstanceField(string label, string source, string expected)
    {
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] must never produce SPY0908 — a const on the instance roster reaches Roslyn "
            + $"as CS0176/CS0120/CS1736. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Validation.StructFieldOrdering,
            $"[{label}] a const carries an initializer but is not a DEFAULTED INSTANCE FIELD, so it "
            + $"cannot make a later field 'follow a field with a default value'\n{source}");
        result.Success.Should().BeTrue(
            $"[{label}] must compile and run. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(expected,
            $"[{label}] prints the constructed instance AND the const, so a roster that drops the "
            + $"instance field or keeps the const fails here\n{source}");
    }

    /// <summary>
    /// The one refusal in this class: C# binds <c>App.K</c> on <c>App</c> alone, so an interface
    /// constant read through an implementor is not a program. It used to leave the checker untyped
    /// and come back as CS0117 behind SPY0908 — a compiler-bug report for a program that names the
    /// wrong type.
    /// </summary>
    [Fact]
    public void InterfaceConstViaImplementor_IsRefusedByNameWithTheInterfaceSteer()
    {
        const string source =
            "interface I:\n    const K: int = 1\n\nclass App(I):\n    pass\n\ndef main():\n    print(App.K)\n";

        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"the refusal is semantic, not an ICE. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeFalse(source);

        var diagnostic = result.RawDiagnostics
            .FirstOrDefault(d => d.Code == DiagnosticCodes.Semantic.UndefinedMember);
        diagnostic.Should().NotBeNull(
            "must report SPY0203. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
        diagnostic!.Message.Should().Contain("declared on interface 'I'", source);
        diagnostic.Message.Should().Contain("'I.K'",
            "the steer names the spelling that works\n" + source);
    }

    /// <summary>
    /// A base-CLASS static IS inherited into the derived type's static surface in C#, so the
    /// refusal above must not generalize to it. Without this control the interface arm could be
    /// written as "any static on any base type" and pass its own test while rejecting real
    /// programs — the exemption would never be measured.
    /// </summary>
    [Fact]
    public void BaseClassStatic_ReadThroughTheDerivedType_StillCompiles()
    {
        const string source =
            "class Base:\n    @static\n    count: int = 7\n\nclass Derived(Base):\n    pass\n\n"
            + "def main():\n    print(Derived.count)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            "C# inherits base-class statics, so `Derived.count` is a real program. Diagnostics: "
            + $"{string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("7\n", source);
    }
}
