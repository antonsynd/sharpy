using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Row 5 of the #2404 element-access roster: <see cref="IndexExtensions.ToNormalizedInt32"/> (public, C#-only;
/// its one caller is <see cref="RangeExtensions.ToSlice"/>) has two modes. Both flags false: strict element
/// access, which raises <see cref="IndexError"/>. Either flag true: a clamped slice or insertion position,
/// which never raises. Before P76's verify round nothing in the repository executed it (#2404 verify X8).
/// <c>^0</c> is deliberately not pinned: it normalizes to 0 rather than to the end (pre-existing at 89fba5eec).
/// </summary>
public class IndexExtensionsTests
{
    [Theory]
    [InlineData(0, false, 0)]
    [InlineData(2, false, 2)]
    [InlineData(1, true, 2)]
    [InlineData(3, true, 0)]
    public void Strict_InRange_ReturnsTheElementOffset(int value, bool fromEnd, int expected)
    {
        new System.Index(value, fromEnd).ToNormalizedInt32(3, false, false).Should().Be(expected);
    }

    [Theory]
    [InlineData(3, false, "list index 3 out of range")]
    [InlineData(4, true, "list index -4 out of range")]
    public void Strict_OutOfRange_RaisesIndexError(int value, bool fromEnd, string message)
    {
        var act = () => new System.Index(value, fromEnd).ToNormalizedInt32(3, false, false);

        act.Should().Throw<IndexError>().Which.Message.Should().Be(message);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void EitherFlag_ClampsToZeroThroughMax_InsteadOfRaising(bool forSlice, bool forInsertion)
    {
        new System.Index(10).ToNormalizedInt32(3, forSlice, forInsertion).Should().Be(3);
        new System.Index(3).ToNormalizedInt32(3, forSlice, forInsertion).Should().Be(3);
        new System.Index(10, fromEnd: true).ToNormalizedInt32(3, forSlice, forInsertion).Should().Be(0);
        new System.Index(2, fromEnd: true).ToNormalizedInt32(3, forSlice, forInsertion).Should().Be(1);
    }

    [Fact]
    public void ToSlice_NormalizesBothEndsAsClampedPositions()
    {
        var slice = (1..^1).ToSlice(4, false);
        slice.Start.Should().Be(1);
        slice.Stop.Should().Be(3);

        var clamped = (^10..10).ToSlice(4, false);
        clamped.Start.Should().Be(0);
        clamped.Stop.Should().Be(4);
    }
}
