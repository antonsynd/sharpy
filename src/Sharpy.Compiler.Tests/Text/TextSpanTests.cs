using Sharpy.Compiler.Text;
using Xunit;

namespace Sharpy.Compiler.Tests.Text;

public class TextSpanTests
{
    [Fact]
    public void Constructor_ValidParameters_SetsProperties()
    {
        var span = new TextSpan(10, 5);

        Assert.Equal(10, span.Start);
        Assert.Equal(5, span.Length);
        Assert.Equal(15, span.End);
        Assert.False(span.IsEmpty);
    }

    [Fact]
    public void Constructor_ZeroLength_CreatesEmptySpan()
    {
        var span = new TextSpan(10, 0);

        Assert.Equal(10, span.Start);
        Assert.Equal(0, span.Length);
        Assert.Equal(10, span.End);
        Assert.True(span.IsEmpty);
    }

    [Fact]
    public void Constructor_NegativeStart_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextSpan(-1, 5));
    }

    [Fact]
    public void Constructor_NegativeLength_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextSpan(0, -1));
    }

    [Fact]
    public void FromBounds_ValidBounds_CreatesCorrectSpan()
    {
        var span = TextSpan.FromBounds(5, 15);

        Assert.Equal(5, span.Start);
        Assert.Equal(10, span.Length);
        Assert.Equal(15, span.End);
    }

    [Fact]
    public void FromBounds_SameStartAndEnd_CreatesEmptySpan()
    {
        var span = TextSpan.FromBounds(10, 10);

        Assert.Equal(10, span.Start);
        Assert.Equal(0, span.Length);
        Assert.True(span.IsEmpty);
    }

    [Fact]
    public void FromBounds_EndBeforeStart_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextSpan.FromBounds(10, 5));
    }

    [Fact]
    public void Contains_PositionWithinSpan_ReturnsTrue()
    {
        var span = new TextSpan(10, 10);

        Assert.True(span.Contains(10));
        Assert.True(span.Contains(15));
        Assert.True(span.Contains(19));
    }

    [Fact]
    public void Contains_PositionAtEnd_ReturnsFalse()
    {
        var span = new TextSpan(10, 10);

        Assert.False(span.Contains(20));
    }

    [Fact]
    public void Contains_PositionBeforeSpan_ReturnsFalse()
    {
        var span = new TextSpan(10, 10);

        Assert.False(span.Contains(9));
    }

    [Fact]
    public void Contains_OtherSpanFullyContained_ReturnsTrue()
    {
        var outer = new TextSpan(10, 20);
        var inner = new TextSpan(15, 5);

        Assert.True(outer.Contains(inner));
    }

    [Fact]
    public void Contains_OtherSpanPartiallyOverlapping_ReturnsFalse()
    {
        var span1 = new TextSpan(10, 10);
        var span2 = new TextSpan(15, 10);

        Assert.False(span1.Contains(span2));
    }

    [Fact]
    public void OverlapsWith_OverlappingSpans_ReturnsTrue()
    {
        var span1 = new TextSpan(10, 10); // [10..20)
        var span2 = new TextSpan(15, 10); // [15..25)

        Assert.True(span1.OverlapsWith(span2));
        Assert.True(span2.OverlapsWith(span1));
    }

    [Fact]
    public void OverlapsWith_AdjacentSpans_ReturnsFalse()
    {
        var span1 = new TextSpan(10, 10); // [10..20)
        var span2 = new TextSpan(20, 10); // [20..30)

        Assert.False(span1.OverlapsWith(span2));
        Assert.False(span2.OverlapsWith(span1));
    }

    [Fact]
    public void OverlapsWith_DisjointSpans_ReturnsFalse()
    {
        var span1 = new TextSpan(10, 5);  // [10..15)
        var span2 = new TextSpan(20, 5);  // [20..25)

        Assert.False(span1.OverlapsWith(span2));
    }

    [Fact]
    public void Intersection_OverlappingSpans_ReturnsIntersection()
    {
        var span1 = new TextSpan(10, 10); // [10..20)
        var span2 = new TextSpan(15, 10); // [15..25)

        var intersection = span1.Intersection(span2);

        Assert.NotNull(intersection);
        Assert.Equal(15, intersection!.Value.Start);
        Assert.Equal(5, intersection.Value.Length);
        Assert.Equal(20, intersection.Value.End);
    }

    [Fact]
    public void Intersection_DisjointSpans_ReturnsNull()
    {
        var span1 = new TextSpan(10, 5);  // [10..15)
        var span2 = new TextSpan(20, 5);  // [20..25)

        Assert.Null(span1.Intersection(span2));
    }

    [Fact]
    public void Union_OverlappingSpans_ReturnsUnion()
    {
        var span1 = new TextSpan(10, 10); // [10..20)
        var span2 = new TextSpan(15, 10); // [15..25)

        var union = span1.Union(span2);

        Assert.Equal(10, union.Start);
        Assert.Equal(15, union.Length);
        Assert.Equal(25, union.End);
    }

    [Fact]
    public void Union_DisjointSpans_ReturnsSpanCoveringBoth()
    {
        var span1 = new TextSpan(10, 5);  // [10..15)
        var span2 = new TextSpan(20, 5);  // [20..25)

        var union = span1.Union(span2);

        Assert.Equal(10, union.Start);
        Assert.Equal(15, union.Length);
        Assert.Equal(25, union.End);
    }

    [Fact]
    public void Empty_IsEmptySpanAtZero()
    {
        Assert.Equal(0, TextSpan.Empty.Start);
        Assert.Equal(0, TextSpan.Empty.Length);
        Assert.True(TextSpan.Empty.IsEmpty);
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var span1 = new TextSpan(10, 5);
        var span2 = new TextSpan(10, 5);

        Assert.True(span1.Equals(span2));
        Assert.True(span1 == span2);
        Assert.False(span1 != span2);
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var span1 = new TextSpan(10, 5);
        var span2 = new TextSpan(10, 6);
        var span3 = new TextSpan(11, 5);

        Assert.False(span1.Equals(span2));
        Assert.False(span1.Equals(span3));
        Assert.False(span1 == span2);
        Assert.True(span1 != span2);
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHashCode()
    {
        var span1 = new TextSpan(10, 5);
        var span2 = new TextSpan(10, 5);

        Assert.Equal(span1.GetHashCode(), span2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        var span = new TextSpan(10, 5);

        Assert.Equal("[10..15)", span.ToString());
    }
}
