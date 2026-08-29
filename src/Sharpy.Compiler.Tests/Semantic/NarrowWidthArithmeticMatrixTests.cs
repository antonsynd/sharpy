using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Width x operator x store matrix: narrow integer arithmetic promotes to int32;
/// plain narrow stores are refused (SPY0220), augmented assignments narrow when
/// both operands are the same narrow type, and normal int operations are unaffected.
/// Guards the int32 promotion floor (a6a7ddc7f) and NarrowTo augmented assignment
/// (561ba1efb). #1666.
/// </summary>
[Collection("HeavyCompilation")]
public class NarrowWidthArithmeticMatrixTests : IntegrationTestBase
{
    public NarrowWidthArithmeticMatrixTests(ITestOutputHelper output) : base(output) { }

    // ── Cell group (a): plain narrow stores — result promoted to int32, SPY0220 ──

    public static IEnumerable<object[]> PlainNarrowStoreCells()
    {
        // (narrowType, lhsInit, rhsInit, operator, description)
        yield return new object[] { "int8", "5", "3", "+", "int8 add" };
        yield return new object[] { "int8", "5", "3", "-", "int8 sub" };
        yield return new object[] { "int8", "5", "3", "*", "int8 mul" };
        yield return new object[] { "int8", "7", "2", "//", "int8 floordiv" };
        yield return new object[] { "int8", "7", "2", "%", "int8 mod" };
        yield return new object[] { "uint8", "5", "3", "+", "uint8 add" };
        yield return new object[] { "int16", "5", "3", "+", "int16 add" };
        yield return new object[] { "uint16", "5", "3", "+", "uint16 add" };
    }

    [Theory]
    [MemberData(nameof(PlainNarrowStoreCells))]
    public void PlainNarrowStore_Refused_SPY0220(string narrowType, string lhs, string rhs, string op, string desc)
    {
        var source = $@"
def main() -> None:
    a: {narrowType} = {lhs}
    b: {narrowType} = {rhs}
    c: {narrowType} = a {op} b
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"plain narrow store ({desc}) should be refused");
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            $"plain narrow store ({desc}) should produce SPY0220");
    }

    // ── Cell group (b): unary narrow stores — also promoted ──

    [Theory]
    [InlineData("-", "negation")]
    [InlineData("~", "bitwise complement")]
    public void UnaryNarrowStore_Refused_SPY0220(string op, string desc)
    {
        var source = $@"
def main() -> None:
    a: int8 = 5
    n: int8 = {op}a
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"unary narrow store ({desc}) should be refused");
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            $"unary narrow store ({desc}) should produce SPY0220");
    }

    // ── Cell group (c): power narrow store — also promoted ──

    [Fact]
    public void PowerNarrowStore_Refused_SPY0220()
    {
        const string source = @"
def main() -> None:
    a: int8 = 2
    b: int8 = 3
    n: int8 = a ** b
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse("power narrow store should be refused");
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            "power narrow store should produce SPY0220");
    }

    // ── Cell group (d): augmented with narrow RHS — NarrowTo allows these ──

    public static IEnumerable<object[]> AugmentedNarrowRhsCells()
    {
        // (narrowType, initVal, rhsInit, augOp, expectedOutput, description)
        yield return new object[] { "int8", "5", "3", "+=", "8", "int8 +=" };
        yield return new object[] { "int8", "5", "3", "-=", "2", "int8 -=" };
        yield return new object[] { "int8", "5", "3", "*=", "15", "int8 *=" };
        yield return new object[] { "int8", "-7", "2", "//=", "-4", "int8 //=" };
        yield return new object[] { "uint8", "200", "50", "+=", "250", "uint8 +=" };
    }

    [Theory]
    [MemberData(nameof(AugmentedNarrowRhsCells))]
    public void AugmentedNarrowRhs_Accepted(string narrowType, string initVal, string rhsInit, string augOp, string expected, string desc)
    {
        var source = $@"
def main() -> None:
    x: {narrowType} = {initVal}
    y: {narrowType} = {rhsInit}
    x {augOp} y
    print(x)
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"augmented narrow ({desc}) should accept: "
            + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be(expected, $"augmented narrow ({desc}) output");
    }

    // ── Cell group (e): augmented with int (wider) RHS — SPY0220 stays ──

    [Fact]
    public void AugmentedWithIntRhs_Refused_SPY0220()
    {
        const string source = @"
def main() -> None:
    x: int8 = 5
    i: int = 3
    x += i
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse("augmented with wider int RHS should be refused");
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            "augmented with wider int RHS should produce SPY0220");
    }

    // ── Cell group (f): normal int operations — unchanged (positive control) ──

    [Fact]
    public void NormalIntArithmetic_PositiveControl()
    {
        const string source = @"
def main() -> None:
    a: int = 5
    b: int = 3
    c: int = a + b
    print(c)
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue("normal int arithmetic should accept: "
            + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("8");
    }

    [Fact]
    public void NormalIntPrint_PositiveControl()
    {
        const string source = @"
def main() -> None:
    a: int = 5
    b: int = 3
    print(a + b)
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue("print(a + b) for ints should accept: "
            + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("8");
    }

    // ── Cell group (g): mixed widths — promoted ──

    [Fact]
    public void MixedNarrowWidths_Refused_SPY0220()
    {
        const string source = @"
def main() -> None:
    a: int8 = 5
    b: int16 = 3
    c: int16 = a + b
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse("mixed narrow widths should be promoted to int32 and refused");
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            "mixed narrow widths should produce SPY0220");
    }

    // ── Cell group (h): narrow int store from int — positive control for SPY0220 ──

    [Fact]
    public void NarrowStoreFromInt_PositiveControl_Refused()
    {
        const string source = @"
def main() -> None:
    c: int = 5 + 3
    print(c)
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue("int store from int arithmetic should accept: "
            + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("8");
    }

    // ── Promotion to int is OK — the fix, not the refusal ──

    [Fact]
    public void NarrowArithmeticStoredInInt_Accepted()
    {
        const string source = @"
def main() -> None:
    a: int8 = 5
    b: int8 = 3
    c: int = a + b
    print(c)
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue("narrow arithmetic stored in int should accept: "
            + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("8");
    }
}
