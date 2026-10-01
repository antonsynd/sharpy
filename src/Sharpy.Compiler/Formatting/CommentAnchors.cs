using Sharpy.Compiler.Lexer;

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
/// Layout tokens (line breaks, indents) and the spellings the formatter may add or drop without
/// moving anything (parentheses, a stub's <c>: ...</c>, a trailing comma) are skipped.</item>
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
    internal readonly record struct Anchor(string Text, int Line, bool Inline, string Before, string After, int Depth);

    /// <summary>
    /// One code token each side. Wider windows reach into spellings the formatter legitimately
    /// rewrites (a callable annotation <c>(int) -&gt; int</c> is written <c>function[int, int]</c>),
    /// which would read as a move; the nearest token plus the block depth separates every move the
    /// unparser has produced.
    /// </summary>
    private const int Window = 1;

    /// <summary>The anchors of every comment in <paramref name="tokens"/> (lexed with trivia), in source order.</summary>
    internal static List<Anchor> Of(IReadOnlyList<Token> tokens)
    {
        var pending = new List<(Trivia Comment, bool Inline, int Position, int Depth)>();
        var keys = new List<string>();
        var bodyColumns = new Stack<int>();
        var closing = new List<int>();

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
                pending.Add((comment, false, keys.Count, depth));
            }

            if (token.Type is not (TokenType.Newline or TokenType.Eof))
            {
                closing.Clear();
                if (!IsLayoutSpelling(tokens, i))
                    keys.Add($"{token.Type}:{token.Value}");
            }

            foreach (var comment in Comments(token.TrailingTrivia))
                pending.Add((comment, true, keys.Count, bodyColumns.Count));
        }

        return pending
            .Select(p => new Anchor(
                p.Comment.Text.TrimEnd(' ', '\t'),
                p.Comment.Line,
                p.Inline,
                string.Join(" ", Enumerable.Range(p.Position - Window, Window).Select(k => k >= 0 ? keys[k] : "<start>")),
                string.Join(" ", Enumerable.Range(p.Position, Window).Select(k => k < keys.Count ? keys[k] : "<end>")),
                p.Depth))
            .ToList();
    }

    /// <summary>
    /// Tokens the formatter may add or drop without moving anything: parentheses (redundant ones are
    /// added), a colon and an ellipsis (an interface stub gains <c>: ...</c>), and a trailing comma
    /// before a closing bracket (dropped). They never count as a comment's neighbour.
    /// </summary>
    private static bool IsLayoutSpelling(IReadOnlyList<Token> tokens, int i)
    {
        switch (tokens[i].Type)
        {
            case TokenType.LeftParen or TokenType.RightParen or TokenType.Colon or TokenType.Ellipsis:
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
            if (b.Inline != a.Inline || b.Before != a.Before || b.After != a.After || b.Depth != a.Depth)
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
        if (before.Before != after.Before || before.After != after.After)
            parts.Add($"from between [{before.Before}] and [{before.After}] to between [{after.Before}] and [{after.After}]");
        return string.Join("; ", parts);
    }
}
