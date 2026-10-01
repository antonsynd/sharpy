using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Pretty;

/// <summary>
/// The unparser's ONE comment mechanism (P22b Design Decision 3, #2077): every comment and blank
/// line of the source, in source order, consumed from the head at <b>anchors</b> as the unparser
/// writes the tree. An anchor at line L first takes everything before L (written as full lines),
/// then the comments INSIDE its header's line range (which make the header verbatim, Decision 5),
/// then the inline comment that ends its header line (appended to the written header). A body's
/// end takes the own-line comments at or right of its column that precede the next code; the
/// module's end takes the rest. Because the cursor only ever advances, a comment no anchor claims
/// is written by the next anchor instead of being dropped — a new clause or line kind is covered
/// by construction, not by another per-node trivia field.
/// <para>Built from the trivia-preserving token stream and its source text
/// (<see cref="FromTokens"/>, the formatter's path), or — when only the AST is at hand — from the
/// statement trivia the parser attached (<see cref="FromAst"/>; no body-end, docstring or
/// verbatim facts, so it writes what the old per-statement writers wrote).</para>
/// </summary>
internal sealed class TriviaCursor
{
    private readonly List<Trivia> _items;
    private int _head;

    // Facts of the token stream (null when built from the AST).
    private readonly string? _source;
    private readonly List<Token>? _codeTokens;
    private readonly List<int>? _boundaries;
    private readonly int[]? _codeLines;
    private readonly HashSet<int>? _linesStartingInsideLiteral;
    private readonly List<int>? _lineStarts;

    private TriviaCursor(List<Trivia> items, string? source, List<Token>? codeTokens, List<int>? boundaries,
        int[]? codeLines, HashSet<int>? linesStartingInsideLiteral)
    {
        _items = items;
        _source = source;
        _codeTokens = codeTokens;
        _boundaries = boundaries;
        _codeLines = codeLines;
        _linesStartingInsideLiteral = linesStartingInsideLiteral;
        if (source != null)
        {
            // Line breaks as the lexer counts them: \r\n, \n and a lone \r each end a line.
            _lineStarts = new List<int> { 0 };
            for (int i = 0; i < source.Length; i++)
            {
                var c = source[i];
                if (c == '\r' && i + 1 < source.Length && source[i + 1] == '\n')
                    i++;
                if (c == '\n' || c == '\r')
                    _lineStarts.Add(i + 1);
            }
        }
    }

    /// <summary>The cursor for <paramref name="options"/>: the source's token stream when given, else the AST's statement trivia when trivia is preserved, else none.</summary>
    public static TriviaCursor? Create(UnparseOptions options, Node root)
    {
        if (options.SourceTokens != null && options.SourceText != null)
            return FromTokens(options.SourceTokens, options.SourceText);
        return options.PreserveTrivia ? FromAst(root) : null;
    }

    /// <summary>
    /// Every comment and blank-line item of <paramref name="tokens"/> (lexed with trivia from
    /// <paramref name="source"/>), in source order. Comments inside a string or f-/t-string token's
    /// span (a replacement-field comment) are excluded: the literal's own verbatim writer
    /// (<c>RawText</c>, P22) carries them.
    /// </summary>
    public static TriviaCursor FromTokens(IReadOnlyList<Token> tokens, string source)
    {
        var literals = LiteralSpans.Of(tokens);
        var items = new List<Trivia>();
        var boundaries = new List<int>();
        var codeTokens = new List<Token>();
        foreach (var token in tokens)
        {
            Add(token.LeadingTrivia);
            Add(token.TrailingTrivia);
            if (token.Position >= 0 && token.Type != TokenType.Indent && token.Type != TokenType.Dedent)
            {
                boundaries.Add(token.Position);
                if (token.Type is not (TokenType.Newline or TokenType.Eof))
                    codeTokens.Add(token);
            }
        }

        items.Sort((a, b) => a.Position.CompareTo(b.Position));
        boundaries.AddRange(items.Where(t => t.Kind == TriviaKind.Comment).Select(t => t.Position));
        boundaries.Sort();
        codeTokens.Sort((a, b) => a.Position.CompareTo(b.Position));
        var codeLines = codeTokens.Select(t => t.Line).Distinct().OrderBy(l => l).ToArray();
        return new TriviaCursor(items, source, codeTokens, boundaries, codeLines,
            LiteralSpans.LinesStartingInside(source, literals));

        void Add(IReadOnlyList<Trivia>? trivia)
        {
            if (trivia == null)
                return;
            foreach (var t in trivia)
            {
                if (t.Kind == TriviaKind.Comment && InsideLiteral(t.Position))
                    continue;
                items.Add(t);
            }
        }

        bool InsideLiteral(int position)
        {
            // The spans are in source order and non-overlapping: the last one starting before
            // the position is the only one that can contain it.
            int lo = 0, hi = literals.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (literals[mid].Start < position)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo > 0 && position < literals[lo - 1].End;
        }
    }

    /// <summary>The trivia the parser attached to statements (leading and trailing), in source order.</summary>
    public static TriviaCursor FromAst(Node root)
    {
        var items = new List<Trivia>();
        var seen = new HashSet<Trivia>(ReferenceEqualityComparer.Instance);
        Collect(root);
        // Stable: synthetic trivia without positions keeps its tree order.
        var ordered = items.Select((t, i) => (t, i)).OrderBy(p => p.t.Position).ThenBy(p => p.i).Select(p => p.t).ToList();
        return new TriviaCursor(ordered, null, null, null, null, null);

        void Collect(Node node)
        {
            if (node is Statement)
            {
                foreach (var t in node.LeadingTrivia ?? Array.Empty<Trivia>())
                    if (seen.Add(t))
                        items.Add(t);
                foreach (var t in node.TrailingTrivia ?? Array.Empty<Trivia>())
                    if (seen.Add(t))
                        items.Add(t);
            }

            foreach (var child in node.GetChildNodes())
                Collect(child);
        }
    }

    /// <summary>True when the cursor reads the token stream (body-end, docstring and verbatim facts are available).</summary>
    public bool HasSource => _source != null;

    /// <summary>The source text the tokens were lexed from (null for an AST-built cursor).</summary>
    public string? Source => _source;

    /// <summary>Everything before <paramref name="line"/>: own-line comments and blank lines — and an inline comment no anchor claimed, which is then written on its own line rather than dropped.</summary>
    public List<Trivia> TakeBefore(int line)
    {
        var taken = new List<Trivia>();
        while (_head < _items.Count && _items[_head].Line < line)
            taken.Add(_items[_head++]);
        return taken;
    }

    /// <summary>The comments on lines [<paramref name="lineStart"/>, <paramref name="lineEnd"/>] other than the inline comment that ends line <paramref name="lineEnd"/> — a header's INNER comments.</summary>
    public List<Trivia> TakeInner(int lineStart, int lineEnd)
    {
        var taken = new List<Trivia>();
        while (_head < _items.Count)
        {
            var t = _items[_head];
            if (t.Line < lineStart || t.Line > lineEnd || (t.IsInline && t.Line == lineEnd))
                break;
            taken.Add(t);
            _head++;
        }
        return taken;
    }

    /// <summary>The inline comment(s) ending line <paramref name="line"/>.</summary>
    public List<Trivia> TakeInline(int line)
    {
        var taken = new List<Trivia>();
        while (_head < _items.Count && _items[_head].IsInline && _items[_head].Line == line)
            taken.Add(_items[_head++]);
        return taken;
    }

    /// <summary>
    /// A body's end: the own-line comments at the head whose column is at or right of the body's
    /// <paramref name="column"/> and that precede the first code after the body's last line
    /// <paramref name="lastLine"/> (blank lines between them included). Nested bodies close
    /// innermost first, so each takes the comments written at its own indent. Empty for an
    /// AST-built cursor.
    /// </summary>
    public List<Trivia> TakeBodyEnd(int column, int lastLine)
    {
        var taken = new List<Trivia>();
        if (_codeLines == null || column <= 0)
            return taken;
        var nextCode = NextCodeLine(lastLine);
        var committed = _head;
        for (var j = _head; j < _items.Count; j++)
        {
            var t = _items[j];
            if (t.Line >= nextCode)
                break;
            if (t.Kind == TriviaKind.BlankLines)
                continue;
            if (t.IsInline || t.Column < column)
                break;
            committed = j + 1;
        }

        while (_head < committed)
            taken.Add(_items[_head++]);
        return taken;
    }

    /// <summary>Everything left (end of module).</summary>
    public List<Trivia> TakeAll()
    {
        var taken = new List<Trivia>();
        while (_head < _items.Count)
            taken.Add(_items[_head++]);
        return taken;
    }

    /// <summary>True when an item is still pending.</summary>
    public bool HasPending => _head < _items.Count;

    private int NextCodeLine(int afterLine)
    {
        var lines = _codeLines!;
        int lo = 0, hi = lines.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (lines[mid] <= afterLine)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo < lines.Length ? lines[lo] : int.MaxValue;
    }

    /// <summary>
    /// The line range of the docstring that is the first code after <paramref name="afterOffset"/>
    /// (a header's end, or 0 for the module's docstring).
    /// </summary>
    public bool TryFindDocString(int afterOffset, out int startLine, out int endLine)
    {
        startLine = endLine = 0;
        if (_codeTokens == null || _source == null)
            return false;
        var token = _codeTokens.FirstOrDefault(t => t.Position >= afterOffset);
        if (token == null || token.Type is not (TokenType.String or TokenType.RawString))
            return false;
        startLine = token.Line;
        endLine = token.Line + CountLineBreaks(token.Position, Math.Min(_source.Length, token.Position + token.Length));
        return true;
    }

    /// <summary>
    /// The exclusive source offset where the code ending at <paramref name="parserEnd"/> really
    /// ends. A span the parser computed from a token's length is not a source offset for every
    /// token — a numeric literal's <c>Value</c> is normalised (<c>.5</c> reads <c>0.5</c>,
    /// <c>1_000</c> reads <c>1000</c>) and it records no source length — so the end is re-derived
    /// from the source: the last code token starting before <paramref name="parserEnd"/> ends where
    /// the next token or comment begins, less the whitespace between them.
    /// </summary>
    public int SourceEnd(int parserEnd)
    {
        if (_codeTokens == null || _boundaries == null || _source == null)
            return parserEnd;
        Token? last = null;
        foreach (var t in _codeTokens)
        {
            if (t.Position >= parserEnd)
                break;
            last = t;
        }
        if (last == null)
            return parserEnd;

        var next = _source.Length;
        foreach (var b in _boundaries)
        {
            if (b > last.Position)
            {
                next = Math.Min(next, b);
                break;
            }
        }

        var end = next;
        while (end > last.Position + 1 && char.IsWhiteSpace(_source[end - 1]))
            end--;
        return end;
    }

    /// <summary>
    /// The 1-based line on which the code ending at <paramref name="parserEnd"/> really ends (see
    /// <see cref="SourceEnd"/>), or 0 for an AST-built cursor. A header's end LINE is derived from
    /// its end OFFSET: several statement kinds record <c>LineEnd</c> as the line of the token after
    /// the statement (the next statement's), so a line field cannot bound the header.
    /// </summary>
    public int EndLineOf(int parserEnd)
    {
        if (_lineStarts == null || parserEnd <= 0)
            return 0;
        var last = Math.Max(0, SourceEnd(parserEnd) - 1);
        int lo = 0, hi = _lineStarts.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_lineStarts[mid] <= last)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    /// <summary>The 0-based column of <paramref name="offset"/> in its source line.</summary>
    public int ColumnOf(int offset)
    {
        var lineStart = offset;
        while (lineStart > 0 && _source![lineStart - 1] != '\n' && _source[lineStart - 1] != '\r')
            lineStart--;
        return offset - lineStart;
    }

    /// <summary>True when source line <paramref name="line"/> (1-based) starts inside a string or f-/t-string literal.</summary>
    public bool LineStartsInsideLiteral(int line) => _linesStartingInsideLiteral?.Contains(line) == true;

    private int CountLineBreaks(int start, int end)
    {
        var count = 0;
        for (var i = start; i < end; i++)
        {
            var c = _source![i];
            if (c == '\n')
                count++;
            else if (c == '\r')
            {
                count++;
                if (i + 1 < end && _source[i + 1] == '\n')
                    i++;
            }
        }
        return count;
    }
}
