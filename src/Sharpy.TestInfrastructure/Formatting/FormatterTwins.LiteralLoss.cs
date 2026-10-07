using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Lexer;
using SLexer = Sharpy.Compiler.Lexer.Lexer;

namespace Sharpy.TestInfrastructure.Formatting;

/// <summary>
/// One document of <see cref="FormatterTwins.LiteralLossShapes"/>: its <see cref="Shape"/>, its text, the
/// kind of the literal it loses (<see cref="FormatterTwins.LiteralKind"/>; null when the parent has no
/// literal spanning lines), how many lines were inserted ABOVE the parent's line 0 (every parent line
/// <c>l</c> is line <c>l + LineShift</c> of <see cref="Text"/>), and the 0-based lines whose leading
/// whitespace the shape rewrote (their block depth may legitimately change — the repair).
/// </summary>
public sealed record LiteralLossShape(string Shape, string Text, string? Kind, int LineShift, IReadOnlyList<int> ReindentedLines);

public static partial class FormatterTwins
{
    // ================================================================
    // Literal loss without a lexer abort (P22f, #2271)
    // ================================================================

    /// <summary>S6a: the lexer's error budget is spent above the parent, which is left unread.</summary>
    public const string BudgetShape = "budget";

    /// <summary>S6b: a stray triple quote above re-pairs every triple of that quote character; a short string holding the triple closes the last run.</summary>
    public const string RepairShape = "repair";

    /// <summary>S6b-comment: the same stray triple, the last run closed by a comment holding the triple (no lexer fact separates it from a document that loses nothing).</summary>
    public const string RepairCommentShape = "repair-comment";

    /// <summary>S6c: a literal's delimiter line dropped whole by error recovery — <c>dropped-</c> and the code that drops it.</summary>
    public const string DroppedShapePrefix = "dropped-";

    /// <summary>The codes S6c drops a delimiter line at: mixed tabs and spaces, a tab, a width not a multiple of 4, a dedent to no enclosing width, an unexpected character before the opener.</summary>
    public static readonly IReadOnlyList<string> DroppedCodes = new[] { "SPY0011", "SPY0012", "SPY0013", "SPY0014", "SPY0015" };

    /// <summary>Every shape <see cref="LiteralLossShapes"/> builds.</summary>
    public static readonly IReadOnlyList<string> LiteralLossShapeNames =
        new[] { BudgetShape, RepairShape, RepairCommentShape }.Concat(DroppedCodes.Select(c => DroppedShapePrefix + c)).ToArray();

    /// <summary>
    /// The documents in which a CLEAN parent <paramref name="q"/> loses a literal that can span lines WITHOUT
    /// the lexer aborting inside its read (#2271) — the one recipe the route-parity sweep's S6 and the lexer's
    /// literal-state tests derive their documents from. Each keeps Q's text and line breaks byte-for-byte
    /// outside the lines it inserts or rewrites:
    /// <list type="bullet">
    /// <item><see cref="BudgetShape"/> — for EVERY Q (one without a literal spanning lines is the direction
    /// control): <see cref="SLexer.MaxErrors"/> lines of <c>x = "abc</c> (an unterminated short string each)
    /// inserted above line 0;</item>
    /// <item><see cref="RepairShape"/> and <see cref="RepairCommentShape"/> — when Q has a literal spanning
    /// lines: a line holding only the triple of its FIRST such literal's quote character inserted as line 0,
    /// and as the last line <c>_ = '"""'</c> (the triple inside a short string of the other quote character)
    /// or <c># """</c>, so the count of that triple stays even and no read is left open;</item>
    /// <item><c>dropped-SPY00NN</c> — for each literal spanning lines whose closer line is a delimiter line
    /// (its first non-whitespace character is the literal's quote character): the leading whitespace of the
    /// opener and closer line replaced by the opener's + 2 spaces (SPY0013), a tab (SPY0012), a tab and a
    /// space (SPY0011), and — only when <paramref name="includeMismatch"/> (the wide twin, every level a
    /// multiple of 8) and the opener is at width ≥ 8 — 4 spaces (SPY0014); and a <c>$</c> inserted right
    /// before the opener's prefix (SPY0015, the rest of the line dropped mid-line, nothing rewritten).</item>
    /// </list>
    /// Which documents are abort-free is OBSERVED by the consumers, never assumed.
    /// </summary>
    public static IReadOnlyList<LiteralLossShape> LiteralLossShapes(string q, bool includeMismatch)
    {
        var tokens = Lex(q, out _);
        var lineStarts = LineStarts(q);
        var multiLine = LiteralSpans.Of(tokens)
            .Where(s => LineOf(lineStarts, s.Start) < LineOf(lineStarts, s.End - 1))
            .ToList();
        var firstKind = multiLine.Count > 0 ? LiteralKind(q, multiLine[0].Start) : null;
        var shapes = new List<LiteralLossShape>();

        var maxErrors = new SLexer("").MaxErrors;
        shapes.Add(new LiteralLossShape(BudgetShape, Prepend(q, Enumerable.Repeat("x = \"abc", maxErrors).ToArray()), firstKind, maxErrors, Array.Empty<int>()));

        if (multiLine.Count > 0)
        {
            var quote = QuoteCharacter(q, multiLine[0].Start);
            var triple = new string(quote, 3);
            var other = quote == '"' ? '\'' : '"';
            var stray = Prepend(q, triple);
            shapes.Add(new LiteralLossShape(RepairShape, Append(stray, $"_ = {other}{triple}{other}"), firstKind, 1, Array.Empty<int>()));
            shapes.Add(new LiteralLossShape(RepairCommentShape, Append(stray, $"# {triple}"), firstKind, 1, Array.Empty<int>()));
        }

        foreach (var (start, end) in multiLine)
        {
            var opener = LineOf(lineStarts, start) - 1;
            var closer = LineOf(lineStarts, end - 1) - 1;
            var closerText = q.AsSpan(lineStarts[closer]).TrimStart(" \t");
            if (closerText.Length == 0 || closerText[0] != QuoteCharacter(q, start))
                continue;

            var kind = LiteralKind(q, start);
            var indent = LeadingWhitespace(q, lineStarts, opener);
            var lines = new[] { opener, closer };
            shapes.Add(new LiteralLossShape(DroppedShapePrefix + "SPY0013", Reindent(q, lines, indent + "  "), kind, 0, lines));
            shapes.Add(new LiteralLossShape(DroppedShapePrefix + "SPY0012", Reindent(q, lines, "\t"), kind, 0, lines));
            shapes.Add(new LiteralLossShape(DroppedShapePrefix + "SPY0011", Reindent(q, lines, "\t "), kind, 0, lines));
            if (includeMismatch && indent.Length >= 8)
                shapes.Add(new LiteralLossShape(DroppedShapePrefix + "SPY0014", Reindent(q, lines, "    "), kind, 0, lines));
            shapes.Add(new LiteralLossShape(DroppedShapePrefix + "SPY0015", q.Insert(start, "$"), kind, 0, Array.Empty<int>()));
        }

        return shapes;
    }

    /// <summary><paramref name="lines"/>, each ended by <paramref name="q"/>'s line break, inserted above its line 0.</summary>
    public static string Prepend(string q, params string[] lines)
    {
        var lineBreak = LineDiff.DocumentLineBreak(q);
        return string.Concat(lines.Select(l => l + lineBreak)) + q;
    }

    /// <summary><paramref name="line"/> appended as the last line of <paramref name="q"/>, ended by Q's line break (one is added before it when Q has no final line break).</summary>
    public static string Append(string q, string line)
    {
        var lineBreak = LineDiff.DocumentLineBreak(q);
        var separator = q.Length == 0 || q[^1] is '\n' or '\r' ? "" : lineBreak;
        return q + separator + line + lineBreak;
    }

    /// <summary><paramref name="q"/> with the leading spaces and tabs of each 0-based line in <paramref name="lines"/> replaced by <paramref name="whitespace"/>.</summary>
    public static string Reindent(string q, IReadOnlyCollection<int> lines, string whitespace)
    {
        var lineStarts = LineStarts(q);
        var text = new System.Text.StringBuilder(q.Length + (lines.Count * whitespace.Length));
        var at = 0;
        foreach (var line in lines.Distinct().Order())
        {
            var start = lineStarts[line];
            var end = start;
            while (end < q.Length && q[end] is ' ' or '\t')
                end++;
            text.Append(q, at, start - at).Append(whitespace);
            at = end;
        }

        return text.Append(q, at, q.Length - at).ToString();
    }

    /// <summary>
    /// A literal's kind: its prefix letters, lower-cased and sorted (<c>rd</c> is <c>dr</c>), and <c>"""</c>
    /// when triple-quoted (either quote character), <c>"</c> otherwise. <paramref name="start"/> is the
    /// literal's first character (its prefix, else its quote).
    /// </summary>
    public static string LiteralKind(string text, int start)
    {
        var i = start;
        while (i < text.Length && char.IsLetter(text[i]))
            i++;
        var prefix = new string(text[start..i].ToLowerInvariant().OrderBy(c => c).ToArray());
        var triple = i + 2 < text.Length && text[i + 1] == text[i] && text[i + 2] == text[i];
        return prefix + (triple ? "\"\"\"" : "\"");
    }

    /// <summary>The quote character of the literal starting at <paramref name="start"/> (past its prefix letters).</summary>
    public static char QuoteCharacter(string text, int start)
    {
        var i = start;
        while (i < text.Length && char.IsLetter(text[i]))
            i++;
        return text[i];
    }

    /// <summary>The leading spaces and tabs of the 0-based <paramref name="line"/>.</summary>
    private static string LeadingWhitespace(string text, List<int> lineStarts, int line)
    {
        var start = lineStarts[line];
        var end = start;
        while (end < text.Length && text[end] is ' ' or '\t')
            end++;
        return text[start..end];
    }
}
