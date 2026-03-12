namespace Sharpy.Compiler.Text;

/// <summary>
/// Represents the source text of a compilation unit with efficient line/column lookup.
/// </summary>
/// <remarks>
/// SourceText provides O(log n) line number lookup from character offsets using
/// binary search over precomputed line start positions. Line and column numbers
/// are 1-based to match common editor conventions and error reporting.
///
/// The text is immutable once created, enabling safe sharing across compilation stages.
/// </remarks>
public sealed class SourceText
{
    private readonly string _text;
    private readonly int[] _lineStarts;

    /// <summary>
    /// The file path of the source, or null if the source is not from a file.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// The total length of the source text in characters.
    /// </summary>
    public int Length => _text.Length;

    /// <summary>
    /// The number of lines in the source text.
    /// </summary>
    public int LineCount => _lineStarts.Length;

    /// <summary>
    /// Creates a new SourceText from a string.
    /// </summary>
    /// <param name="text">The source text content.</param>
    /// <param name="filePath">Optional file path for error reporting.</param>
    public SourceText(string text, string? filePath = null)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        FilePath = filePath;
        _lineStarts = ComputeLineStarts(text);
    }

    /// <summary>
    /// Creates a SourceText from a file.
    /// </summary>
    /// <param name="filePath">The path to the source file.</param>
    /// <returns>A new SourceText with the file contents.</returns>
    public static SourceText FromFile(string filePath)
    {
        var text = File.ReadAllText(filePath);
        return new SourceText(text, filePath);
    }

    /// <summary>
    /// Gets the character at the specified position.
    /// </summary>
    /// <param name="position">The zero-based character position.</param>
    /// <returns>The character at the position.</returns>
    public char this[int position] => _text[position];

    /// <summary>
    /// Gets the text content of a span.
    /// </summary>
    /// <param name="span">The span to extract.</param>
    /// <returns>The text within the span.</returns>
    public string GetText(TextSpan span)
    {
        if (span.Start < 0 || span.End > _text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(span), "Span is outside the bounds of the source text.");
        }
        return _text.Substring(span.Start, span.Length);
    }

    /// <summary>
    /// Gets the full text content.
    /// </summary>
    /// <returns>The complete source text as a string.</returns>
    public override string ToString() => _text;

    /// <summary>
    /// Gets the 1-based line number for a character position.
    /// </summary>
    /// <param name="position">The zero-based character position.</param>
    /// <returns>The 1-based line number.</returns>
    public int GetLineNumber(int position)
    {
        if (position < 0 || position > _text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position is outside the bounds of the source text.");
        }

        // Binary search to find the line containing the position
        int low = 0;
        int high = _lineStarts.Length - 1;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            int lineStart = _lineStarts[mid];

            if (position < lineStart)
            {
                high = mid - 1;
            }
            else if (mid + 1 < _lineStarts.Length && position >= _lineStarts[mid + 1])
            {
                low = mid + 1;
            }
            else
            {
                return mid + 1; // Convert to 1-based
            }
        }

        return _lineStarts.Length; // Last line
    }

    /// <summary>
    /// Gets the 1-based column number for a character position.
    /// </summary>
    /// <param name="position">The zero-based character position.</param>
    /// <returns>The 1-based column number.</returns>
    public int GetColumnNumber(int position)
    {
        if (position < 0 || position > _text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position is outside the bounds of the source text.");
        }

        int lineNumber = GetLineNumber(position);
        int lineStart = _lineStarts[lineNumber - 1]; // Convert back to 0-based index
        return position - lineStart + 1; // Convert to 1-based column
    }

    /// <summary>
    /// Gets both line and column numbers for a character position.
    /// </summary>
    /// <param name="position">The zero-based character position.</param>
    /// <returns>A tuple of (1-based line number, 1-based column number).</returns>
    public (int Line, int Column) GetLineAndColumn(int position)
    {
        int line = GetLineNumber(position);
        int lineStart = _lineStarts[line - 1];
        int column = position - lineStart + 1;
        return (line, column);
    }

    /// <summary>
    /// Gets the character position for a 1-based line and column.
    /// </summary>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="column">The 1-based column number.</param>
    /// <returns>The zero-based character position.</returns>
    public int GetPosition(int line, int column)
    {
        if (line < 1 || line > _lineStarts.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Line number is out of range.");
        }
        if (column < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(column), "Column number must be at least 1.");
        }

        int lineStart = _lineStarts[line - 1];
        int position = lineStart + column - 1;

        // Clamp to end of text
        if (position > _text.Length)
        {
            position = _text.Length;
        }

        return position;
    }

    /// <summary>
    /// Gets the text of a specific line (without line ending).
    /// </summary>
    /// <param name="lineNumber">The 1-based line number.</param>
    /// <returns>The text of the line.</returns>
    public string GetLineText(int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > _lineStarts.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number is out of range.");
        }

        int lineStart = _lineStarts[lineNumber - 1];
        int lineEnd;

        if (lineNumber < _lineStarts.Length)
        {
            // Find end by looking at next line start, excluding newline characters
            lineEnd = _lineStarts[lineNumber];
            // Trim trailing \r\n or \n
            while (lineEnd > lineStart && (_text[lineEnd - 1] == '\n' || _text[lineEnd - 1] == '\r'))
            {
                lineEnd--;
            }
        }
        else
        {
            // Last line - goes to end of text
            lineEnd = _text.Length;
        }

        return _text.Substring(lineStart, lineEnd - lineStart);
    }

    /// <summary>
    /// Returns a new SourceText with the specified changes applied.
    /// </summary>
    /// <param name="changes">The text changes to apply.</param>
    /// <returns>A new SourceText with the changes applied.</returns>
    /// <remarks>
    /// Changes are applied in reverse offset order to maintain position validity.
    /// </remarks>
    public SourceText WithChanges(IEnumerable<TextChange> changes)
    {
        var sortedChanges = changes
            .OrderByDescending(c => c.Span.Start)
            .ToList();

        if (sortedChanges.Count == 0)
        {
            return new SourceText(_text, FilePath);
        }

        // Validate that changes don't overlap (descending order, so each span
        // must end at or before the next span's start)
        for (int i = 0; i < sortedChanges.Count - 1; i++)
        {
            if (sortedChanges[i + 1].Span.End > sortedChanges[i].Span.Start)
            {
                throw new ArgumentException(
                    $"Text changes overlap: [{sortedChanges[i + 1].Span.Start}..{sortedChanges[i + 1].Span.End}) and [{sortedChanges[i].Span.Start}..{sortedChanges[i].Span.End})");
            }
        }

        var result = _text;
        foreach (var change in sortedChanges)
        {
            result = string.Concat(
                result.AsSpan(0, change.Span.Start),
                change.NewText,
                result.AsSpan(change.Span.End));
        }

        return new SourceText(result, FilePath);
    }

    /// <summary>
    /// Computes the starting character position of each line.
    /// </summary>
    private static int[] ComputeLineStarts(string text)
    {
        var lineStarts = new List<int> { 0 }; // First line always starts at 0

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                // Start of next line is after this newline
                lineStarts.Add(i + 1);
            }
            else if (c == '\r')
            {
                // Handle \r\n as single line ending
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    lineStarts.Add(i + 2);
                    i++; // Skip the \n
                }
                else
                {
                    // Standalone \r
                    lineStarts.Add(i + 1);
                }
            }
        }

        return lineStarts.ToArray();
    }
}
