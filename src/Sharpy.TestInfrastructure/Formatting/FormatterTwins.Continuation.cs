using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Lexer;

namespace Sharpy.TestInfrastructure.Formatting;

/// <summary>
/// One document of <see cref="FormatterTwins.ContinuationShapes"/>: its <see cref="Shape"/>, its text, and the
/// 0-based line <see cref="InsertedAt"/> the shape injected — Q's line <c>l</c> is line <c>l</c> of
/// <see cref="Text"/> above it and line <c>l + 1</c> at or below it. The line after the injected one is Q's
/// first body line of its first block (re-indented by <c>recovery-misindented</c>; untouched otherwise).
/// </summary>
public sealed record ContinuationShape(string Shape, string Text, int InsertedAt);

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
    /// The injected line of each shape (spellings of plan-f92797 Design 5). Each is a statement that a user
    /// typing above a block leaves unfinished; the bracket shapes leave a bracket the lexer never sees closed.
    /// </summary>
    private static readonly IReadOnlyList<(string Shape, string Line)> ContinuationLines = new[]
    {
        (BracketEofShape, "_ = [1,"),
        (BracketStringShape, "_ = foo(\"abc,"),
        (BracketBacktickShape, "_ = foo(`x,"),
        (BracketDroppedShape, "_ = foo($,"),
        (RecoveryShape, "_ = $"),
        (RecoveryMisindentedShape, "_ = $"),
    };

    /// <summary>
    /// Every shape <see cref="ContinuationShapes"/> must build when Q has a block — the census's universe, spelled
    /// from the constants rather than read off <c>ContinuationLines</c>, so a shape the builder stops building is a
    /// missing floor instead of a missing name.
    /// </summary>
    public static readonly IReadOnlyList<string> ContinuationShapeNames = new[]
    {
        BracketEofShape, BracketStringShape, BracketBacktickShape, BracketDroppedShape, RecoveryShape, RecoveryMisindentedShape,
    };

    /// <summary>The spaces <see cref="RecoveryMisindentedShape"/> adds to the line after its injected line: a width on no level of a 4- or 8-space document.</summary>
    public const string MisindentBy = "  ";

    /// <summary>
    /// The continuation documents of a CLEAN parent <paramref name="q"/> (#2279) — the one recipe the route-parity
    /// sweep's S7 derives its documents from. Each shape's line is injected as the FIRST body line of Q's first
    /// block: directly above the line holding the first token of the body of Q's first block opener (a
    /// <c>Colon</c> followed by a <c>Newline</c>), at that line's own leading whitespace, ended by Q's line break.
    /// <see cref="RecoveryMisindentedShape"/> also re-indents that body line by <see cref="MisindentBy"/>. Q's text
    /// and line breaks are kept byte-for-byte outside the injected line (and the re-indented one). None is built
    /// when Q has no block.
    /// </summary>
    public static IReadOnlyList<ContinuationShape> ContinuationShapes(string q)
    {
        var tokens = Lex(q, out _);
        var body = FirstBodyLine(tokens);
        if (body < 0)
            return Array.Empty<ContinuationShape>();

        var lineStarts = LineStarts(q);
        var indent = LeadingWhitespace(q, lineStarts, body);
        var injectAt = lineStarts[body];
        var lineBreak = LineDiff.DocumentLineBreak(q);
        var shapes = new List<ContinuationShape>(ContinuationLines.Count);
        foreach (var (shape, line) in ContinuationLines)
        {
            var parent = shape == RecoveryMisindentedShape ? Reindent(q, new[] { body }, indent + MisindentBy) : q;
            shapes.Add(new ContinuationShape(shape, parent.Insert(injectAt, indent + line + lineBreak), body));
        }

        return shapes;
    }

    /// <summary>
    /// The 0-based line of the first token of the body of the first block opener in <paramref name="tokens"/> (a
    /// <c>Colon</c> directly followed by a <c>Newline</c>, then <c>Indent</c>), or -1 when there is none.
    /// </summary>
    private static int FirstBodyLine(IReadOnlyList<Token> tokens)
    {
        for (var i = 0; i + 3 < tokens.Count; i++)
        {
            if (tokens[i].Type == TokenType.Colon && tokens[i + 1].Type == TokenType.Newline && tokens[i + 2].Type == TokenType.Indent
                && tokens[i + 3].Type is not (TokenType.Eof or TokenType.Dedent))
            {
                return tokens[i + 3].Line - 1;
            }
        }

        return -1;
    }
}
