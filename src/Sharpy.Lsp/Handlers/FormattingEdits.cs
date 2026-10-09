using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Formatting;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// The funnel from <see cref="LineHunk"/>s to LSP text edits (P22e decision 4, #2168): one
/// <see cref="TextEdit"/> per hunk, non-overlapping, in document order, whose application is exactly
/// <see cref="LineDiff.Apply"/> — an unchanged line keeps its own line break, a replaced line keeps
/// the break after it, and every break an edit writes is the document's own (so a CRLF document stays
/// CRLF). Positions are UTF-16 code units, as both LSP and <see cref="string.Length"/> count them.
/// </summary>
internal static class FormattingEdits
{
    /// <summary>
    /// The edits that apply <paramref name="hunks"/> (sorted, non-overlapping, as <see cref="LineDiff.Hunks(string, string)"/>
    /// returns them) to <paramref name="source"/>:
    /// <list type="bullet">
    /// <item>replace <c>[a, b)</c> with N lines: <c>(a,0)–(b−1,len)</c> ← <c>join(N, eol)</c> — a 1:1 line
    /// replacement is <c>(i,0)–(i,len)</c> ← the bare line;</item>
    /// <item>delete <c>[a, b)</c>: <c>(a,0)–(b,0)</c> ← <c>""</c>; through the last line (<c>b == n</c>), from the
    /// end of line <c>a − 1</c> to the end of the document;</item>
    /// <item>insert N lines before <c>a &lt; n</c>: <c>(a,0)–(a,0)</c> ← <c>join(N, eol) + eol</c>; at
    /// <c>a == n</c>, at the end of line <c>n − 1</c> ← <c>eol + join(N, eol)</c>.</item>
    /// </list>
    /// </summary>
    public static List<TextEdit> ToTextEdits(string source, IReadOnlyList<LineHunk> hunks)
    {
        var lines = LineDiff.Split(source).Lines;
        var n = lines.Count;
        var eol = LineDiff.DocumentLineBreak(source);
        var edits = new List<TextEdit>(hunks.Count);
        foreach (var hunk in hunks)
        {
            int a = hunk.Start, b = hunk.End;
            var newText = string.Join(eol, hunk.NewLines);
            LspRange range;
            if (b > a && hunk.NewLines.Count > 0)
            {
                range = new LspRange(new Position(a, 0), new Position(b - 1, lines[b - 1].Length));
            }
            else if (b > a)
            {
                range = b < n
                    ? new LspRange(new Position(a, 0), new Position(b, 0))
                    : new LspRange(a == 0 ? new Position(0, 0) : EndOf(a - 1), EndOf(n - 1));
            }
            else if (a < n)
            {
                range = new LspRange(new Position(a, 0), new Position(a, 0));
                newText += eol;
            }
            else
            {
                range = new LspRange(EndOf(n - 1), EndOf(n - 1));
                newText = eol + newText;
            }

            edits.Add(new TextEdit { Range = range, NewText = newText });
        }

        return edits;

        Position EndOf(int line) => new(line, lines[line].Length);
    }

    /// <summary>
    /// The funnel's second arm (P22e decision 8, #2168): the edits of an indent-only candidate — the
    /// on-type alignment or the full and range fallbacks — when the text they produce passes the check,
    /// else none. When the lexer lost a literal that can span lines (decision 7,
    /// <see cref="Compiler.Lexer.Lexer.LiteralStateUnknown"/>) which lines are string content is unknown,
    /// so nothing is edited and nothing else runs; nor when a line is indented with whitespace other than spaces and tabs
    /// (<see cref="IndentMap.HasOtherWhitespace"/>, lead ruling L12: the map has no structure to give such a line). Otherwise the applied text is checked: when the
    /// source parses, by the SPY0912 net (<see cref="FormatterService.CheckApplied"/>); when it does
    /// not, by <see cref="FormattingFallback.IndentOnlyPreserved"/>.
    /// </summary>
    public static List<TextEdit> CheckedIndentOnly(string source, IReadOnlyList<TextEdit> candidate)
    {
        if (candidate.Count == 0)
            return new List<TextEdit>();
        var map = IndentationService.BuildIndentMap(source);
        if (map.LiteralStateUnknown || map.HasOtherWhitespace)
            return new List<TextEdit>();

        var applied = Apply(source, candidate);
        if (applied == null)
            return new List<TextEdit>();

        var declined = FormatterService.CheckApplied(source, applied, out var sourceParses);
        var preserved = sourceParses ? declined == null : FormattingFallback.IndentOnlyPreserved(source, applied);
        return preserved ? candidate.ToList() : new List<TextEdit>();
    }

    /// <summary>
    /// <paramref name="edits"/> applied to <paramref name="source"/> the way the client applies them — lines
    /// ended by <c>\r\n</c>, <c>\n</c> or a lone <c>\r</c> (<see cref="LineDiff.Split"/>), a position within its
    /// line's content; null when an edit lies outside the document or two overlap (never for a candidate this
    /// server built — refused, not guessed at).
    /// </summary>
    private static string? Apply(string source, IReadOnlyList<TextEdit> edits)
    {
        var (lines, breaks) = LineDiff.Split(source);
        var lineStarts = new int[lines.Count];
        for (var i = 1; i < lines.Count; i++)
            lineStarts[i] = lineStarts[i - 1] + lines[i - 1].Length + breaks[i - 1].Length;

        var spans = new List<(int Start, int End, string NewText)>(edits.Count);
        foreach (var edit in edits)
        {
            var start = Offset(edit.Range.Start);
            var end = Offset(edit.Range.End);
            if (start < 0 || end < start)
                return null;
            spans.Add((start, end, edit.NewText ?? string.Empty));
        }

        spans.Sort((x, y) => x.Start != y.Start ? x.Start.CompareTo(y.Start) : x.End.CompareTo(y.End));
        var sb = new StringBuilder(source.Length);
        var cursor = 0;
        foreach (var (start, end, newText) in spans)
        {
            if (start < cursor)
                return null;
            sb.Append(source, cursor, start - cursor).Append(newText);
            cursor = end;
        }

        return sb.Append(source, cursor, source.Length - cursor).ToString();

        int Offset(Position position)
        {
            if (position.Line < 0 || position.Line >= lines.Count
                || position.Character < 0 || position.Character > lines[position.Line].Length)
                return -1;
            return lineStarts[position.Line] + position.Character;
        }
    }
}
