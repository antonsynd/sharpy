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
}
