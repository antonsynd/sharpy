using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Lexer;

namespace Sharpy.TestInfrastructure.Formatting;

/// <summary>
/// One document of <see cref="FormatterTwins.ContinuationShapes"/>: its <see cref="Shape"/>, its text, and the
/// 0-based line <see cref="InsertedAt"/> the shape injected — Q's line <c>l</c> is line <c>l</c> of
/// <see cref="Text"/> above it and line <c>l + 1</c> at or below it — and whether the injected line sits at the
/// block OPENER's width (<see cref="AtOpenerWidth"/>; at the body's width otherwise). The line after the injected one
/// is Q's first body line of its first block (re-indented by <c>recovery-misindented</c>; untouched otherwise).
/// </summary>
public sealed record ContinuationShape(string Shape, string Text, int InsertedAt, bool AtOpenerWidth, int InsertedCount = 1, int? SecondTarget = null)
{
    /// <summary>The 0-based line of <see cref="Text"/> that holds Q's 0-based <paramref name="qLine"/>: <see cref="InsertedCount"/> lines are injected at <see cref="InsertedAt"/>.</summary>
    public int DocLineOf(int qLine) => qLine < InsertedAt ? qLine : qLine + InsertedCount;
}

public static partial class FormatterTwins
{
    // ================================================================
    // Continuation lines (P22h, #2279)
    // ================================================================

    /// <summary>A real bracket left open to the end of the source (A1): every later line is the bracket's continuation to the lexer.</summary>
    public const string BracketEofShape = "bracket-eof";

    /// <summary>A bracket left open before an unterminated short string (A3's mechanism): the abort leaves it open at the dropped line's END.</summary>
    public const string BracketStringShape = "bracket-string";

    /// <summary>A bracket left open before an unterminated backtick name (A4's mechanism, SPY0018).</summary>
    public const string BracketBacktickShape = "bracket-backtick";

    /// <summary>A bracket opened on a line recovery drops (SPY0015 after the opener, A5's mechanism).</summary>
    public const string BracketDroppedShape = "bracket-dropped";

    /// <summary>An erroring line with no bracket (SPY0015): the line after it is read as the dropped line's continuation (#2279 seam 2), here at its block's width.</summary>
    public const string RecoveryShape = "recovery";

    /// <summary>The <see cref="RecoveryShape"/> line with the line after it misindented by 2 spaces (A6): the repair that line needs is the one #2279's seam 2 loses.</summary>
    public const string RecoveryMisindentedShape = "recovery-misindented";

    /// <summary>
    /// The prefix of the <c>recovery-header-*</c> shapes (P22h lead ruling L8, the standalone verifier's 1a/1b): a block
    /// header that errors before its colon is read (<c>if _($):</c>) injected at the first block opener's width, the line
    /// after it — Q's first body line — re-indented deeper than the header by 2, 4 or 6 spaces or one tab
    /// (<see cref="RecoveryHeaderShapes"/>). The dropped line could open a block, so the line's level is unknown and no
    /// route may move it: Q's layout cannot judge that (it has no such block), so these shapes are judged by their own
    /// oracle — the target line byte-identical.
    /// </summary>
    public const string RecoveryHeaderPrefix = "recovery-header-";

    /// <summary>
    /// The <c>recovery-header-*</c> shapes, the header each injects and the whitespace each adds after the header's own
    /// leading whitespace on the line after it. <c>-string</c> and <c>-backtick</c> (lead ruling L11): a header whose colon
    /// lies inside an unterminated short string or backtick name — the lexer cannot read the line's end.
    /// </summary>
    public static readonly IReadOnlyList<(string Shape, string Header, string Deeper)> RecoveryHeaderShapes = new[]
    {
        (RecoveryHeaderPrefix + "2", "if _($):", "  "),
        (RecoveryHeaderPrefix + "4", "if _($):", "    "),
        (RecoveryHeaderPrefix + "6", "if _($):", "      "),
        (RecoveryHeaderPrefix + "tab", "if _($):", "\t"),
        (RecoveryHeaderPrefix + "string", "if _ == \"a:", "  "),
        (RecoveryHeaderPrefix + "backtick", "if `a:", "  "),
    };

    /// <summary>
    /// A putative body nested in a putative body (lead ruling L11): <c>if _($):</c> at the opener's width, an inner
    /// <c>if _($):</c> as its first body line (+4), a line at +4 below that, then Q's first body line dedented to the
    /// outer body (+4) — below the inner header's width, still inside the outer header's putative body.
    /// </summary>
    public const string RecoveryHeaderNestedShape = RecoveryHeaderPrefix + "nested";

    /// <summary>
    /// The second verifier's L6-SIB as a shape (lead ruling L11): <c>if _ == "a:</c> at the opener's width and Q's first
    /// TWO body lines (the first a one-line statement, the second the next code line at the same width) re-indented 6
    /// deeper — a width the lexer drops, so the depth judgment exempts the second line and only the putative body keeps it
    /// in place. Built only when Q has such a pair; its line routes are requested on both lines
    /// (<see cref="ContinuationShape.SecondTarget"/>).
    /// </summary>
    public const string RecoveryHeaderSiblingShape = RecoveryHeaderPrefix + "sibling";

    /// <summary>Whether <paramref name="shape"/> is a <c>recovery-header-*</c> shape (<see cref="RecoveryHeaderPrefix"/>).</summary>
    public static bool IsRecoveryHeaderShape(string shape) => shape.StartsWith(RecoveryHeaderPrefix, StringComparison.Ordinal);

    /// <summary>
    /// The injected line of each shape (spellings of plan-f92797 Design 5) and whether it sits at the block opener's
    /// width. Each is a statement a user leaves unfinished; the bracket shapes leave a bracket the lexer never sees
    /// closed. The three ABANDONED brackets (an abort on their line resets the lexer's bracket depth) sit at the
    /// opener's width (lead ruling L3): the body line below is then one level deeper than the injected line — #2279's
    /// A3 shape, where aligning that continuation line to the bracket's level moves it out of its block. At the body's
    /// width the alignment would move nothing.
    /// </summary>
    private static readonly IReadOnlyList<(string Shape, string Line, bool AtOpenerWidth)> ContinuationLines = new[]
    {
        (BracketEofShape, "_ = [1,", false),
        (BracketStringShape, "_ = foo(\"abc,", true),
        (BracketBacktickShape, "_ = foo(`x,", true),
        (BracketDroppedShape, "_ = foo($,", true),
        (RecoveryShape, "_ = $", false),
        (RecoveryMisindentedShape, "_ = $", false),
    };

    /// <summary>
    /// Every shape <see cref="ContinuationShapes"/> must build when Q has a block — the census's universe, spelled
    /// from the constants rather than read off <c>ContinuationLines</c>, so a shape the builder stops building is a
    /// missing floor instead of a missing name.
    /// </summary>
    public static readonly IReadOnlyList<string> ContinuationShapeNames = new[]
    {
        BracketEofShape, BracketStringShape, BracketBacktickShape, BracketDroppedShape, RecoveryShape, RecoveryMisindentedShape,
        RecoveryHeaderPrefix + "2", RecoveryHeaderPrefix + "4", RecoveryHeaderPrefix + "6", RecoveryHeaderPrefix + "tab",
        RecoveryHeaderPrefix + "string", RecoveryHeaderPrefix + "backtick", RecoveryHeaderNestedShape, RecoveryHeaderSiblingShape,
    };

    /// <summary>The spaces <see cref="RecoveryMisindentedShape"/> adds to the line after its injected line: a width on no level of a 4- or 8-space document.</summary>
    public const string MisindentBy = "  ";

    /// <summary>
    /// The continuation documents of a CLEAN parent <paramref name="q"/> (#2279) — the one recipe the route-parity
    /// sweep's S7 derives its documents from. Each shape's line is injected directly above the line holding the
    /// first token of the body of Q's first block opener (a <c>Colon</c> followed by a <c>Newline</c>) — after the
    /// opener and any comment or blank lines below it, so the line after the injection is the body's first statement
    /// on every twin — at that body line's own leading whitespace, or at the leading whitespace of the opener's first
    /// line for a shape at the opener's width; ended by Q's line break.
    /// <see cref="RecoveryMisindentedShape"/> also re-indents that body line by <see cref="MisindentBy"/>; the
    /// <c>recovery-header-*</c> shapes inject <c>if _($):</c> at the opener's width and re-indent the body line deeper
    /// than it (<see cref="RecoveryHeaderShapes"/>). Q's text
    /// and line breaks are kept byte-for-byte outside the injected line (and the re-indented one). None is built
    /// when Q has no block.
    /// </summary>
    public static IReadOnlyList<ContinuationShape> ContinuationShapes(string q)
    {
        var tokens = Lex(q, out _);
        var (opener, body) = FirstBlock(tokens);
        if (body < 0)
            return Array.Empty<ContinuationShape>();

        var lineStarts = LineStarts(q);
        var indent = LeadingWhitespace(q, lineStarts, body);
        var openerIndent = LeadingWhitespace(q, lineStarts, opener);
        var injectAt = lineStarts[body];
        var lineBreak = LineDiff.DocumentLineBreak(q);
        var shapes = new List<ContinuationShape>(ContinuationLines.Count);
        foreach (var (shape, line, atOpener) in ContinuationLines)
        {
            var parent = shape == RecoveryMisindentedShape ? Reindent(q, new[] { body }, indent + MisindentBy) : q;
            shapes.Add(new ContinuationShape(shape, parent.Insert(injectAt, (atOpener ? openerIndent : indent) + line + lineBreak), body, atOpener));
        }

        foreach (var (shape, header, deeper) in RecoveryHeaderShapes)
        {
            var parent = Reindent(q, new[] { body }, openerIndent + deeper);
            shapes.Add(new ContinuationShape(shape, parent.Insert(injectAt, openerIndent + header + lineBreak), body, AtOpenerWidth: true));
        }

        if (SiblingOf(tokens, q, lineStarts, body, indent) is { } sibling)
        {
            var parent = Reindent(q, new[] { body, sibling }, openerIndent + "      ");
            shapes.Add(new ContinuationShape(RecoveryHeaderSiblingShape, parent.Insert(injectAt, openerIndent + "if _ == \"a:" + lineBreak), body,
                AtOpenerWidth: true, SecondTarget: sibling));
        }

        var nested = Reindent(q, new[] { body }, openerIndent + "    ");
        shapes.Add(new ContinuationShape(RecoveryHeaderNestedShape,
            nested.Insert(injectAt, openerIndent + "if _($):" + lineBreak + openerIndent + "    if _($):" + lineBreak + openerIndent + "        _ = 0" + lineBreak),
            body, AtOpenerWidth: true, InsertedCount: 3));

        return shapes;
    }

    /// <summary>
    /// The 0-based line of the code line after the one-line statement on <paramref name="body"/>, when it starts at the same
    /// leading whitespace <paramref name="indent"/> (the next statement of the same block), else null.
    /// </summary>
    private static int? SiblingOf(IReadOnlyList<Token> tokens, string q, List<int> lineStarts, int body, string indent)
    {
        var first = tokens.Select((t, i) => (t, i)).First(p => p.t.Line - 1 == body && p.t.Type is not (TokenType.Indent or TokenType.Dedent)).i;
        var newline = first;
        while (newline < tokens.Count && tokens[newline].Type is not (TokenType.Newline or TokenType.Eof))
            newline++;
        if (newline >= tokens.Count || tokens[newline].Type != TokenType.Newline || tokens[newline].Line - 1 != body)
            return null;
        var next = newline + 1;
        while (next < tokens.Count && tokens[next].Type is TokenType.Newline)
            next++;
        if (next >= tokens.Count || tokens[next].Type is TokenType.Indent or TokenType.Dedent or TokenType.Eof)
            return null;
        var line = tokens[next].Line - 1;
        return LeadingWhitespace(q, lineStarts, line) == indent ? line : null;
    }

    /// <summary>
    /// The first block opener in <paramref name="tokens"/> (a <c>Colon</c> directly followed by a <c>Newline</c>, then
    /// <c>Indent</c>): the 0-based line of the opener's FIRST token (a header spanning lines starts there) and of the
    /// first token of its body; <c>(-1, -1)</c> when there is none.
    /// </summary>
    private static (int Opener, int Body) FirstBlock(IReadOnlyList<Token> tokens)
    {
        for (var i = 0; i + 3 < tokens.Count; i++)
        {
            if (tokens[i].Type == TokenType.Colon && tokens[i + 1].Type == TokenType.Newline && tokens[i + 2].Type == TokenType.Indent
                && tokens[i + 3].Type is not (TokenType.Eof or TokenType.Dedent))
            {
                var start = i;
                while (start > 0 && tokens[start - 1].Type is not (TokenType.Newline or TokenType.Indent or TokenType.Dedent))
                    start--;
                return (tokens[start].Line - 1, tokens[i + 3].Line - 1);
            }
        }

        return (-1, -1);
    }
}
