using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The numeric-literal range matrix (#2340, rulings R-HU / R-HV).
///
/// <para><b>Contract.</b> A numeric literal outside the range of its OWN resolved type is refused
/// at one semantic seam with the SPY0210 family — never by Roslyn (CS0594 → SPY0599) and never by
/// the emitter (SPY0909). The integer half of the seam is <see cref="IntegerLiteralClassifier"/>;
/// the float half is <see cref="FloatLiteralClassifier"/>, whose <c>Fits</c> is also the range
/// question the float32/decimal slot-narrowing predicates ask, so the suffixed and the
/// slot-narrowed spelling of one value cannot disagree. Before #2340 only the integer arm and
/// the narrowing arm existed: <c>1e400</c> and <c>1e39f</c> were SPY0599, <c>1e29m</c> SPY0909.</para>
///
/// <para><b>Axes (closed).</b> TYPE {int32→int64 promotion, int64, uint32, uint64, float64,
/// float32, decimal} × SPELLING {unsuffixed, suffixed, slot-narrowed} × MAGNITUDE {max, max + one
/// unit/ulp, far, negative}. A negative UNSIGNED literal is not a range cell: <c>-1U</c> is the
/// unary operator over a <c>uint32</c> operand, typed <c>int64</c> as C# types it, and
/// <c>-1UL</c> is SPY0223 as <c>-u</c> is for a <c>uint64</c> variable. Two axis changes ride
/// along: POSITION (pattern, parameter default, const, field, lambda body, f-string) and CASE
/// (<c>1E400</c>, <c>F</c>, <c>M</c>). Spellings avoid the forms #2339 re-reads in the lexer.</para>
///
/// <para><b>Contract widened (#2344, R-IB).</b> A numeric literal's TYPE, VALUE and admissibility are
/// decided once, from its resolved type and value, at the one seam every consumer reads. Two more
/// axes: CONSUMER {literal expression, narrowed slot, lambda-parameter pre-inference, binary operand}
/// — a second literal→type table in lambda pre-inference had <c>f</c> and no <c>m</c>, so
/// <c>lambda x: x * 1.5m</c> typed <c>x</c> as float64 — and MAGNITUDE gains {below the smallest
/// non-zero value}: a non-zero <c>decimal</c> literal that becomes zero is refused on both spellings
/// (R-IA; <c>1e-30m</c> printed <c>0</c> while <c>d: decimal = 1e-30</c> was SPY0220), and float32 /
/// float64 keep IEEE zero. An unsuffixed integer literal converts by VALUE to an unsigned type at
/// every site (R-IC): {annotated slot, operator operand, call argument, return, <c>const</c>,
/// augmented, conditional arm}, while a suffixed or declared <c>int64</c> constant still converts to
/// <c>uint64</c> only (C# §10.2.11).</para>
///
/// <para><b>Every cell executes.</b> An accepted cell prints its value; a refused cell asserts
/// the code, that the message names the type, and that no ICE code rides along.
/// <see cref="FloatSeam_AgreesWithRoslynsLexer"/> is the differential half: the seam's answer
/// for each float text is Roslyn's own CS0594 verdict on the text the emitter writes.</para>
/// </summary>
public class NumericLiteralRangeMatrixTests : IntegrationTestBase
{
    public NumericLiteralRangeMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string Refused0210 = "SPY0210";

    private static readonly string[] IceCodes = { "SPY0599", "SPY0908", "SPY0909" };

    // ── Accepted cells: the program runs and prints the literal's value ─────────────────────

    [Theory]
    // int32 → int64 promotion (unsuffixed)
    [InlineData("Promoted_Unsuffixed_Max", "print(9223372036854775807)", "9223372036854775807")]
    [InlineData("Promoted_Unsuffixed_Int32MaxPlusOne", "print(2147483648)", "2147483648")]
    [InlineData("Promoted_Unsuffixed_NegativeMin", "print(-9223372036854775808)", "-9223372036854775808")]
    [InlineData("Promoted_SlotNarrowed_Int64", "x: int64 = 2147483648\n    print(x)", "2147483648")]
    [InlineData("Int32_SlotNarrowed_Max", "x: int32 = 2147483647\n    print(x)", "2147483647")]
    // int64
    [InlineData("Int64_Suffixed_Max", "print(9223372036854775807L)", "9223372036854775807")]
    [InlineData("Int64_Suffixed_NegativeMin", "print(-9223372036854775808L)", "-9223372036854775808")]
    // uint32
    [InlineData("UInt32_Suffixed_Max", "print(4294967295U)", "4294967295")]
    // An unsuffixed literal converts to an unsigned slot by its VALUE (R-IC, #2344): int32's max as
    // an int32 constant, and every larger literal up to uint32's max as the int64 it is typed.
    [InlineData("UInt32_SlotNarrowed_Int32Max", "x: uint32 = 2147483647\n    print(x)", "2147483647")]
    [InlineData("UInt32_SlotNarrowed_Int32MaxPlusOne", "v: uint32 = 2147483648\n    print(v)", "2147483648")]
    [InlineData("UInt32_SlotNarrowed_Max", "x: uint32 = 4294967295\n    print(x)", "4294967295")]
    [InlineData("UInt32_SlotNarrowed_HexMax", "m: uint32 = 0xFFFFFFFF\n    print(m)", "4294967295")]
    [InlineData("UInt32_SlotNarrowed_Parenthesized", "x: uint32 = (4294967295)\n    print(x)", "4294967295")]
    [InlineData("UInt32_Operand_Max", "b: uint32 = 0\n    c: uint32 = b + 4294967295\n    print(c)", "4294967295")]
    [InlineData("UInt32_Operand_Left", "b: uint32 = 1\n    c: uint32 = 4294967294 + b\n    print(c)", "4294967295")]
    [InlineData("UInt32_Augmented_Max", "b: uint32 = 0\n    b += 4294967295\n    print(b)", "4294967295")]
    [InlineData("UInt32_ConditionalArm_Max", "flag = len(\"a\") == 1\n    x: uint32 = 4294967295 if flag else 0\n    print(x)", "4294967295")]
    [InlineData("UInt32_Suffixed_Negative_IsInt64", "y = -1U\n    z: int64 = y\n    print(z)", "-1")]
    [InlineData("UInt32_Suffixed_NegativeMax_IsInt64", "print(-4294967295U)", "-4294967295")]
    // uint64
    [InlineData("UInt64_Suffixed_Max", "print(18446744073709551615UL)", "18446744073709551615")]
    [InlineData("UInt64_SlotNarrowed_Int64Max", "x: uint64 = 9223372036854775807\n    print(x)", "9223372036854775807")]
    // float64
    [InlineData("Float64_Unsuffixed_Max", "print(1.7976931348623157e308)", "1.7976931348623157e+308")]
    [InlineData("Float64_Unsuffixed_NegativeMax", "print(-1.7976931348623157e308)", "-1.7976931348623157e+308")]
    [InlineData("Float64_Suffixed_Max", "print(1.7976931348623157e308d)", "1.7976931348623157e+308")]
    [InlineData("Float64_SlotNarrowed_Max", "x: float = 1.7976931348623157e308\n    print(x)", "1.7976931348623157e+308")]
    [InlineData("Float64_Unsuffixed_Underflow_IsZero", "print(1e-400)", "0.0")]
    [InlineData("Float64_Suffixed_Underflow_IsZero", "print(1e-400d)", "0.0")]
    // float32
    [InlineData("Float32_Suffixed_Max", "print(3.4028235e38f)", "3.4028235e+38")]
    [InlineData("Float32_Suffixed_NegativeMax", "print(-3.4028235e38f)", "-3.4028235e+38")]
    [InlineData("Float32_SlotNarrowed_Max", "x: float32 = 3.4028235e38\n    print(x)", "3.4028235e+38")]
    // Below the float32 rounding midpoint 2^128 - 2^103 by one: a correctly-rounded float parse is
    // float.MaxValue (Roslyn's verdict on the emitted `...447.0f`), but routed through double it
    // first rounds UP to the midpoint and then ties to infinity — the narrowing predicate's old
    // double-routed question refused a literal the suffixed spelling of the same digits ran.
    [InlineData("Float32_SlotNarrowed_BelowMidpoint",
        "x: float32 = 340282356779733661637539395458142568447.0\n    print(x)", "3.4028235e+38")]
    [InlineData("Float32_Suffixed_BelowMidpoint",
        "print(340282356779733661637539395458142568447.0f)", "3.4028235e+38")]
    [InlineData("Float32_Suffixed_Small", "print(0.5f)", "0.5")]
    // Below float32's smallest subnormal: IEEE zero on both spellings, as float64 (R-IA keeps it).
    [InlineData("Float32_Suffixed_Underflow_IsZero", "print(1e-50f)", "0.0")]
    [InlineData("Float32_SlotNarrowed_Underflow_IsZero", "x: float32 = 1e-50\n    print(x)", "0.0")]
    // decimal
    [InlineData("Decimal_Suffixed_Max", "print(79228162514264337593543950335m)", "79228162514264337593543950335")]
    [InlineData("Decimal_Suffixed_NegativeMax", "print(-79228162514264337593543950335m)", "-79228162514264337593543950335")]
    [InlineData("Decimal_Suffixed_Small", "print(0.5m)", "0.5")]
    [InlineData("Decimal_SlotNarrowed_Exponent", "d: decimal = 1.5e2\n    print(d)", "150")]
    // decimal's smallest non-zero magnitude, and a value that rounds to it rather than to zero.
    [InlineData("Decimal_Suffixed_SmallestNonZero", "print(1e-28m)", "0.0000000000000000000000000001")]
    [InlineData("Decimal_SlotNarrowed_SmallestNonZero", "d: decimal = 1e-28\n    print(d)", "0.0000000000000000000000000001")]
    [InlineData("Decimal_Suffixed_RoundsToSmallest", "print(1.5e-28m)", "0.0000000000000000000000000002")]
    // A ZERO text is not "becomes zero": it runs. Its scale clamps to 28 and prints as C# prints it.
    [InlineData("Decimal_Suffixed_ZeroExponent_IsZero", "print(0e-30m)", "0.0000000000000000000000000000")]
    // Consumer: lambda-parameter pre-inference reads the literal's own type (#2344 cell 2).
    [InlineData("Decimal_LambdaPreInference", "f = lambda x: x * 1.5m\n    print(f(2m))", "3.0")]
    [InlineData("Float32_LambdaPreInference", "f = lambda x: x + 1.5f\n    print(f(1.0f))", "2.5")]
    [InlineData("UInt32_LambdaPreInference", "f = lambda x: x * 2u\n    print(f(3u))", "6")]
    [InlineData("Float64_LambdaPreInference", "f = lambda x: x * 1.5\n    print(f(2.0))", "3.0")]
    public void AcceptedCell_RunsAndPrintsTheValue(string cell, string body, string expected)
    {
        var source = InMain(body);
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"[{cell}] must compile and run. Diagnostics: {Describe(result)}\n{source}");
        result.StandardOutput.TrimEnd('\n', '\r').Should().Be(expected, $"[{cell}]\n{source}");
    }

    // ── Refused cells: the code, the named type, no ICE ──────────────────────────────────────

    [Theory]
    // int32 → int64 promotion (unsuffixed)
    [InlineData("Promoted_Unsuffixed_MaxPlusOne", "print(9223372036854775808)", Refused0210, "'long'")]
    [InlineData("Promoted_Unsuffixed_Far", "print(99999999999999999999)", Refused0210, "any integer type")]
    [InlineData("Promoted_Unsuffixed_NegativePastMin", "print(-9223372036854775809)", Refused0210, "any integer type")]
    [InlineData("Int32_SlotNarrowed_MaxPlusOne", "x: int32 = 2147483648", "SPY0220", "'int32'")]
    // int64
    [InlineData("Int64_Suffixed_MaxPlusOne", "print(9223372036854775808L)", Refused0210, "'long'")]
    [InlineData("Int64_Suffixed_Far", "print(99999999999999999999L)", Refused0210, "'long'")]
    [InlineData("Int64_Suffixed_NegativePastMin", "print(-9223372036854775809L)", Refused0210, "'long'")]
    [InlineData("Int64_SlotNarrowed_MaxPlusOne", "x: int64 = 9223372036854775808", Refused0210, "'long'")]
    // uint32
    [InlineData("UInt32_Suffixed_MaxPlusOne", "print(4294967296U)", Refused0210, "'uint'")]
    [InlineData("UInt32_Suffixed_Far", "print(99999999999999999999U)", Refused0210, "'uint'")]
    [InlineData("UInt32_Suffixed_NegativeFar", "print(-4294967296U)", Refused0210, "'uint'")]
    [InlineData("UInt32_Suffixed_Negative_IntoUInt32Slot", "x: uint32 = -1U", "SPY0220", "'int64'")]
    [InlineData("UInt32_SlotNarrowed_MaxPlusOne", "x: uint32 = 4294967296", "SPY0220", "'uint32'")]
    // A literal that does not fit keeps its own type (R-IC): the operator sees uint32 ⊗ int64 → int64.
    [InlineData("UInt32_Operand_MaxPlusOne", "b: uint32 = 0\n    c: uint32 = b + 4294967296", "SPY0220", "'int64'")]
    // Only an UNSUFFIXED literal converts by value; an int64 constant EXPRESSION is C#'s long constant
    // and converts to uint64 only (§10.2.11) — `uint x = 4294967296L - 1;` is CS0266.
    [InlineData("UInt32_SlotNarrowed_Int64Expression", "x: uint32 = 4294967296 - 1", "SPY0220", "'int64'")]
    [InlineData("UInt32_SlotNarrowed_NegativeInt64", "x: uint32 = -2147483649", "SPY0220", "'int64'")]
    // uint64
    [InlineData("UInt64_Suffixed_MaxPlusOne", "print(18446744073709551616UL)", Refused0210, "'ulong'")]
    [InlineData("UInt64_Suffixed_Far", "print(99999999999999999999UL)", Refused0210, "'ulong'")]
    [InlineData("UInt64_Suffixed_Negative", "print(-1UL)", "SPY0223", "'uint64'")]
    [InlineData("UInt64_SlotNarrowed_MaxPlusOne", "x: uint64 = 18446744073709551616", Refused0210, "any integer type")]
    // float64
    [InlineData("Float64_Unsuffixed_MaxPlusOneUlp", "print(1.7976931348623159e308)", Refused0210, "'double'")]
    [InlineData("Float64_Unsuffixed_Far", "print(1e400)", Refused0210, "'double'")]
    [InlineData("Float64_Unsuffixed_Negative", "print(-1e400)", Refused0210, "'double'")]
    [InlineData("Float64_Suffixed_Far", "print(1e400d)", Refused0210, "'double'")]
    [InlineData("Float64_SlotNarrowed_Far", "x: float = 1e400", Refused0210, "'double'")]
    // float32
    [InlineData("Float32_Suffixed_MaxPlusOneUlp", "print(3.4028236e38f)", Refused0210, "'float'")]
    [InlineData("Float32_Suffixed_Far", "print(1e39f)", Refused0210, "'float'")]
    [InlineData("Float32_Suffixed_Negative", "print(-1e39f)", Refused0210, "'float'")]
    [InlineData("Float32_SlotNarrowed_MaxPlusOneUlp", "x: float32 = 3.4028236e38", "SPY0220", "'float32'")]
    [InlineData("Float32_SlotNarrowed_Far", "x: float32 = 1e40", "SPY0220", "'float32'")]
    [InlineData("Float32_SlotNarrowed_PastFloat64", "x: float32 = 1e400", Refused0210, "'double'")]
    // decimal
    [InlineData("Decimal_Suffixed_MaxPlusOne", "print(79228162514264337593543950336m)", Refused0210, "'decimal'")]
    [InlineData("Decimal_Suffixed_Far", "print(1e29m)", Refused0210, "'decimal'")]
    [InlineData("Decimal_Suffixed_Negative", "print(-1e29m)", Refused0210, "'decimal'")]
    [InlineData("Decimal_SlotNarrowed_MaxPlusOne", "d: decimal = 79228162514264337593543950336.0", "SPY0220", "'decimal'")]
    [InlineData("Decimal_SlotNarrowed_Far", "d: decimal = 1e29", "SPY0220", "'decimal'")]
    [InlineData("Decimal_SlotNarrowed_PastFloat64", "d: decimal = 1e400", Refused0210, "'double'")]
    // Below decimal's smallest non-zero value (R-IA, #2344): a non-zero literal that becomes zero is
    // refused on every spelling and consumer. The suffixed spelling printed `0.0000…` before.
    [InlineData("Decimal_Suffixed_BelowSmallest", "print(1e-30m)", Refused0210, "too small for 'decimal'")]
    [InlineData("Decimal_Suffixed_BetweenZeroAndSmallest", "print(1.5e-29m)", Refused0210, "too small for 'decimal'")]
    [InlineData("Decimal_Suffixed_NegativeBelowSmallest", "print(-1e-30m)", Refused0210, "too small for 'decimal'")]
    [InlineData("Decimal_Suffixed_BelowFloat64Smallest", "print(1e-400m)", Refused0210, "too small for 'decimal'")]
    [InlineData("Decimal_SlotNarrowed_BelowSmallest", "d: decimal = 1e-30", "SPY0220", "'decimal'")]
    [InlineData("Decimal_SlotNarrowed_BelowFloat64Smallest", "d: decimal = 1e-400", "SPY0220", "'decimal'")]
    [InlineData("Decimal_LambdaPreInference_BelowSmallest", "f = lambda x: x * 1e-30m\n    print(f(2m))", Refused0210, "too small for 'decimal'")]
    [InlineData("Decimal_Operand_BelowSmallest", "d: decimal = 1m\n    print(d + 1e-30m)", Refused0210, "too small for 'decimal'")]
    // Axis change: case of the exponent and suffix letters.
    [InlineData("Case_Float64_UpperExponent", "print(1E400)", Refused0210, "'double'")]
    [InlineData("Case_Float32_UpperSuffix", "print(1e39F)", Refused0210, "'float'")]
    [InlineData("Case_Decimal_UpperSuffix", "print(1e29M)", Refused0210, "'decimal'")]
    [InlineData("Case_Decimal_UpperSuffix_BelowSmallest", "print(1E-30M)", Refused0210, "too small for 'decimal'")]
    public void RefusedCell_CarriesTheCodeAndNoIce(string cell, string body, string code, string typeNamed)
    {
        AssertRefused(cell, InMain(body), code, typeNamed);
    }

    // ── Sites that need a whole program: an unsuffixed literal converts by value (R-IC) ──────

    [Theory]
    [InlineData("UInt32_CallArgument",
        "def f(x: uint32) -> uint32:\n    return x\n\ndef main() -> None:\n    print(f(4294967295))\n",
        "4294967295")]
    [InlineData("UInt32_Return",
        "def g() -> uint32:\n    return 4294967295\n\ndef main() -> None:\n    print(g())\n",
        "4294967295")]
    [InlineData("UInt32_Const",
        "const C: uint32 = 4294967295\n\ndef main() -> None:\n    print(C)\n",
        "4294967295")]
    [InlineData("UInt32_ParameterDefault",
        "def f(x: uint32 = 0xFFFFFFFF) -> None:\n    print(x)\n\ndef main() -> None:\n    f()\n",
        "4294967295")]
    public void AcceptedProgram_RunsAndPrintsTheValue(string cell, string source, string expected)
    {
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"[{cell}] must compile and run. Diagnostics: {Describe(result)}\n{source}");
        result.StandardOutput.TrimEnd('\n', '\r').Should().Be(expected, $"[{cell}]\n{source}");
    }

    [Theory]
    // A declared int64 constant is C#'s `const long`: it converts to uint64 only (§10.2.11, #1355).
    [InlineData("UInt32_ConstInt64Reference",
        "const L: int64 = 4294967295\n\ndef main() -> None:\n    x: uint32 = L\n    print(x)\n",
        "SPY0220", "'int64'")]
    public void RefusedProgram_CarriesTheCodeAndNoIce(string cell, string source, string code, string typeNamed)
    {
        AssertRefused(cell, source, code, typeNamed);
    }

    // ── Axis change: the literal's POSITION — one seam, so every position refuses ────────────

    [Theory]
    [InlineData("Position_Pattern",
        "def f(x: float) -> None:\n    match x:\n        case 1e400:\n            print(\"a\")\n        case _:\n            print(\"b\")\n\ndef main() -> None:\n    f(1.0)\n",
        "'double'")]
    [InlineData("Position_ParameterDefault",
        "def f(x: float = 1e400) -> None:\n    print(x)\n\ndef main() -> None:\n    f()\n",
        "'double'")]
    [InlineData("Position_Const",
        "const C: float = 1e400\n\ndef main() -> None:\n    print(C)\n",
        "'double'")]
    [InlineData("Position_Field",
        "class K:\n    v: decimal = 1e29m\n\ndef main() -> None:\n    print(K().v)\n",
        "'decimal'")]
    [InlineData("Position_LambdaBody",
        "def main() -> None:\n    g = lambda x: x * 1e39f\n    print(g(1.0f))\n",
        "'float'")]
    [InlineData("Position_FString",
        "def main() -> None:\n    print(f\"{1e400}\")\n",
        "'double'")]
    public void RefusedPosition_CarriesSpy0210AndNoIce(string cell, string source, string typeNamed)
    {
        AssertRefused(cell, source, Refused0210, typeNamed);
    }

    // ── Differential: the seam's answer is Roslyn's own verdict on the emitted text ──────────

    [Theory]
    [InlineData("1.7976931348623157e308", "d")]
    [InlineData("1.7976931348623159e308", "d")]
    [InlineData("1e400", "d")]
    [InlineData("1e-400", "d")]
    [InlineData("3.4028235e38", "f")]
    [InlineData("3.4028236e38", "f")]
    [InlineData("340282356779733661637539395458142568447.0", "f")]
    [InlineData("340282356779733661637539395458142568448.0", "f")]
    [InlineData("1e39", "f")]
    [InlineData("79228162514264337593543950335", "m")]
    [InlineData("79228162514264337593543950336", "m")]
    [InlineData("1e29", "m")]
    [InlineData("1.5e2", "m")]
    [InlineData("1e-50", "f")]
    [InlineData("1e-28", "m")]
    [InlineData("1e-30", "m")]
    public void FloatSeam_AgreesWithRoslynsLexer(string digits, string suffix)
    {
        // The RANGE half only: a non-zero decimal that becomes zero is refused by ruling (R-IA,
        // #2344) where Roslyn admits it as 0m, so TooSmall is not part of this differential.
        var clrType = suffix switch { "f" => typeof(float), "m" => typeof(decimal), _ => typeof(double) };
        var seamAdmits = FloatLiteralClassifier.Check(digits, clrType) != FloatLiteralClassifier.Verdict.TooLarge;

        var tree = CSharpSyntaxTree.ParseText($"class C {{ object V = {digits}{suffix}; }}");
        var roslynRefuses = tree.GetDiagnostics().Any(d => d.Id == "CS0594");

        seamAdmits.Should().Be(!roslynRefuses,
            $"'{digits}{suffix}': the seam must admit exactly the texts Roslyn's lexer admits "
            + $"(Roslyn: {string.Join(", ", tree.GetDiagnostics().Select(d => d.Id))})");
    }

    private void AssertRefused(string cell, string source, string code, string typeNamed)
    {
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{cell}] must be refused; it printed '{result.StandardOutput}'\n{source}");

        var matching = result.RawDiagnostics.Where(d => d.Code == code).ToList();
        matching.Should().HaveCount(1, $"[{cell}] must report {code} exactly once. Got: {Describe(result)}\n{source}");
        matching[0].Message.Should().Contain(typeNamed, $"[{cell}] the refusal names the type\n{source}");

        result.RawDiagnostics.Should().NotContain(d => IceCodes.Contains(d.Code),
            $"[{cell}] the semantic seam refuses first — no ICE rides along. Got: {Describe(result)}");
        result.CompilationErrors.Should().NotContain(e => IceCodes.Any(e.Contains),
            $"[{cell}] no ICE in the error text either\n{source}");
    }

    private static string InMain(string body) => $"def main() -> None:\n    {body}\n";

    private static string Describe(ExecutionResult result)
        => string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}@{d.Line}: {d.Message}"))
           + (result.CompilationErrors.Count > 0 ? " || " + string.Join(" | ", result.CompilationErrors) : "");
}
