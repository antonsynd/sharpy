namespace Sharpy.Compiler.Text;

/// <summary>
/// Represents a span of text in a source file using character offsets.
/// This is a value type for efficient memory usage and zero-cost abstraction.
/// </summary>
/// <remarks>
/// TextSpan uses zero-based character offsets into the source text. The span
/// includes the character at Start and excludes the character at Start + Length.
///
/// Example: For source "hello world", TextSpan(0, 5) represents "hello".
/// </remarks>
public readonly struct TextSpan : IEquatable<TextSpan>
{
    /// <summary>
    /// The zero-based character offset where this span starts.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// The number of characters in this span.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// The exclusive end position (Start + Length).
    /// </summary>
    public int End => Start + Length;

    /// <summary>
    /// Returns true if this span has zero length.
    /// </summary>
    public bool IsEmpty => Length == 0;

    /// <summary>
    /// Creates a new TextSpan with the specified start position and length.
    /// </summary>
    /// <param name="start">The zero-based character offset where this span starts.</param>
    /// <param name="length">The number of characters in this span.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if start or length is negative.</exception>
    public TextSpan(int start, int length)
    {
        if (start < 0)
            throw new ArgumentOutOfRangeException(nameof(start), "Start position cannot be negative.");
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative.");

        Start = start;
        Length = length;
    }

    /// <summary>
    /// Creates a TextSpan from start and end positions.
    /// </summary>
    /// <param name="start">The zero-based character offset where this span starts.</param>
    /// <param name="end">The exclusive end position.</param>
    /// <returns>A new TextSpan covering the range [start, end).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if start or end is invalid.</exception>
    public static TextSpan FromBounds(int start, int end)
    {
        if (start < 0)
            throw new ArgumentOutOfRangeException(nameof(start), "Start position cannot be negative.");
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end), "End position cannot be less than start position.");

        return new TextSpan(start, end - start);
    }

    /// <summary>
    /// Returns true if this span contains the specified position.
    /// </summary>
    /// <param name="position">The character position to check.</param>
    /// <returns>True if position is within [Start, End).</returns>
    public bool Contains(int position)
    {
        return position >= Start && position < End;
    }

    /// <summary>
    /// Returns true if this span fully contains another span.
    /// </summary>
    /// <param name="other">The span to check.</param>
    /// <returns>True if this span contains the entire other span.</returns>
    public bool Contains(TextSpan other)
    {
        return other.Start >= Start && other.End <= End;
    }

    /// <summary>
    /// Returns true if this span overlaps with another span.
    /// </summary>
    /// <param name="other">The span to check for overlap.</param>
    /// <returns>True if the spans share at least one character position.</returns>
    public bool OverlapsWith(TextSpan other)
    {
        return Start < other.End && other.Start < End;
    }

    /// <summary>
    /// Returns the intersection of this span with another span.
    /// </summary>
    /// <param name="other">The span to intersect with.</param>
    /// <returns>The overlapping portion, or null if the spans don't overlap.</returns>
    public TextSpan? Intersection(TextSpan other)
    {
        int intersectStart = System.Math.Max(Start, other.Start);
        int intersectEnd = System.Math.Min(End, other.End);

        if (intersectStart < intersectEnd)
        {
            return FromBounds(intersectStart, intersectEnd);
        }

        return null;
    }

    /// <summary>
    /// Returns the smallest span that contains both this span and another span.
    /// </summary>
    /// <param name="other">The span to combine with.</param>
    /// <returns>A span that encompasses both spans.</returns>
    public TextSpan Union(TextSpan other)
    {
        int unionStart = System.Math.Min(Start, other.Start);
        int unionEnd = System.Math.Max(End, other.End);
        return FromBounds(unionStart, unionEnd);
    }

    /// <summary>
    /// An empty span at position 0.
    /// </summary>
    public static readonly TextSpan Empty = new(0, 0);

    public bool Equals(TextSpan other)
    {
        return Start == other.Start && Length == other.Length;
    }

    public override bool Equals(object? obj)
    {
        return obj is TextSpan other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Start, Length);
    }

    public static bool operator ==(TextSpan left, TextSpan right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TextSpan left, TextSpan right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"[{Start}..{End})";
    }
}
