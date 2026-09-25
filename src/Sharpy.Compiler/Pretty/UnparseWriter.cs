using System.Text;

namespace Sharpy.Compiler.Pretty;

internal sealed class UnparseWriter
{
    private readonly StringBuilder _sb = new();
    private readonly string _indent;
    private readonly string _lineEnding;
    private int _indentLevel;
    private bool _atLineStart = true;

    // Output spans written verbatim that contain a line break (a multi-line replacement field,
    // #2022); a trailing-comment insertion must never land inside one.
    private readonly List<(int Start, int End)> _opaqueMultiLineSpans = new();

    public UnparseWriter(UnparseOptions options)
    {
        _indent = options.IndentString;
        _lineEnding = options.LineEnding;
    }

    public void Indent() => _indentLevel++;
    public void Dedent() => _indentLevel--;

    public void Write(string text)
    {
        if (_atLineStart && text.Length > 0)
        {
            for (int i = 0; i < _indentLevel; i++)
                _sb.Append(_indent);
            _atLineStart = false;
        }
        _sb.Append(text);
    }

    /// <summary>
    /// Writes source text that must reach the output byte-for-byte (a replacement field's raw text,
    /// #2024) and may span lines (#2022): its line breaks are skipped by
    /// <see cref="IndexOfLineEndingOutsideOpaque"/>.
    /// </summary>
    public void WriteOpaque(string text)
    {
        Write(text);
        if (text.AsSpan().IndexOfAny('\n', '\r') >= 0)
            _opaqueMultiLineSpans.Add((_sb.Length - text.Length, _sb.Length));
    }

    public void WriteLine()
    {
        _sb.Append(_lineEnding);
        _atLineStart = true;
    }

    public void WriteLine(string text)
    {
        Write(text);
        WriteLine();
    }

    public int Length => _sb.Length;

    public void InsertAt(int position, string text)
    {
        _sb.Insert(position, text);
        for (int i = 0; i < _opaqueMultiLineSpans.Count; i++)
        {
            var (start, end) = _opaqueMultiLineSpans[i];
            if (start >= position)
                _opaqueMultiLineSpans[i] = (start + text.Length, end + text.Length);
        }
    }

    /// <summary>
    /// The first <paramref name="lineEnding"/> at or after <paramref name="startIndex"/> that is not
    /// inside text written by <see cref="WriteOpaque"/>, or -1.
    /// </summary>
    public int IndexOfLineEndingOutsideOpaque(string lineEnding, int startIndex)
    {
        var index = IndexOf(lineEnding, startIndex);
        while (index >= 0)
        {
            var inside = _opaqueMultiLineSpans.FindIndex(s => s.Start <= index && index < s.End);
            if (inside < 0)
                return index;
            index = IndexOf(lineEnding, _opaqueMultiLineSpans[inside].End);
        }
        return -1;
    }

    public char CharAt(int position) => _sb[position];

    public int IndexOf(string value, int startIndex)
    {
        var str = _sb.ToString(startIndex, _sb.Length - startIndex);
        var idx = str.IndexOf(value, StringComparison.Ordinal);
        return idx >= 0 ? startIndex + idx : -1;
    }

    public override string ToString() => _sb.ToString();
}
