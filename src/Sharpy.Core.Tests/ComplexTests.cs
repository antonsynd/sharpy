using Xunit;
using FluentAssertions;
using Sharpy;

namespace Sharpy.Core.Tests;

/// <summary>
/// <c>Sharpy.Complex</c> arithmetic and CPython-shaped rendering (#1362).
/// </summary>
/// <remarks>
/// Before this, <c>Complex</c> declared exactly two operators and BOTH were conversions, so
/// <c>a + b</c> drew <c>SPY0222: Type 'Complex' does not support operator '+'</c> — discovery
/// resolving no operators was correct, because there were none. It also had no
/// <c>ToString()</c>, so printing one gave the CLR type name.
///
/// <para>
/// EVERY EXPECTED STRING BELOW WAS MEASURED against python3 3.12, not inferred. The format has
/// three irregularities that reward measuring: positive-zero real drops the parentheses while
/// NEGATIVE zero keeps them, the imaginary sign follows its own sign bit (so <c>-0.0</c> prints
/// <c>-0j</c>), and components print without a trailing <c>.0</c> even though <c>repr(4.0)</c>
/// is <c>4.0</c>.
/// </para>
/// </remarks>
public class ComplexTests
{
    // ===== Arithmetic (the #1362 repro) =====

    [Fact]
    public void Add_MatchesCPython()
        => (new Complex(1.0, 2.0) + new Complex(3.0, -1.0)).ToString().Should().Be("(4+1j)");

    [Fact]
    public void Multiply_MatchesCPython()
        => (new Complex(1.0, 2.0) * new Complex(3.0, -1.0)).ToString().Should().Be("(5+5j)");

    [Fact]
    public void Subtract_MatchesCPython()
        => (new Complex(1.0, 2.0) - new Complex(3.0, -1.0)).ToString().Should().Be("(-2+3j)");

    [Fact]
    public void Divide_KeepsFullPrecision()
    {
        // The cell a naive formatter gets wrong: CPython prints the full round-trip digits.
        (new Complex(1.0, 2.0) / new Complex(3.0, -1.0)).ToString()
            .Should().Be("(0.1+0.7000000000000001j)");
    }

    [Fact]
    public void Negate_MatchesCPython()
        => (-new Complex(1.0, 2.0)).ToString().Should().Be("(-1-2j)");

    // ===== Rendering =====

    [Theory]
    [InlineData(4.0, 1.0, "(4+1j)")]
    [InlineData(5.0, 5.0, "(5+5j)")]
    [InlineData(0.0, 1.0, "1j")]           // positive-zero real drops parens AND the real term
    [InlineData(0.0, 0.0, "0j")]
    [InlineData(0.0, -1.0, "-1j")]
    [InlineData(0.0, -0.0, "-0j")]
    [InlineData(3.0, 0.0, "(3+0j)")]
    [InlineData(2.5, 0.0, "(2.5+0j)")]
    [InlineData(-1.0, -2.0, "(-1-2j)")]
    [InlineData(1.0, -1.0, "(1-1j)")]
    [InlineData(1.5, 2.25, "(1.5+2.25j)")]
    [InlineData(-0.0, 1.0, "(-0+1j)")]     // NEGATIVE zero real keeps them — the sign-bit case
    [InlineData(-0.0, -0.0, "(-0-0j)")]
    [InlineData(1e300, 1e-300, "(1e+300+1e-300j)")]
    public void ToString_MatchesCPython(double real, double imag, string expected)
        => new Complex(real, imag).ToString().Should().Be(expected);

    [Fact]
    public void ToString_Infinity()
        => new Complex(double.PositiveInfinity, 1.0).ToString().Should().Be("(inf+1j)");

    [Fact]
    public void ToString_NaN_UsesPlus_DespiteDotNetsSignedNaN()
    {
        // .NET's double.NaN is 0xfff8... (sign bit SET) while CPython's is 0x7ff8... (clear).
        // Reading the bit faithfully would print "(1-nanj)"; CPython prints "(1+nanj)".
        new Complex(1.0, double.NaN).ToString().Should().Be("(1+nanj)");
    }

    // ===== Equality =====

    [Fact]
    public void Equality_ComparesBothComponents()
    {
        (new Complex(1.0, 2.0) == new Complex(1.0, 2.0)).Should().BeTrue();
        (new Complex(1.0, 2.0) != new Complex(1.0, 2.5)).Should().BeTrue();
        new Complex(1.0, 2.0).Equals(new Complex(1.0, 2.0)).Should().BeTrue();
        new Complex(1.0, 2.0).Equals("not a complex").Should().BeFalse();
    }

    [Fact]
    public void EqualValues_HashAlike()
        => new Complex(1.0, 2.0).GetHashCode().Should().Be(new Complex(1.0, 2.0).GetHashCode());

    [Fact]
    public void RealAndImag_SurviveArithmetic()
    {
        var c = new Complex(1.0, 2.0) + new Complex(3.0, -1.0);
        c.Real.Should().Be(4.0);
        c.Imag.Should().Be(1.0);
        c.Conjugate().ToString().Should().Be("(4-1j)");
    }
}
