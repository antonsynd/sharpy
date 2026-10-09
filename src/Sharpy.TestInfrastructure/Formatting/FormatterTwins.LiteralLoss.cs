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

    /// <summary>
    /// Where the error that spends the budget lands within its line (#2273's axis): the shape name and its line,
    /// below <c>MaxErrors - 1</c> lines of <c>x = "abc</c>. At the line end of an unterminated short string (the
    /// <see cref="BudgetShape"/> itself: every line is <c>x = "abc</c>), inside a short string aborting mid-line at
    /// an invalid escape (SPY0004), at an unexpected character inside a replacement field (SPY0015), and at an
    /// indentation error that drops the line whole (SPY0013) — codes measured with <c>sharpyc emit diagnostics</c>
    /// @ 729bf1e7d.
    /// </summary>
    public static readonly IReadOnlyList<(string Shape, string Line)> BudgetStopLines = new[]
    {
        (BudgetShape, "x = \"abc"),
        (BudgetShape + "-midstring", "x = \"\\q\" + 1"),
        (BudgetShape + "-field", "x = f\"{a $ b}\""),
        (BudgetShape + "-indent", "  x = 1"),
    };

    /// <summary>Whether <paramref name="shape"/> is one of <see cref="BudgetStopLines"/>' shapes.</summary>
    public static bool IsBudgetShape(string shape) => BudgetStopLines.Any(b => b.Shape == shape);

    /// <summary>S6b: a stray triple quote above re-pairs every triple of that quote character; a short string holding the triple closes the last run.</summary>
    public const string RepairShape = "repair";

    /// <summary>S6b-comment: the same stray triple, the last run closed by a comment holding the triple (no lexer fact separates it from a document that loses nothing).</summary>
    public const string RepairCommentShape = "repair-comment";

    /// <summary>S6c: a literal's delimiter line dropped whole by error recovery — <c>dropped-</c> and the code that drops it.</summary>
    public const string DroppedShapePrefix = "dropped-";

    /// <summary>
    /// The codes S6c drops a delimiter line at: mixed tabs and spaces, a tab, a width not a multiple of 4, a
    /// dedent to no enclosing width, an unexpected character before the opener, and a short string before each
    /// delimiter that aborts mid-line (an invalid escape — the abort leaves the lexer INSIDE that string).
    /// </summary>
    public static readonly IReadOnlyList<string> DroppedCodes = new[] { "SPY0011", "SPY0012", "SPY0013", "SPY0014", "SPY0015", "SPY0004" };

    /// <summary>A short string that aborts mid-line at SPY0004 (an invalid escape) with the lexer inside it, and the <c>+</c> after it.</summary>
    private const string AbortingShortString = "\"\\q\" + ";

    /// <summary>
    /// S6r: the short-string re-pair whose orphan line aborts right after the re-paired closer — <c>repair-</c> and
    /// the code it aborts at (#2275): the orphan's closing quote is never read as one.
    /// </summary>
    public const string RepairCloseLinePrefix = "repair-";

    /// <summary>
    /// S6x: NO stray — the closer line of the first literal spanning lines gets a tail that aborts there and drops
    /// no quote character: <c>closeline-</c> and the code. Nothing is lost (#2275's direction control).
    /// </summary>
    public const string CloseLinePrefix = "closeline-";

    /// <summary>
    /// One abort on the close line of a literal spanning lines (#2275): the <see cref="Code"/> it reports, the
    /// <see cref="OrphanTail"/> that follows the re-paired triple on the orphan line of the <c>repair-</c> shape
    /// (<c>{o}</c> the quote character other than the literal's, <c>{q}</c> the literal's), and the
    /// <see cref="ControlTail"/> appended after <c> + </c> on a closer line that re-paired nothing.
    /// </summary>
    public sealed record CloseLineCode(string Code, string OrphanTail, string ControlTail)
    {
        /// <summary><see cref="OrphanTail"/> for a literal whose quote character is <paramref name="quote"/>.</summary>
        public string OrphanTailFor(char quote)
        {
            var other = quote == '"' ? '\'' : '"';
            return OrphanTail.Replace("{o}", other.ToString(), StringComparison.Ordinal).Replace("{q}", quote.ToString(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The close-line abort axis (#2275, measured with <c>sharpyc emit diagnostics</c> @ 729bf1e7d): an unexpected
    /// character, a backtick-delimited name left open at its line end, a number with consecutive underscores, a hex
    /// literal with no digits, and an f-string whose format spec runs to the line end.
    /// </summary>
    public static readonly IReadOnlyList<CloseLineCode> CloseLineCodes = new[]
    {
        new CloseLineCode("SPY0015", "${o}", "$"),
        new CloseLineCode("SPY0018", "`{o}", "`x"),
        new CloseLineCode("SPY0007", "1__2{o}", "1__2"),
        new CloseLineCode("SPY0008", "0x{o}", "0x"),
        new CloseLineCode("SPY0022", "f{o} + {q}{x:{q} + str(y)", "f\"{x:"),
    };

    /// <summary>Whether <paramref name="shape"/> is a <see cref="RepairCloseLinePrefix"/> shape (exactly; <see cref="RepairCommentShape"/> shares the prefix and is not one).</summary>
    public static bool IsRepairCloseLineShape(string shape) => CloseLineCodes.Any(c => RepairCloseLinePrefix + c.Code == shape);

    /// <summary>Whether <paramref name="shape"/> is a <see cref="CloseLinePrefix"/> shape.</summary>
    public static bool IsCloseLineShape(string shape) => CloseLineCodes.Any(c => CloseLinePrefix + c.Code == shape);

    /// <summary>
    /// S6o: NO stray — the closer line of the first literal spanning lines gets <c> + </c> and an
    /// <see cref="OwnQuoteTails"/> tail, a literal that aborts INSIDE itself: <c>closeline-own-</c> and the tail's
    /// name. The given-up text holds the aborted literal's OWN delimiter, which is no orphan — nothing is lost (#2280).
    /// </summary>
    public const string CloseLineOwnPrefix = "closeline-own-";

    /// <summary>
    /// S6o: the stray triple, the last line <c>_ = {o}{triple} + </c>, the tail, and the orphan's closing quote: the
    /// re-paired closer line aborts inside the tail, and the orphan's closing quote sits in the text recovery drops
    /// AFTER the tail's own delimiter (#2280's positive control: a literal line IS lost).
    /// </summary>
    public const string RepairOwnPrefix = "repair-own-";

    /// <summary>
    /// One close-line tail whose abort lands inside a literal (or a backtick name, or a comment) of its own (#2280): its
    /// <see cref="Name"/> and its <see cref="Tail"/>, spelled with <c>{q}</c> (the multi-line literal's quote character)
    /// and <c>{o}</c> (the other one) as <see cref="CloseLineCode.OrphanTail"/> is. <see cref="WalkCannotPair"/> marks a
    /// tail whose given-up text holds a quote R-FV's close-line walk cannot pair (plan-f92797 Design 4's pinned limits:
    /// an unterminated backtick name, a quote after <c>#</c>, the f-string's own quote in its spec) — its
    /// <c>closeline-own-</c> document keeps the fact set, a known limit of #2274 (lead ruling L2).
    /// </summary>
    public sealed record OwnQuoteTail(string Name, string Tail, bool WalkCannotPair = false)
    {
        /// <summary><see cref="Tail"/> next to a literal whose quote character is <paramref name="quote"/>.</summary>
        public string TailFor(char quote) => WithQuotes(Tail, quote);
    }

    /// <summary>
    /// The own-quote axis of #2280 (codes measured with <c>sharpyc emit diagnostics</c> on plan-f92797's B documents
    /// @ be1c16fcf): an invalid escape in a short string of either quote character and in a bytes literal (SPY0004), an
    /// invalid conversion in an f- and a t-string (SPY0030), a lone <c>}</c> in an f-string (SPY0021), the f-string's
    /// own quote inside its format spec (SPY0022), a backtick name left open whose text holds the other quote (SPY0018),
    /// and an unexpected character followed by a comment holding it (SPY0015).
    /// </summary>
    public static readonly IReadOnlyList<OwnQuoteTail> OwnQuoteTails = new[]
    {
        new OwnQuoteTail("dq", "{q}\\q{q}"),
        new OwnQuoteTail("oq", "{o}\\q{o}"),
        new OwnQuoteTail("bq", "b{q}\\q{q}"),
        new OwnQuoteTail("fconv", "f{q}{x!q}{q}"),
        new OwnQuoteTail("tconv", "t{q}{x!q}{q}"),
        new OwnQuoteTail("fbrace", "f{q}}{q}"),
        new OwnQuoteTail("fspec", "f{q}{x:{q}>}{q}", WalkCannotPair: true),
        new OwnQuoteTail("bt", "`it{o}s", WalkCannotPair: true),
        new OwnQuoteTail("cmt", "$  # it{o}s", WalkCannotPair: true),
    };

    /// <summary>Whether <paramref name="shape"/> is a <see cref="CloseLineOwnPrefix"/> or <see cref="RepairOwnPrefix"/> shape (exactly).</summary>
    public static bool IsOwnQuoteShape(string shape) => IsCloseLineOwnShape(shape) || OwnQuoteTails.Any(t => RepairOwnPrefix + t.Name == shape);

    /// <summary>Whether <paramref name="shape"/> is a <see cref="CloseLineOwnPrefix"/> shape (exactly).</summary>
    public static bool IsCloseLineOwnShape(string shape) => OwnQuoteTails.Any(t => CloseLineOwnPrefix + t.Name == shape);

    /// <summary>Whether <paramref name="shape"/> is the <see cref="CloseLineOwnPrefix"/> shape of a tail the walk cannot pair (<see cref="OwnQuoteTail.WalkCannotPair"/>).</summary>
    public static bool IsCloseLineOwnLimitShape(string shape) => OwnQuoteTails.Any(t => t.WalkCannotPair && CloseLineOwnPrefix + t.Name == shape);

    /// <summary><paramref name="spelling"/> with <c>{q}</c> replaced by <paramref name="quote"/> and <c>{o}</c> by the other quote character.</summary>
    private static string WithQuotes(string spelling, char quote)
    {
        var other = quote == '"' ? '\'' : '"';
        return spelling.Replace("{o}", other.ToString(), StringComparison.Ordinal).Replace("{q}", quote.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Every shape <see cref="LiteralLossShapes"/> builds.</summary>
    public static readonly IReadOnlyList<string> LiteralLossShapeNames =
        BudgetStopLines.Select(b => b.Shape).Concat(new[] { RepairShape, RepairCommentShape }).Concat(DroppedCodes.Select(c => DroppedShapePrefix + c))
            .Concat(CloseLineCodes.Select(c => RepairCloseLinePrefix + c.Code))
            .Concat(CloseLineCodes.Select(c => CloseLinePrefix + c.Code))
            .Concat(OwnQuoteTails.Select(t => CloseLineOwnPrefix + t.Name))
            .Concat(OwnQuoteTails.Select(t => RepairOwnPrefix + t.Name)).ToArray();

    /// <summary>
    /// The documents in which a CLEAN parent <paramref name="q"/> loses a literal that can span lines WITHOUT
    /// the lexer aborting inside its read (#2271) — the one recipe the route-parity sweep's S6 and the lexer's
    /// literal-state tests derive their documents from. Each keeps Q's text and line breaks byte-for-byte
    /// outside the lines it inserts or rewrites:
    /// <list type="bullet">
    /// <item><see cref="BudgetShape"/> and the other <see cref="BudgetStopLines"/> shapes — for EVERY Q (one without
    /// a literal spanning lines is the direction control): <see cref="SLexer.MaxErrors"/> lines inserted above line 0,
    /// <c>MaxErrors - 1</c> of <c>x = "abc</c> (an unterminated short string each) and the shape's own last line;</item>
    /// <item><see cref="RepairShape"/> and <see cref="RepairCommentShape"/> — when Q has a literal spanning
    /// lines: a line holding only the triple of its FIRST such literal's quote character inserted as line 0,
    /// and as the last line <c>_ = '"""'</c> (the triple inside a short string of the other quote character)
    /// or <c># """</c>, so the count of that triple stays even and no read is left open;</item>
    /// <item><c>repair-SPY00NN</c> — the same stray, the last line <c>_ = '"""</c> followed by
    /// <see cref="CloseLineCode.OrphanTail"/> in place of the closing quote, so the line aborts right after the
    /// re-paired closer and the orphan's closing quote sits in the text recovery drops (#2275);</item>
    /// <item><c>closeline-SPY00NN</c> — no stray: the closer line of Q's FIRST literal spanning lines gets
    /// <c> + </c> and <see cref="CloseLineCode.ControlTail"/> appended at its end, which aborts and drops no quote
    /// character — nothing is lost (#2275's direction control). Not built when that line holds a <c>#</c> or ends in
    /// a <c>\</c> after the closer (the tail would be a comment or a continuation);</item>
    /// <item><c>dropped-SPY00NN</c> — for each literal spanning lines whose closer line is a delimiter line
    /// (its first non-whitespace character is the literal's quote character): the leading whitespace of the
    /// opener and closer line replaced by the opener's + 2 spaces (SPY0013), a tab (SPY0012), a tab and a
    /// space (SPY0011), and — only when <paramref name="includeMismatch"/> (the wide twin, every level a
    /// multiple of 8) and the opener is at width ≥ 8 — 4 spaces (SPY0014); a <c>$</c> inserted right
    /// before the opener's prefix (SPY0015, the rest of the line dropped mid-line, nothing rewritten); and
    /// <c>"\q" + </c> inserted there AND before the closer's delimiter (SPY0004, an invalid escape: the short
    /// string aborts with the lexer inside it, so the dropped rest of each line starts mid-literal — on the
    /// closer line too, since a bare closer line dropped whole at an indentation error is itself a dropped
    /// opener; abort-free when the closer sits at a width the content left on the indent stack: a module-level
    /// literal, or one whose content is at the closer's width).</item>
    /// <item><c>closeline-own-NAME</c> and <c>repair-own-NAME</c> (#2280), appended after every shape above — the
    /// <see cref="OwnQuoteTails"/> axis: the <c>closeline-</c> document with an own-quote tail in place of the control
    /// tail (same exclusions; nothing is lost), and the <c>repair-</c> document whose last line is
    /// <c>_ = {o}{triple} + </c>, the tail and the orphan's closing quote (a literal line is lost).</item>
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
        foreach (var (shape, line) in BudgetStopLines)
        {
            var lines = Enumerable.Repeat("x = \"abc", maxErrors - 1).Append(line).ToArray();
            shapes.Add(new LiteralLossShape(shape, Prepend(q, lines), firstKind, maxErrors, Array.Empty<int>()));
        }

        if (multiLine.Count > 0)
        {
            var quote = QuoteCharacter(q, multiLine[0].Start);
            var triple = new string(quote, 3);
            var other = quote == '"' ? '\'' : '"';
            var stray = Prepend(q, triple);
            shapes.Add(new LiteralLossShape(RepairShape, Append(stray, $"_ = {other}{triple}{other}"), firstKind, 1, Array.Empty<int>()));
            shapes.Add(new LiteralLossShape(RepairCommentShape, Append(stray, $"# {triple}"), firstKind, 1, Array.Empty<int>()));
            foreach (var code in CloseLineCodes)
            {
                shapes.Add(new LiteralLossShape(RepairCloseLinePrefix + code.Code,
                    Append(stray, $"_ = {other}{triple}{code.OrphanTailFor(quote)}"), firstKind, 1, Array.Empty<int>()));
            }

            var closerLine = LineOf(lineStarts, multiLine[0].End - 1);
            var closerEnd = LineContentEnd(q, lineStarts, closerLine);
            var afterCloser = q.AsSpan(multiLine[0].End, closerEnd - multiLine[0].End);
            if (afterCloser.IndexOf('#') < 0 && !afterCloser.TrimEnd(" \t").EndsWith("\\"))
            {
                foreach (var code in CloseLineCodes)
                    shapes.Add(new LiteralLossShape(CloseLinePrefix + code.Code, q.Insert(closerEnd, " + " + code.ControlTail), firstKind, 0, Array.Empty<int>()));
            }
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
            var closerDelimiter = lineStarts[closer] + LeadingWhitespace(q, lineStarts, closer).Length;
            shapes.Add(new LiteralLossShape(DroppedShapePrefix + "SPY0004",
                q.Insert(closerDelimiter, AbortingShortString).Insert(start, AbortingShortString), kind, 0, Array.Empty<int>()));
        }

        // The own-quote axis (#2280), appended after every shape above so their texts and order are untouched.
        if (multiLine.Count > 0)
        {
            var quote = QuoteCharacter(q, multiLine[0].Start);
            var triple = new string(quote, 3);
            var other = quote == '"' ? '\'' : '"';
            var closerLine = LineOf(lineStarts, multiLine[0].End - 1);
            var closerEnd = LineContentEnd(q, lineStarts, closerLine);
            var afterCloser = q.AsSpan(multiLine[0].End, closerEnd - multiLine[0].End);
            if (afterCloser.IndexOf('#') < 0 && !afterCloser.TrimEnd(" \t").EndsWith("\\"))
            {
                foreach (var tail in OwnQuoteTails)
                    shapes.Add(new LiteralLossShape(CloseLineOwnPrefix + tail.Name, q.Insert(closerEnd, " + " + tail.TailFor(quote)), firstKind, 0, Array.Empty<int>()));
            }

            var stray = Prepend(q, triple);
            foreach (var tail in OwnQuoteTails)
            {
                shapes.Add(new LiteralLossShape(RepairOwnPrefix + tail.Name,
                    Append(stray, $"_ = {other}{triple} + {tail.TailFor(quote)}{other}"), firstKind, 1, Array.Empty<int>()));
            }
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
