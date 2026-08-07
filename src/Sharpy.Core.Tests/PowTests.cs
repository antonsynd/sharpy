using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Pow_Tests
{
    [Fact]
    public void Pow_PositiveIntegers_ReturnsCorrectPower()
    {
        // When
        var result = Pow(2, 3);

        // Then
        result.Should().BeApproximately(8.0, 0.001);
    }

    [Fact]
    public void Pow_Doubles_ReturnsCorrectPower()
    {
        // When
        var result = Pow(2.0, 3.0);

        // Then
        result.Should().BeApproximately(8.0, 0.001);
    }

    [Fact]
    public void Pow_FractionalExponent_ReturnsSquareRoot()
    {
        // When
        var result = Pow(9.0, 0.5);

        // Then
        result.Should().BeApproximately(3.0, 0.001);
    }

    [Fact]
    public void Pow_ZeroExponent_ReturnsOne()
    {
        // When
        var result = Pow(5, 0);

        // Then
        result.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void Pow_NegativeExponent_ReturnsReciprocal()
    {
        // When
        var result = Pow(2.0, -2.0);

        // Then
        result.Should().BeApproximately(0.25, 0.001);
    }

    [Theory]
    [InlineData(2, 10, 1024)]
    [InlineData(2, 0, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 5, 0)]
    [InlineData(1, 100, 1)]
    [InlineData(5, 3, 125)]
    [InlineData(-2, 3, -8)]
    [InlineData(-2, 4, 16)]
    public void CheckedIntPow_Int_InRange_ReturnsExactResult(int x, int y, int expected)
    {
        // When/then
        CheckedIntPow(x, y).Should().Be(expected);
    }

    [Fact]
    public void CheckedIntPow_Long_ExactResultBeyond2Pow53()
    {
        // 3 ** 39 = 4052555153018976267, exact in long but not in double
        CheckedIntPow(3L, 39L).Should().Be(4052555153018976267L);
    }

    [Fact]
    public void CheckedIntPow_Int_Overflow_ThrowsOverflowError()
    {
        // 2 ** 31 overflows int (max 2^31 - 1)
        System.Action act = () => CheckedIntPow(2, 31);

        act.Should().Throw<OverflowError>();
    }

    [Fact]
    public void CheckedIntPow_Long_Overflow_ThrowsOverflowError()
    {
        // 2 ** 63 overflows long (max 2^63 - 1)
        System.Action act = () => CheckedIntPow(2L, 63L);

        act.Should().Throw<OverflowError>();
    }

    // The two tests that stood here pinned the RETIRED contract: a negative exponent threw
    // ArgumentOutOfRangeException, which is exactly what forced the emitter to wrap every call in
    // a dispatch ternary and regenerate its operands (#1228). #1228 moves that dispatch into the
    // helper, so the throw is gone by design and these tests pinned behavior that no longer
    // exists. They are REPLACED, not deleted: CheckedIntPow_NegativeExponent_Tests below pins the
    // new contract, including an explicit "no longer throws" assertion so the removal itself stays
    // covered rather than merely untested.
}

/// <summary>
/// <c>CheckedIntPow</c>'s negative-exponent contract, absorbed from the emitter in #1228.
/// </summary>
/// <remarks>
/// The emitter used to wrap the call in a <c>y &lt; 0</c> ternary that dispatched between this
/// method and a double path — which regenerated both operands, and regeneration can re-push
/// hoisted statements that then run unconditionally. Absorbing the case here lets the emitter
/// emit one invocation splicing each operand once.
/// </remarks>
public class CheckedIntPow_NegativeExponent_Tests
{
    // DELIBERATE CPython divergence, and the spec is the oracle here rather than python3.
    // arithmetic_operators.md: "Integer ** negative exponent | Truncating Math.Pow double path
    // (int ** int stays int, e.g. 2 ** -1 is 0)". CPython instead returns FLOATS:
    // 2 ** -1 is 0.5, 1 ** -5 is 1.0, (-1) ** -3 is -1.0. A reader following "verify against
    // python3 first" would mistake these pinned values for bugs — they are not.
    [Theory]
    [InlineData(2, -1, 0)]      // CPython: 0.5
    [InlineData(1, -5, 1)]      // CPython: 1.0
    [InlineData(-1, -3, -1)]    // CPython: -1.0
    [InlineData(10, -2, 0)]     // CPython: 0.01
    [InlineData(-1, -2, 1)]     // CPython: 1.0
    public void CheckedIntPow_Int_NegativeExponent_TruncatesPerSpec(int x, int y, int expected)
    {
        CheckedIntPow(x, y).Should().Be(expected);
    }

    [Theory]
    [InlineData(2L, -1L, 0L)]
    [InlineData(1L, -5L, 1L)]
    [InlineData(-1L, -3L, -1L)]
    public void CheckedIntPow_Long_NegativeExponent_TruncatesPerSpec(long x, long y, long expected)
    {
        CheckedIntPow(x, y).Should().Be(expected);
    }

    // The previous contract. Pinning the removal explicitly: a negative exponent used to throw
    // ArgumentOutOfRangeException, which is what forced the emitter's dispatch ternary.
    [Fact]
    public void CheckedIntPow_NegativeExponent_NoLongerThrows()
    {
        var act = () => CheckedIntPow(2, -1);

        act.Should().NotThrow();
    }

    // The non-negative path is untouched — these are the regression floor.
    [Theory]
    [InlineData(3, 4, 81)]
    [InlineData(2, 10, 1024)]
    [InlineData(0, 0, 1)]
    [InlineData(-2, 3, -8)]
    [InlineData(-2, 2, 4)]
    public void CheckedIntPow_Int_NonNegativeExponent_Unchanged(int x, int y, int expected)
    {
        CheckedIntPow(x, y).Should().Be(expected);
    }

    [Fact]
    public void CheckedIntPow_Int_Overflow_StillRaisesOverflowError()
    {
        var act = () => CheckedIntPow(2, 31);

        act.Should().Throw<OverflowError>();
    }

    // Exactness above 2^53 is the reason CheckedIntPow exists rather than a Math.Pow cast:
    // 3 ** 40 = 12157665459056928801 is beyond a double's 53-bit mantissa. Value from
    // python3 -c "print(3 ** 39)".
    [Fact]
    public void CheckedIntPow_Long_IsExactAboveDoublePrecision()
    {
        CheckedIntPow(3L, 39L).Should().Be(4052555153018976267L);
    }
}
