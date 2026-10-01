using System.Text;
using Sharpy.Compiler.Lexer;
using SLexer = Sharpy.Compiler.Lexer.Lexer;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The kinds of <c># cN</c> comment <see cref="FormatterTwins.CommentInjected"/> injects. Each is a
/// comment anchor the formatter must keep (#2077); the census reports each kind's corpus total so a
/// kind the corpus never exercises is visible.
/// </summary>
public enum InjectedCommentKind
{
    /// <summary><c>(  # cN</c> — right after an opening bracket, then a line break.</summary>
    OpenBracket,

    /// <summary><c>,  # cN</c> — right after a comma at bracket depth ≥ 1, then a line break.</summary>
    Comma,

    /// <summary>An own-line <c># cN</c> at column 1 right before a closing bracket.</summary>
    CloseBracket,

    /// <summary><c>else:  # cN</c> — after the colon of an <c>elif</c>/<c>else</c>/<c>except</c>/<c>finally</c>/<c>case</c> block header.</summary>
    ClauseHeaderColon,

    /// <summary>An own-line <c># cN</c> at the clause keyword's indent, on the line before it.</summary>
    BeforeClauseKeyword,

    /// <summary>An own-line <c># cN</c> at a closed block's indent, right after the block's last line (one per block closed).</summary>
    BlockEnd,

    /// <summary>An own-line <c># cN</c> at end of file.</summary>
    EndOfFile,

    /// <summary>The line-total kind, trailing half: <c>  # cN</c> after the last code token of a line that ends at bracket depth 0 (a statement, a header, a member line).</summary>
    LineTrailing,

    /// <summary>The line-total kind, trailing half, on a line that ends INSIDE brackets — an inner comment of its statement or header.</summary>
    LineTrailingInBracket,

    /// <summary>The line-total kind, own-line half: <c># cN</c> at the line's indent, on the line before it.</summary>
    LineOwnLine,
}

/// <summary>Per-kind injection counts of one <see cref="FormatterTwins.CommentInjected"/> call.</summary>
public sealed class InjectedCommentCounts
{
    public static readonly IReadOnlyList<InjectedCommentKind> Kinds = Enum.GetValues<InjectedCommentKind>();

    private readonly int[] _counts = new int[Kinds.Count];

    public int this[InjectedCommentKind kind] => _counts[(int)kind];

    public int Total => _counts.Sum();

    internal void Increment(InjectedCommentKind kind) => _counts[(int)kind]++;

    public override string ToString() => string.Join(" ", Kinds.Select(k => $"{k}={this[k]}"));
}

/// <summary>
/// Twin builders and observers for the formatter meaning-preservation sweep (P22b, #2062 #2077
/// #2157). A twin is the source with text INSERTED at token-derived offsets — never re-laid-out —
/// so it stays the same program:
/// <list type="bullet">
/// <item><see cref="CommentInjected"/> (T1) adds a uniquely numbered <c># cN</c> at every comment
/// anchor kind in <see cref="InjectedCommentKind"/>; comments are inert, so the twin parses to the
/// same AST.</item>
/// <item><see cref="BacktickInjected"/> (T2) wraps every identifier token not already escaped in
/// backticks, except the parser's contextual identifiers <c>before_set</c>/<c>after_set</c> and a
/// contextual keyword of <see cref="ContextualKeywordsReadAsKeywords"/> at its contextual position
/// (<see cref="IsReadAsContextualKeyword"/>).</item>
/// </list>
/// Nothing is injected inside a string token or between <c>FStringStart</c> and <c>FStringEnd</c>
/// for T1 (the replacement-field hole is P22's contract); T2 escapes hole identifiers too (the hole
/// is written verbatim, so the escape must survive).
/// </summary>
public static class FormatterTwins
{
    private const string ContextualBeforeSet = "before_set";
    private const string ContextualAfterSet = "after_set";

    /// <summary>
    /// Contextual keywords the parser reads AS the keyword even when backtick-escaped (#2166: an
    /// escaped <c>`get`</c>, <c>`_`</c>, <c>`out`</c> … at its contextual position parses to the same
    /// AST as the bare spelling and the escape is lost). Escaping one THERE is not meaning-preserving
    /// under the spec, so T2 leaves it bare — at that position only
    /// (<see cref="IsReadAsContextualKeyword"/>): the same spelling as a plain identifier
    /// (<c>d.get</c>, <c>xs.add(1)</c>, <c>out = 2</c>) round-trips its escape and is escaped like any
    /// other identifier. The census counts how many each value skipped; every value still fails at
    /// its contextual position (measured: emptying this set reddens 311 T2 cells, and each of the ten
    /// values drops its escape in ≥ 1 fixture).
    /// </summary>
    public static readonly IReadOnlySet<string> ContextualKeywordsReadAsKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "_", "get", "set", "init", "out", "ref", "add", "remove", "when", "notnull",
    };

    /// <summary>
    /// Whether the identifier token at <paramref name="i"/> sits where the parser reads its value as
    /// a contextual keyword despite an escape (#2166) — the positions T2 leaves bare. Each arm
    /// mirrors the parser's check, generously (a skip only costs coverage; a missed position is a
    /// red T2 cell):
    /// </summary>
    public static bool IsReadAsContextualKeyword(IReadOnlyList<Token> tokens, int i)
    {
        var token = tokens[i];
        if (token.Type != TokenType.Identifier || !ContextualKeywordsReadAsKeywords.Contains(token.Value))
            return false;
        var previous = i > 0 ? tokens[i - 1].Type : TokenType.Newline;
        var next = i + 1 < tokens.Count ? tokens[i + 1].Type : TokenType.Eof;
        return token.Value switch
        {
            // #2166: `_` is the partial-application placeholder in every expression position and the
            // wildcard in every pattern position — an escaped `_` is read as both.
            "_" => true,
            // #2166: ParsePropertyDef reads get/set/init right after `property` as the accessor.
            "get" or "set" or "init" => previous == TokenType.Property,
            // #2166: ParseEventDef reads add/remove right after `event` as the accessor.
            "add" or "remove" => previous == TokenType.Event,
            // #2166: a type-parameter constraint `[T: notnull]` (also after `,`/`&` in a constraint list).
            "notnull" => previous is TokenType.Colon or TokenType.Comma or TokenType.Ampersand,
            // #2166: the exception filter of `except E when …` / `except E as e when …`.
            "when" => LogicalLineStartsWith(tokens, i, TokenType.Except),
            // #2166: variance `[out T]`, the parameter modifier `x: out T`, and the call-site argument
            // modifier `f(out y)` (the parser's peek guard: not before `,`, `)` or `=`).
            "out" => IsModifierPosition(previous, next) || (previous is TokenType.LeftBracket or TokenType.Comma && next == TokenType.Identifier),
            // #2166: the parameter modifier `x: ref T` and the call-site modifier `f(ref y)`.
            "ref" => IsModifierPosition(previous, next),
            _ => false,
        };

        static bool IsModifierPosition(TokenType previous, TokenType next)
            => (previous == TokenType.Colon && next == TokenType.Identifier)
                || (previous is TokenType.LeftParen or TokenType.Comma
                    && next is not (TokenType.Comma or TokenType.RightParen or TokenType.Assign));
    }

    /// <summary>Whether the logical line holding token <paramref name="i"/> begins with a <paramref name="type"/> token.</summary>
    private static bool LogicalLineStartsWith(IReadOnlyList<Token> tokens, int i, TokenType type)
    {
        var j = i;
        while (j > 0 && tokens[j - 1].Type is not (TokenType.Newline or TokenType.Indent or TokenType.Dedent))
            j--;
        return tokens[j].Type == type;
    }

    /// <summary>Lexes with trivia and positions, the way <c>FormatterService.Format</c> does.</summary>
    public static List<Token> Lex(string source, out bool hasErrors)
    {
        var lexer = new SLexer(source, preserveTrivia: true);
        var tokens = lexer.TokenizeAll();
        hasErrors = lexer.Diagnostics.HasErrors;
        return tokens;
    }

    /// <summary>The <c>#</c> comments of a source in source order (leading then trailing trivia per token).</summary>
    public static IReadOnlyList<string> Comments(string source)
        => Lex(source, out _)
            .SelectMany(t => (t.LeadingTrivia ?? Array.Empty<Trivia>()).Concat(t.TrailingTrivia ?? Array.Empty<Trivia>()))
            .Where(t => t.Kind == TriviaKind.Comment)
            .Select(CommentText)
            .ToList();

    /// <summary>
    /// A comment's text as the meaning-preservation contract compares it: trailing spaces/tabs are
    /// not part of it. The lexer keeps them (<c>"# c   "</c>) and <c>StripTrailingWhitespace</c>
    /// removes them by design — the same rule as the formatter's net
    /// (<c>FormatterService.CheckMeaningPreserved</c>), so the sweep and the net agree.
    /// </summary>
    public static string CommentText(Trivia comment) => comment.Text.TrimEnd(' ', '\t');

    /// <summary>The multiset of backtick-escaped identifier token values, sorted ordinally.</summary>
    public static IReadOnlyList<string> EscapedNames(string source)
        => Lex(source, out _)
            .Where(t => t.Type == TokenType.Identifier && t.IsBacktickEscaped)
            .Select(t => t.Value)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

    /// <summary>T2: every unescaped identifier token wrapped in backticks, by source offset.</summary>
    public static (string Text, int Count) BacktickInjected(string source)
    {
        var tokens = Lex(source, out _);
        var targets = tokens
            .Where((t, i) => IsUnescapedIdentifier(t)
                && t.Value != ContextualBeforeSet && t.Value != ContextualAfterSet
                && !IsReadAsContextualKeyword(tokens, i))
            .OrderByDescending(t => t.Position)
            .ToList();
        var text = new StringBuilder(source);
        foreach (var token in targets)
        {
            text.Insert(token.Position + token.Length, '`');
            text.Insert(token.Position, '`');
        }

        return (text.ToString(), targets.Count);
    }

    /// <summary>How many unescaped identifier tokens of each <see cref="ContextualKeywordsReadAsKeywords"/> value T2 leaves bare (at a contextual position).</summary>
    public static IReadOnlyDictionary<string, int> ContextualKeywordSkips(string source)
    {
        var tokens = Lex(source, out _);
        return tokens
            .Where((t, i) => IsUnescapedIdentifier(t) && IsReadAsContextualKeyword(tokens, i))
            .GroupBy(t => t.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
    }

    private static bool IsUnescapedIdentifier(Token t)
        => t.Type == TokenType.Identifier && !t.IsBacktickEscaped && t.Position >= 0;

    // ================================================================
    // T1
    // ================================================================

    /// <summary>A pending insertion: text at a source offset. <c>Order</c> sorts insertions sharing an offset.</summary>
    private sealed record Insertion(int Offset, int Order, InjectedCommentKind Kind, bool OwnLine, string Indent);

    /// <summary>One code unit: a token outside any f-string, or a whole outermost f-string.</summary>
    private sealed record Unit(int TokenIndex, int Start, int End, int StartLine, int EndLine, int DepthAfter, bool HasTrailingComment);

    /// <summary>
    /// T1: the source with a numbered <c># cN</c> at every anchor of every
    /// <see cref="InjectedCommentKind"/>. Numbers ascend in the twin's text order. An end-of-line
    /// comment is never appended to a line that already ends in a comment (it would swallow it), in
    /// a literal, or before a backslash continuation.
    /// </summary>
    public static (string Text, InjectedCommentCounts Injected) CommentInjected(string source)
        => Inject(source, kinds: null);

    /// <summary>The comment kinds of <see cref="LineTrailingInjected"/> (T3).</summary>
    public static readonly IReadOnlySet<InjectedCommentKind> LineTrailingKinds = new HashSet<InjectedCommentKind> { InjectedCommentKind.LineTrailing };

    /// <summary>
    /// T3: the source with a numbered <c>  # cN</c> after the last code token of every line that ends
    /// at bracket depth 0 outside a literal — the trailing half of the line-total kind ALONE, with no
    /// bracket, clause, block-end or own-line injection. T1 puts a comment inside every bracket,
    /// which writes every bracketed statement verbatim from source (P22b Decision 5); T3 leaves the
    /// statement to the unparser, so a trailing comment meets every spelling the unparser rewrites
    /// (<c>{int}</c> → <c>set[int]</c>, <c>add(5, _)</c> → <c>lambda __placeholder_0: …</c>). Lines
    /// ending inside brackets get no comment for the same reason.
    /// </summary>
    public static (string Text, InjectedCommentCounts Injected) LineTrailingInjected(string source)
        => Inject(source, LineTrailingKinds);

    /// <summary>
    /// Injects the comment kinds in <paramref name="kinds"/> (every kind when null). A slot is
    /// "taken" — the line-total kind leaves it alone — only by a kind that is injected.
    /// </summary>
    private static (string Text, InjectedCommentCounts Injected) Inject(string source, IReadOnlySet<InjectedCommentKind>? kinds)
    {
        bool Included(InjectedCommentKind kind) => kinds == null || kinds.Contains(kind);

        var tokens = Lex(source, out _);
        var nl = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lineStarts = LineStarts(source);
        var literalLines = LiteralSpans.LinesStartingInside(source, LiteralSpans.Of(tokens));
        var insertions = new List<Insertion>();

        var units = Units(tokens, source, lineStarts);
        var unitByToken = units.ToDictionary(u => u.TokenIndex);

        // Tokens whose line end already carries an injected comment (open bracket, comma, clause colon)
        // and lines whose own-line slot is taken by a clause keyword.
        var trailingTaken = new HashSet<int>();
        var ownLineTaken = new HashSet<int>();
        var indentStack = new Stack<string>();
        var depth = 0;
        var fstringDepth = 0;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (fstringDepth > 0)
            {
                if (token.Type == TokenType.FStringStart)
                    fstringDepth++;
                else if (token.Type == TokenType.FStringEnd)
                    fstringDepth--;
                continue;
            }

            switch (token.Type)
            {
                case TokenType.FStringStart:
                    fstringDepth = 1;
                    break;

                case TokenType.LeftParen or TokenType.LeftBracket or TokenType.LeftBrace:
                    depth++;
                    insertions.Add(new Insertion(token.Position + token.Length, 0, InjectedCommentKind.OpenBracket, false, ""));
                    if (Included(InjectedCommentKind.OpenBracket))
                        trailingTaken.Add(i);
                    break;

                case TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace:
                    insertions.Add(new Insertion(token.Position, 3, InjectedCommentKind.CloseBracket, true, ""));
                    depth--;
                    break;

                case TokenType.Comma when depth >= 1:
                    insertions.Add(new Insertion(token.Position + token.Length, 0, InjectedCommentKind.Comma, false, ""));
                    if (Included(InjectedCommentKind.Comma))
                        trailingTaken.Add(i);
                    break;

                case TokenType.Elif or TokenType.Else or TokenType.Except or TokenType.Finally or TokenType.Case
                    when depth == 0 && IsFirstOnLine(source, lineStarts, token):
                    {
                        // A clause keyword starts a logical line and its header ends at a depth-0
                        // colon; a ternary `else` continued onto a line by a backslash does neither.
                        var line = LineOf(lineStarts, token.Position);
                        var colon = HeaderColon(tokens, i);
                        if (colon < 0 || ContinuesBackslash(source, lineStarts, literalLines, line))
                            break;
                        insertions.Add(new Insertion(lineStarts[line - 1], 2, InjectedCommentKind.BeforeClauseKeyword, true, LeadingWhitespace(source, lineStarts, token)));
                        if (Included(InjectedCommentKind.BeforeClauseKeyword))
                            ownLineTaken.Add(line);

                        if (colon + 2 < tokens.Count
                            && tokens[colon + 1].Type == TokenType.Newline
                            && tokens[colon + 2].Type == TokenType.Indent
                            && !unitByToken[colon].HasTrailingComment)
                        {
                            insertions.Add(new Insertion(tokens[colon].Position + tokens[colon].Length, 0, InjectedCommentKind.ClauseHeaderColon, false, ""));
                            if (Included(InjectedCommentKind.ClauseHeaderColon))
                                trailingTaken.Add(colon);
                        }

                        break;
                    }

                case TokenType.Indent:
                    {
                        var next = i + 1 < tokens.Count ? tokens[i + 1] : token;
                        indentStack.Push(LeadingWhitespace(source, lineStarts, next));
                        break;
                    }

                case TokenType.Dedent:
                    {
                        // A run of dedents closes blocks innermost first, right after the last line of
                        // the innermost block (the logical line's Newline; at EOF without one, the end).
                        var run = i;
                        while (run > 0 && tokens[run - 1].Type == TokenType.Dedent)
                            run--;
                        var offset = run > 0 && tokens[run - 1].Type == TokenType.Newline
                            ? AfterLineBreak(source, tokens[run - 1].Position)
                            : source.Length;
                        var indent = indentStack.Count > 0 ? indentStack.Pop() : "";
                        insertions.Add(new Insertion(offset, 1, InjectedCommentKind.BlockEnd, true, indent));
                        break;
                    }
            }
        }

        // The line-total kind: every line's trailing slot and own-line slot not already taken.
        var lastEndingOn = new Dictionary<int, Unit>();
        var firstStartingOn = new Dictionary<int, Unit>();
        foreach (var unit in units)
        {
            lastEndingOn[unit.EndLine] = unit;
            firstStartingOn.TryAdd(unit.StartLine, unit);
        }

        foreach (var (line, unit) in lastEndingOn)
        {
            if (trailingTaken.Contains(unit.TokenIndex) || unit.HasTrailingComment || literalLines.Contains(line + 1))
                continue;
            var lineEnd = LineContentEnd(source, lineStarts, line);
            if (source.AsSpan(unit.End, lineEnd - unit.End).Trim().Length != 0)
                continue; // a backslash continuation follows the last token
            var kind = unit.DepthAfter >= 1 ? InjectedCommentKind.LineTrailingInBracket : InjectedCommentKind.LineTrailing;
            insertions.Add(new Insertion(unit.End, 0, kind, false, ""));
        }

        foreach (var (line, unit) in firstStartingOn)
        {
            if (ownLineTaken.Contains(line)
                || !IsFirstOnLine(source, lineStarts, tokens[unit.TokenIndex])
                || literalLines.Contains(line)
                || ContinuesBackslash(source, lineStarts, literalLines, line))
            {
                continue;
            }

            insertions.Add(new Insertion(lineStarts[line - 1], 2, InjectedCommentKind.LineOwnLine, true, LeadingWhitespace(source, lineStarts, tokens[unit.TokenIndex])));
        }

        insertions.Add(new Insertion(source.Length, 4, InjectedCommentKind.EndOfFile, true, ""));

        return Build(source, nl, insertions.Where(insertion => Included(insertion.Kind)).ToList());
    }

    private static (string, InjectedCommentCounts) Build(string source, string nl, List<Insertion> insertions)
    {
        var counts = new InjectedCommentCounts();
        var text = new StringBuilder(source.Length + insertions.Count * 12);
        var n = 0;
        var at = 0;
        foreach (var insertion in insertions.OrderBy(x => x.Offset).ThenBy(x => x.Order))
        {
            text.Append(source, at, insertion.Offset - at);
            at = insertion.Offset;
            n++;
            counts.Increment(insertion.Kind);
            var comment = $"# c{n}";
            if (insertion.OwnLine)
            {
                // An own-line comment starts a line: after the previous line's break, or its own.
                if (insertion.Kind is InjectedCommentKind.CloseBracket
                    || (text.Length > 0 && text[^1] != '\n' && text[^1] != '\r'))
                {
                    text.Append(nl);
                }

                text.Append(insertion.Indent).Append(comment).Append(nl);
            }
            else if (insertion.Kind is InjectedCommentKind.OpenBracket or InjectedCommentKind.Comma)
            {
                text.Append("  ").Append(comment).Append(nl);
            }
            else
            {
                text.Append("  ").Append(comment);
            }
        }

        text.Append(source, at, source.Length - at);
        return (text.ToString(), counts);
    }

    /// <summary>Code units in source order: tokens outside f-strings, each outermost f-string as one unit.</summary>
    private static List<Unit> Units(List<Token> tokens, string source, List<int> lineStarts)
    {
        var units = new List<Unit>();
        var depth = 0;
        var fstringDepth = 0;
        var fstringStart = -1;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Position < 0)
                continue;
            if (fstringDepth > 0)
            {
                if (token.Type == TokenType.FStringStart)
                {
                    fstringDepth++;
                }
                else if (token.Type == TokenType.FStringEnd && --fstringDepth == 0)
                {
                    var start = tokens[fstringStart].Position;
                    var end = token.Position + token.Length;
                    units.Add(new Unit(fstringStart, start, end, LineOf(lineStarts, start), LineOf(lineStarts, Math.Max(start, end - 1)), depth, HasComment(token.TrailingTrivia)));
                    // The unit is keyed by its FStringStart token (the first-on-line check reads it);
                    // the trailing comment rides the FStringEnd token.
                }

                continue;
            }

            switch (token.Type)
            {
                case TokenType.Newline or TokenType.Indent or TokenType.Dedent or TokenType.Eof:
                    continue;
                case TokenType.FStringStart:
                    fstringDepth = 1;
                    fstringStart = i;
                    continue;
                case TokenType.LeftParen or TokenType.LeftBracket or TokenType.LeftBrace:
                    depth++;
                    break;
                case TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace:
                    depth--;
                    break;
            }

            var tokenEnd = TokenEnd(tokens, i, source);
            units.Add(new Unit(i, token.Position, tokenEnd, LineOf(lineStarts, token.Position), LineOf(lineStarts, Math.Max(token.Position, tokenEnd - 1)), depth, HasComment(token.TrailingTrivia)));
        }

        return units;
    }

    /// <summary>
    /// Where a token's source text ends. A numeric literal's <c>Value</c> is normalised — underscores
    /// dropped, <c>.5</c> spelled <c>0.5</c> — and carries no <c>SourceLength</c>, so its
    /// <see cref="Token.Length"/> is not its source extent; its end is found by scanning the lexeme,
    /// bounded by the next token.
    /// </summary>
    private static int TokenEnd(List<Token> tokens, int index, string source)
    {
        var token = tokens[index];
        if (token.Type is not (TokenType.Integer or TokenType.Float))
            return token.Position + token.Length;
        var bound = index + 1 < tokens.Count && tokens[index + 1].Position > token.Position
            ? Math.Min(source.Length, tokens[index + 1].Position)
            : source.Length;
        var end = token.Position;
        while (end < bound)
        {
            var c = source[end];
            var exponentSign = c is '+' or '-' && end > token.Position && source[end - 1] is 'e' or 'E'
                && !source.AsSpan(token.Position, end - token.Position).StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '.' && !exponentSign)
                break;
            end++;
        }

        return end;
    }

    private static bool HasComment(IReadOnlyList<Trivia>? trivia)
        => trivia != null && trivia.Any(t => t.Kind == TriviaKind.Comment);

    /// <summary>The index of a clause header's colon: the first depth-0 colon on the logical line, or -1.</summary>
    private static int HeaderColon(List<Token> tokens, int keyword)
    {
        var depth = 0;
        var fstringDepth = 0;
        for (var j = keyword + 1; j < tokens.Count; j++)
        {
            var type = tokens[j].Type;
            if (type == TokenType.FStringStart)
                fstringDepth++;
            else if (type == TokenType.FStringEnd)
                fstringDepth--;
            if (fstringDepth > 0 || type == TokenType.FStringEnd)
                continue;
            switch (type)
            {
                case TokenType.Newline or TokenType.Eof:
                    return -1;
                case TokenType.LeftParen or TokenType.LeftBracket or TokenType.LeftBrace:
                    depth++;
                    break;
                case TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace:
                    depth--;
                    break;
                case TokenType.Colon when depth == 0:
                    return j;
            }
        }

        return -1;
    }

    /// <summary>0-based offsets of each line's first character; a line ends at <c>\r\n</c>, <c>\n</c> or a lone <c>\r</c>, as in the lexer.</summary>
    private static List<int> LineStarts(string source)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (c == '\r' && i + 1 < source.Length && source[i + 1] == '\n')
                i++;
            else if (c != '\n' && c != '\r')
                continue;
            starts.Add(i + 1);
        }

        return starts;
    }

    /// <summary>The 1-based line containing <paramref name="offset"/>.</summary>
    private static int LineOf(List<int> lineStarts, int offset)
    {
        var index = lineStarts.BinarySearch(offset);
        return index >= 0 ? index + 1 : ~index;
    }

    /// <summary>The offset just before the line break (or end of source) ending the 1-based line.</summary>
    private static int LineContentEnd(string source, List<int> lineStarts, int line)
    {
        var end = line < lineStarts.Count ? lineStarts[line] : source.Length;
        while (end > lineStarts[line - 1] && (source[end - 1] == '\n' || source[end - 1] == '\r'))
            end--;
        return end;
    }

    private static int AfterLineBreak(string source, int position)
    {
        if (position >= source.Length)
            return source.Length;
        if (source[position] == '\r' && position + 1 < source.Length && source[position + 1] == '\n')
            return position + 2;
        return source[position] is '\n' or '\r' ? position + 1 : position;
    }

    private static string LeadingWhitespace(string source, List<int> lineStarts, Token token)
    {
        if (token.Position < 0 || token.Position > source.Length)
            return "";
        var start = lineStarts[LineOf(lineStarts, token.Position) - 1];
        var prefix = source.Substring(start, token.Position - start);
        return prefix.Trim().Length == 0 ? prefix : "";
    }

    private static bool IsFirstOnLine(string source, List<int> lineStarts, Token token)
    {
        var start = lineStarts[LineOf(lineStarts, token.Position) - 1];
        return source.AsSpan(start, token.Position - start).Trim().Length == 0;
    }

    /// <summary>Whether the previous line ends in a backslash continuation (outside a literal).</summary>
    private static bool ContinuesBackslash(string source, List<int> lineStarts, HashSet<int> literalLines, int line)
    {
        if (line <= 1 || literalLines.Contains(line))
            return false;
        var end = LineContentEnd(source, lineStarts, line - 1);
        var content = source.AsSpan(lineStarts[line - 2], end - lineStarts[line - 2]).TrimEnd();
        return content.Length > 0 && content[^1] == '\\';
    }
}
