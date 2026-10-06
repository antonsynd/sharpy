using System.Text;

namespace Sharpy.Compiler.Formatting;

/// <summary>
/// One change between a source and a target text, over line CONTENT: replace the source lines
/// <c>[Start, End)</c> with <see cref="NewLines"/>. <c>Start == End</c> is a pure insertion before
/// source line <c>Start</c> (<c>Start == n</c> inserts after the last line); an empty
/// <see cref="NewLines"/> is a pure deletion. Line numbers are 0-based.
/// </summary>
internal readonly record struct LineHunk(int Start, int End, IReadOnlyList<string> NewLines);

/// <summary>
/// The hunks between a source and its formatted text, and their application (P22e, #2168).
///
/// <para><b>Lines.</b> A text is split on <c>\r\n</c>, <c>\n</c> and a lone <c>\r</c> — the lexer's
/// and <c>LiteralSpans</c>' definition. A text with <c>k</c> line breaks has <c>k + 1</c> lines, the
/// last one unterminated: <c>"a\nb"</c> is <c>["a", "b"]</c>, <c>"a\nb\n"</c> is
/// <c>["a", "b", ""]</c> and the empty text is <c>[""]</c>. So a missing final line break is
/// modelled: <c>"a\nb"</c> → <c>"a\nb\n"</c> is the insertion of <c>""</c> at <c>n</c>.</para>
///
/// <para><b>Hunks</b> compare line content only (ordinal). The common prefix and suffix are trimmed
/// and the middle is diffed by an exact LCS, so the source lines the hunks cover number exactly
/// <c>n − LCS</c>; a middle of more than <see cref="DefaultMaxCells"/> cells (source × target lines)
/// is returned as ONE hunk instead — coarser, still correct. Hunks are sorted, non-overlapping and
/// non-adjacent (<c>h[k].End &lt; h[k+1].Start</c>): an insertion touching a replacement is one hunk.</para>
///
/// <para><b>Apply never edits a line break it does not have to.</b> An unchanged line keeps its own
/// terminator. A hunk replacing <c>[a, b)</c> (<c>b &gt; a</c>) keeps source line <c>b − 1</c>'s
/// terminator as the terminator of its last new line; every other line break a hunk writes is the
/// document's own (its first line break, <c>\n</c> when it has none). That is the text-edit shape
/// <c>(a,0)–(b−1,len)</c> ← <c>join(NewLines, eol)</c>. A CRLF document stays CRLF. The last line of the
/// result is unterminated, so a deletion through the last line drops the preceding line's
/// terminator, and an insertion at <c>n</c> gives the old last line an <c>eol</c>.</para>
///
/// <para><c>FormatRunner</c>'s private LCS (<c>--diff</c>) is deliberately separate: trimming the
/// prefix and suffix can change which of two equal lines it prints as removed.</para>
/// </summary>
internal static class LineDiff
{
    /// <summary>The largest LCS middle (source lines × target lines) diffed line by line.</summary>
    internal const long DefaultMaxCells = 4_000_000;

    /// <summary>The hunks that turn <paramref name="source"/>'s line contents into <paramref name="target"/>'s.</summary>
    public static IReadOnlyList<LineHunk> Hunks(string source, string target)
        => Hunks(source, target, DefaultMaxCells);

    /// <summary><see cref="Hunks(string, string)"/> with the middle's size bound stated (tests lower it).</summary>
    internal static IReadOnlyList<LineHunk> Hunks(string source, string target, long maxCells)
        => Hunks(Split(source).Lines, Split(target).Lines, maxCells);

    /// <summary>The hunks over two line lists, as <see cref="Split"/> gives them.</summary>
    internal static IReadOnlyList<LineHunk> Hunks(IReadOnlyList<string> a, IReadOnlyList<string> b, long maxCells = DefaultMaxCells)
    {
        int n = a.Count, m = b.Count;
        var prefix = 0;
        while (prefix < n && prefix < m && string.Equals(a[prefix], b[prefix], StringComparison.Ordinal))
            prefix++;
        var suffix = 0;
        while (suffix < n - prefix && suffix < m - prefix
               && string.Equals(a[n - 1 - suffix], b[m - 1 - suffix], StringComparison.Ordinal))
            suffix++;

        int p = n - prefix - suffix, q = m - prefix - suffix;
        var hunks = new List<LineHunk>();
        if (p == 0 && q == 0)
            return hunks;
        var cells = (long)(p + 1) * (q + 1);
        if (p == 0 || q == 0 || (long)p * q > maxCells || cells > int.MaxValue)
        {
            hunks.Add(new LineHunk(prefix, prefix + p, Slice(b, prefix, q)));
            return hunks;
        }

        // Intern the middle's lines so the table compares ints.
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var x = new int[p];
        var y = new int[q];
        for (var i = 0; i < p; i++)
            x[i] = Intern(ids, a[prefix + i]);
        for (var j = 0; j < q; j++)
            y[j] = Intern(ids, b[prefix + j]);

        // table[i, j] = LCS(x[i..], y[j..]).
        var w = q + 1;
        var table = new int[(int)cells];
        for (var i = p - 1; i >= 0; i--)
        {
            for (var j = q - 1; j >= 0; j--)
            {
                table[i * w + j] = x[i] == y[j]
                    ? table[(i + 1) * w + j + 1] + 1
                    : Math.Max(table[(i + 1) * w + j], table[i * w + j + 1]);
            }
        }

        // Walk one LCS; every maximal run of unmatched lines between two matches is one hunk, so
        // hunks are separated by at least one kept source line (non-adjacent).
        int si = 0, ti = 0, runSource = -1, runTarget = -1;
        while (si < p || ti < q)
        {
            if (si < p && ti < q && x[si] == y[ti])
            {
                Flush();
                si++;
                ti++;
                continue;
            }

            if (runSource < 0)
            {
                runSource = si;
                runTarget = ti;
            }

            if (ti == q || (si < p && table[(si + 1) * w + ti] >= table[si * w + ti + 1]))
                si++;
            else
                ti++;
        }

        Flush();
        return hunks;

        void Flush()
        {
            if (runSource < 0)
                return;
            hunks.Add(new LineHunk(prefix + runSource, prefix + si, Slice(b, prefix + runTarget, ti - runTarget)));
            runSource = -1;
        }
    }

    /// <summary>
    /// Applies sorted, non-overlapping <paramref name="hunks"/> to <paramref name="source"/>, keeping
    /// every line break the hunks do not replace (see the type's remarks).
    /// </summary>
    public static string Apply(string source, IReadOnlyList<LineHunk> hunks)
    {
        var (lines, breaks) = Split(source);
        var eol = DocumentLineBreak(breaks);
        var contents = new List<string>(lines.Count);
        var terminators = new List<string?>(lines.Count);
        var next = 0;
        foreach (var hunk in hunks)
        {
            if (hunk.Start < next || hunk.End < hunk.Start || hunk.End > lines.Count)
                throw new ArgumentException($"hunk [{hunk.Start}, {hunk.End}) is out of order or out of range for {lines.Count} lines", nameof(hunks));
            Keep(next, hunk.Start);
            for (var j = 0; j < hunk.NewLines.Count; j++)
            {
                contents.Add(hunk.NewLines[j]);
                var last = j == hunk.NewLines.Count - 1;
                terminators.Add(last && hunk.End > hunk.Start && hunk.End - 1 < breaks.Count ? breaks[hunk.End - 1] : null);
            }

            next = hunk.End;
        }

        Keep(next, lines.Count);

        var text = new StringBuilder();
        for (var k = 0; k < contents.Count; k++)
        {
            text.Append(contents[k]);
            if (k < contents.Count - 1)
                text.Append(terminators[k] ?? eol);
        }

        return text.ToString();

        void Keep(int from, int to)
        {
            for (var i = from; i < to; i++)
            {
                contents.Add(lines[i]);
                terminators.Add(i < breaks.Count ? breaks[i] : null);
            }
        }
    }

    /// <summary>
    /// The lines of <paramref name="text"/> and the line breaks between them
    /// (<c>Breaks.Count == Lines.Count − 1</c>; the last line is unterminated).
    /// </summary>
    internal static (List<string> Lines, List<string> Breaks) Split(string text)
    {
        var lines = new List<string>();
        var breaks = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c != '\n' && c != '\r')
                continue;
            lines.Add(text.Substring(start, i - start));
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                breaks.Add("\r\n");
                i++;
            }
            else
            {
                breaks.Add(c == '\n' ? "\n" : "\r");
            }

            start = i + 1;
        }

        lines.Add(text.Substring(start));
        return (lines, breaks);
    }

    /// <summary>The line break <see cref="Apply"/> writes where it inserts one: <paramref name="text"/>'s first, else <c>\n</c>.</summary>
    internal static string DocumentLineBreak(string text) => DocumentLineBreak(Split(text).Breaks);

    private static string DocumentLineBreak(List<string> breaks) => breaks.Count > 0 ? breaks[0] : "\n";

    private static int Intern(Dictionary<string, int> ids, string line)
    {
        if (!ids.TryGetValue(line, out var id))
        {
            id = ids.Count;
            ids[line] = id;
        }

        return id;
    }

    private static string[] Slice(IReadOnlyList<string> lines, int start, int count)
    {
        var slice = new string[count];
        for (var i = 0; i < count; i++)
            slice[i] = lines[start + i];
        return slice;
    }
}
