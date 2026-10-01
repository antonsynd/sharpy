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
    // #2022; a triple-quoted string or docstring, #2068); a trailing-comment insertion must never
    // land inside one.
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
    /// Writes text that must reach the output byte-for-byte and may span lines — a replacement
    /// field's raw text (#2024, #2022), a triple-quoted string's body (#2068): its line breaks are
    /// skipped by
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

    /// <summary>The width of the indentation the next line will be written at.</summary>
    public int IndentWidth => _indentLevel * _indent.Length;

    /// <summary>The indentation level the next line will be written at.</summary>
    public int IndentLevel => _indentLevel;

    /// <summary>Writes a full line at indentation level <paramref name="level"/> (≤ the current one), leaving the current level unchanged.</summary>
    public void WriteLineAtLevel(int level, string text)
    {
        var saved = _indentLevel;
        _indentLevel = level;
        WriteLine(text);
        _indentLevel = saved;
    }

    /// <summary>True when the next <see cref="Write"/> starts a line (and writes the indentation first).</summary>
    public bool AtLineStart => _atLineStart;

    /// <summary>
    /// Replaces the output [<paramref name="start"/>, <paramref name="end"/>) with
    /// <paramref name="text"/> written verbatim (a header's source slice, P22b Decision 5): the
    /// replaced range's opaque spans are dropped, later spans shift, and the new text is opaque when
    /// it spans lines — so a trailing-comment insertion lands after it, never inside it.
    /// </summary>
    public void ReplaceRangeOpaque(int start, int end, string text)
    {
        _sb.Remove(start, end - start);
        _sb.Insert(start, text);
        var delta = text.Length - (end - start);
        _opaqueMultiLineSpans.RemoveAll(s => s.Start >= start && s.End <= end);
        for (int i = 0; i < _opaqueMultiLineSpans.Count; i++)
        {
            var (s, e) = _opaqueMultiLineSpans[i];
            if (s >= end)
                _opaqueMultiLineSpans[i] = (s + delta, e + delta);
        }
        if (text.AsSpan().IndexOfAny('\n', '\r') >= 0)
            _opaqueMultiLineSpans.Add((start, start + text.Length));
    }

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
