using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Formatting;

/// <summary>
/// Where each comment of a token stream is ATTACHED — the fact formatting must keep besides the
/// comment's text and order (P22b, #2077). A comment that keeps its place in the sequence but changes
/// its attachment has moved: <c>else:  # c</c> written on the <c>if</c> line, a block-end comment hoisted
/// out of the block it ends, a def's header comment written on its decorator's line. Layout is not
/// attachment: re-indenting a body (8 → 4 columns) moves no comment.
/// <para>A comment's anchor is:</para>
/// <list type="bullet">
/// <item><b>Inline</b> — it ends a line that carries code (trailing trivia), or stands on its own line.</item>
/// <item><b>Neighbours</b> — the nearest code token before it and after it, by kind and value.
/// Layout tokens (line breaks, indents) and the spellings the formatter may add, drop or re-spell
/// without moving anything (every bracket, the zero-arity <c>tuple</c>, a lambda header, a stub's
/// <c>: ...</c>, a trailing comma — see <see cref="IsLayoutSpelling"/>) are skipped, and a re-spelled
/// token is keyed as the spelling it may have been written as (a lambda parameter's use as the
/// placeholder <c>_</c>, see <see cref="KeyOf"/>).</item>
/// <item><b>Bracket depth</b> — how many brackets enclose it. Skipping brackets as neighbours
/// would otherwise hide a comment moved from inside a bracket to after it (<c>[1, 2  # c</c> ⏎
/// <c>]</c> written <c>[1, 2]  # c</c>); the depth keeps that a move.</item>
/// <item><b>Block depth</b> — how many blocks enclose it. An inline comment has the depth of its
/// line's code. An own-line comment has the depth of the code token that follows it (the statement
/// it precedes, at any column — a comment-only line carries no indentation meaning), EXCEPT when
/// blocks close between it and that token: then its column decides how many of the closing blocks it
/// still belongs to — every closing block whose body column is at or left of the comment. So
/// <c># end of the if body</c> at the if-body's column stays in the if body, and a column-1 comment
/// after a function's last statement is module level.</item>
/// </list>
/// </summary>
internal static class CommentAnchors
{
    internal readonly record struct Anchor(string Text, int Line, bool Inline, string Before, string After, int Depth, int BracketDepth);

    /// <summary>
    /// One code token each side. Wider windows reach into spellings the formatter legitimately
    /// rewrites (a callable annotation <c>(int) -&gt; int</c> is written <c>function[int, int]</c>),
    /// which would read as a move; the nearest token plus the block and bracket depths separate every
    /// move the unparser has produced.
    /// </summary>
    private const int Window = 1;

    /// <summary>The anchors of every comment in <paramref name="tokens"/> (lexed with trivia), in source order.</summary>
    internal static List<Anchor> Of(IReadOnlyList<Token> tokens)
    {
        var pending = new List<(Trivia Comment, bool Inline, int Position, int Depth, int BracketDepth)>();
        var keys = new List<string>();
        var bodyColumns = new Stack<int>();
        var closing = new List<int>();
        var brackets = 0;
        var lambdaParameters = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            switch (token.Type)
            {
                case TokenType.Indent:
                    // The body column is the column of the block's first code token.
                    bodyColumns.Push(i + 1 < tokens.Count ? tokens[i + 1].Column : token.Column);
                    continue;
                case TokenType.Dedent:
                    if (bodyColumns.Count > 0)
                        closing.Add(bodyColumns.Pop());
                    continue;
            }

            foreach (var comment in Comments(token.LeadingTrivia))
            {
                var depth = bodyColumns.Count + closing.Count(column => column <= comment.Column);
                pending.Add((comment, false, keys.Count, depth, brackets));
            }

            if (token.Type is TokenType.LeftParen or TokenType.LeftBracket or TokenType.LeftBrace)
                brackets++;
            else if (token.Type is TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace)
                brackets = Math.Max(0, brackets - 1);

            if (token.Type is TokenType.Newline or TokenType.Eof)
            {
                // A lambda's parameters are in scope to the end of its logical line at most.
                lambdaParameters.Clear();
            }
            else
            {
                closing.Clear();
                if (IsLambdaParameter(tokens, i))
                    lambdaParameters.Add(token.Value);
                if (!IsLayoutSpelling(tokens, i))
                    keys.Add(KeyOf(token, lambdaParameters));
            }

            foreach (var comment in Comments(token.TrailingTrivia))
                pending.Add((comment, true, keys.Count, bodyColumns.Count, brackets));
        }

        return pending
            .Select(p => new Anchor(
                p.Comment.Text.TrimEnd(' ', '\t'),
                p.Comment.Line,
                p.Inline,
                string.Join(" ", Enumerable.Range(p.Position - Window, Window).Select(k => k >= 0 ? keys[k] : "<start>")),
                string.Join(" ", Enumerable.Range(p.Position, Window).Select(k => k < keys.Count ? keys[k] : "<end>")),
                p.Depth,
                p.BracketDepth))
            .ToList();
    }

    /// <summary>
    /// Whether the identifier at <paramref name="i"/> declares a <c>lambda</c> parameter: it follows
    /// <c>lambda</c>, or a comma/<c>*</c>/<c>**</c> of the same parameter list (walking back over
    /// parameter names and separators to the <c>lambda</c> keyword without meeting its colon).
    /// </summary>
    private static bool IsLambdaParameter(IReadOnlyList<Token> tokens, int i)
    {
        if (tokens[i].Type != TokenType.Identifier)
            return false;
        for (var j = i - 1; j >= 0; j--)
        {
            switch (tokens[j].Type)
            {
                case TokenType.Lambda:
                    return true;
                case TokenType.Comma or TokenType.Star or TokenType.DoubleStar or TokenType.Identifier:
                    continue;
                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    /// A neighbour's key: kind and value — except where the kind or value is a SPELLING the unparser
    /// may legitimately change:
    /// <list type="bullet">
    /// <item>a literal's spelling (P22b Phase 4 Task 3): an f-/t-string's delimiters are keyed by kind
    /// alone (their value is the quote, <c>f"""</c> / <c>"""</c>, and the unparser writes a triple-quoted
    /// f-string single-quoted, P22), and a raw string is keyed as the string it denotes (the unparser
    /// writes <c>r"Doc."</c> as <c>"""Doc."""</c>; the token value is the string's content either way);</item>
    /// <item>a partial-application placeholder: the parser lowers <c>add(5, _)</c> to a lambda whose
    /// parameter replaces the <c>_</c> (<c>lambda __placeholder_0: add(5, __placeholder_0)</c>; a keyword
    /// placeholder <c>f(y=_)</c> takes the keyword's name, <c>lambda y: f(y=y)</c>), and the unparser
    /// writes that lambda. So <c>_</c> and every use of a parameter of a lambda earlier on its
    /// logical line are keyed alike, as the placeholder <c>_</c> (the lambda header itself is layout,
    /// <see cref="IsLayoutSpelling"/>).</item>
    /// </list>
    /// </summary>
    private static string KeyOf(Token token, IReadOnlySet<string> lambdaParameters) => token.Type switch
    {
        TokenType.FStringStart or TokenType.FStringEnd => token.Type.ToString(),
        TokenType.RawString => $"{TokenType.String}:{token.Value}",
        TokenType.Identifier when lambdaParameters.Contains(token.Value) => PlaceholderKey,
        _ => $"{token.Type}:{token.Value}"
    };

    /// <summary>The key of the partial-application placeholder <c>_</c> as the lexer reads it.</summary>
    private static readonly string PlaceholderKey = $"{TokenType.Identifier}:_";

    /// <summary>
    /// Tokens the formatter may add, drop or re-spell without moving anything. They never count as a
    /// comment's neighbour:
    /// <list type="bullet">
    /// <item>every bracket, opening and closing — redundant parentheses are added and dropped, and a
    /// type shorthand is written in its canonical form (<c>{int}</c> → <c>set[int]</c>,
    /// <c>{str: int}</c> → <c>dict[str, int]</c>, <c>(int, int)</c> → <c>tuple[int, int]</c>,
    /// <c>(int) -&gt; int</c> → <c>function[int, int]</c>, <c>tuple[()]</c> → <c>tuple</c>), so the
    /// bracket next to a comment is often not the one written. What a bracket means for attachment —
    /// inside it or after it — is the comment's bracket depth, compared separately;</item>
    /// <item>the identifier <c>tuple</c> — the parser normalises the zero-arity tuple type's three
    /// spellings, <c>()</c>, <c>tuple[()]</c> and bare <c>tuple</c>, to one AST, and the unparser writes
    /// bare <c>tuple</c> (making that spelling the spec's <c>tuple[()]</c> is #2169's), so
    /// <c>-&gt; ():  # c</c> is written <c>-&gt; tuple:  # c</c>. With every bracket layout, the three
    /// spellings are then alike: nothing. (Keying an empty <c>()</c> pair as <c>tuple</c> instead does
    /// not hold: the unparser also DROPS empty pairs — <c>case Empty()</c> in a union,
    /// <c>@serializable()</c> — which are no type.) Every other use of the name keys alike on both sides;</item>
    /// <item>a lambda header — the <c>lambda</c> keyword and its parameter names: a partial
    /// application <c>print(_)</c> is written as the lambda the parser lowered it to,
    /// <c>lambda __placeholder_0: print(__placeholder_0)</c>, so the header is added in front of the
    /// statement (see <see cref="KeyOf"/> for the parameter's uses);</item>
    /// <item>a colon and an ellipsis (an interface stub gains <c>: ...</c>), and a trailing comma
    /// before a closing bracket (dropped).</item>
    /// </list>
    /// </summary>
    private static bool IsLayoutSpelling(IReadOnlyList<Token> tokens, int i)
    {
        switch (tokens[i].Type)
        {
            case TokenType.LeftParen or TokenType.RightParen or TokenType.LeftBracket or TokenType.RightBracket
                or TokenType.LeftBrace or TokenType.RightBrace or TokenType.Colon or TokenType.Ellipsis or TokenType.Lambda:
                return true;
            case TokenType.Identifier when tokens[i].Value == BuiltinNames.Tuple || IsLambdaParameter(tokens, i):
                return true;
            case TokenType.Comma:
                var next = i + 1;
                while (next < tokens.Count && tokens[next].Type is TokenType.Newline or TokenType.Indent or TokenType.Dedent)
                    next++;
                return next < tokens.Count && tokens[next].Type is TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace;
            default:
                return false;
        }
    }

    private static IEnumerable<Trivia> Comments(IReadOnlyList<Trivia>? trivia)
        => trivia == null ? Enumerable.Empty<Trivia>() : trivia.Where(t => t.Kind == TriviaKind.Comment);

    /// <summary>
    /// The first comment whose anchor differs between two streams with the SAME comment sequence
    /// (the caller checks the sequence first), or null.
    /// </summary>
    internal static (Anchor Before, Anchor After)? FirstMoved(List<Anchor> before, List<Anchor> after)
    {
        for (var i = 0; i < Math.Min(before.Count, after.Count); i++)
        {
            var (b, a) = (before[i], after[i]);
            if (b.Inline != a.Inline || b.Before != a.Before || b.After != a.After || b.Depth != a.Depth || b.BracketDepth != a.BracketDepth)
                return (b, a);
        }

        return null;
    }

    /// <summary>What changed about a moved comment's anchor, for the refusal message and the sweep's detail.</summary>
    internal static string Describe(Anchor before, Anchor after)
    {
        var parts = new List<string>();
        if (before.Inline != after.Inline)
            parts.Add(before.Inline ? "from the end of a code line onto its own line" : "from its own line onto the end of a code line");
        if (before.Depth != after.Depth)
            parts.Add($"from block depth {before.Depth} to {after.Depth}");
        if (before.BracketDepth != after.BracketDepth)
            parts.Add($"from bracket depth {before.BracketDepth} to {after.BracketDepth}");
        if (before.Before != after.Before || before.After != after.After)
            parts.Add($"from between [{before.Before}] and [{before.After}] to between [{after.Before}] and [{after.After}]");
        return string.Join("; ", parts);
    }
}
