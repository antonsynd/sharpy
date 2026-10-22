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
    // An unsuffixed literal above int32 is int64, and an int64 constant narrows only into uint64
    // (integer_literals.md), so the widest unsuffixed constant a uint32 slot admits is int32's max.
    [InlineData("UInt32_SlotNarrowed_Int32Max", "x: uint32 = 2147483647\n    print(x)", "2147483647")]
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
    // decimal
    [InlineData("Decimal_Suffixed_Max", "print(79228162514264337593543950335m)", "79228162514264337593543950335")]
    [InlineData("Decimal_Suffixed_NegativeMax", "print(-79228162514264337593543950335m)", "-79228162514264337593543950335")]
    [InlineData("Decimal_Suffixed_Small", "print(0.5m)", "0.5")]
    [InlineData("Decimal_SlotNarrowed_Exponent", "d: decimal = 1.5e2\n    print(d)", "150")]
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
    // Axis change: case of the exponent and suffix letters.
    [InlineData("Case_Float64_UpperExponent", "print(1E400)", Refused0210, "'double'")]
    [InlineData("Case_Float32_UpperSuffix", "print(1e39F)", Refused0210, "'float'")]
    [InlineData("Case_Decimal_UpperSuffix", "print(1e29M)", Refused0210, "'decimal'")]
    public void RefusedCell_CarriesTheCodeAndNoIce(string cell, string body, string code, string typeNamed)
    {
        AssertRefused(cell, InMain(body), code, typeNamed);
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
    public void FloatSeam_AgreesWithRoslynsLexer(string digits, string suffix)
    {
        var clrType = suffix switch { "f" => typeof(float), "m" => typeof(decimal), _ => typeof(double) };
        var seamAdmits = FloatLiteralClassifier.Fits(digits, clrType);

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
