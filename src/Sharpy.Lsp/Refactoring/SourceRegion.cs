using System.Runtime.CompilerServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Text;
using AstNode = Sharpy.Compiler.Parser.Ast.Node;
using CompilerLexer = Sharpy.Compiler.Lexer.Lexer;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using Token = Sharpy.Compiler.Lexer.Token;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// The ONE line table every code-action provider reads (P47, #2232, plan D1). It is built from
/// <see cref="LineDiff.Split"/> — the splitter the formatting routes were single-sourced onto (#2168) —
/// so a provider counts lines exactly as the client, the workspace and the lexer do: <c>\r\n</c>,
/// <c>\n</c> and a lone <c>\r</c> each end a line. A provider holds no line splitting of its own and
/// writes <see cref="DocumentLineBreak"/> wherever it writes a line break.
///
/// <para><b>Line numbers are the compiler's 1-based numbers</b> (an AST node's <c>LineStart</c>); the
/// LSP line of line <c>n</c> is <c>n − 1</c>. Line contents never include their line break.</para>
///
/// <para>The comment trivia, the multi-line-literal lines and the tokens <see cref="Extent.EndLine"/>
/// reads come from ONE lex of the text (<see cref="Tokens"/>, trivia preserved), made on first use.
/// Instances are cached per text instance (<see cref="Of"/>), so the providers answering one request —
/// which all receive the same document string — share one table and one lex.</para>
/// </summary>
internal sealed class SourceLines
{
    private static readonly ConditionalWeakTable<string, SourceLines> Cache = new();

    private readonly List<string> _lines;
    private readonly List<string> _breaks;
    private readonly int[] _starts;
    private readonly Lazy<LexFacts> _lex;
    private readonly Lazy<string> _indentUnit;

    private SourceLines(string text)
    {
        Text = text;
        (_lines, _breaks) = LineDiff.Split(text);
        DocumentLineBreak = LineDiff.DocumentLineBreak(text);
        _starts = new int[_lines.Count];
        var offset = 0;
        for (var i = 0; i < _lines.Count; i++)
        {
            _starts[i] = offset;
            offset += _lines[i].Length + (i < _breaks.Count ? _breaks[i].Length : 0);
        }

        _lex = new Lazy<LexFacts>(() => LexFacts.Of(text), LazyThreadSafetyMode.ExecutionAndPublication);
        _indentUnit = new Lazy<string>(DetectIndentUnit, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>The line table of <paramref name="text"/> (one instance per text instance).</summary>
    public static SourceLines Of(string text) => Cache.GetValue(text, static t => new SourceLines(t));

    /// <summary>The document text the table was built from.</summary>
    public string Text { get; }

    /// <summary>Every line's content, without its break; index <c>n − 1</c> is line <c>n</c>.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>The line breaks between the lines (<c>Breaks.Count == Lines.Count − 1</c>).</summary>
    public IReadOnlyList<string> Breaks => _breaks;

    /// <summary>The break a provider writes: the document's first, <c>\n</c> when it has none.</summary>
    public string DocumentLineBreak { get; }

    /// <summary>The number of lines (a text with <c>k</c> breaks has <c>k + 1</c>).</summary>
    public int Count => _lines.Count;

    /// <summary>True when <paramref name="line"/> (1-based) is a line of the document.</summary>
    public bool Contains(int line) => line >= 1 && line <= _lines.Count;

    /// <summary>The content of line <paramref name="line"/> (1-based), without its break.</summary>
    public string Line(int line) => _lines[line - 1];

    /// <summary>The 0-based offset of the first character of line <paramref name="line"/> (1-based).</summary>
    public int StartOffset(int line) => _starts[line - 1];

    /// <summary>The 1-based line containing the 0-based <paramref name="offset"/> (a break belongs to the line it ends).</summary>
    public int LineOfOffset(int offset)
    {
        var index = Array.BinarySearch(_starts, offset);
        return (index >= 0 ? index : ~index - 1) + 1;
    }

    /// <summary>The leading spaces and tabs of line <paramref name="line"/>; <c>""</c> outside the document.</summary>
    public string Indentation(int line)
    {
        if (!Contains(line))
            return "";
        var content = _lines[line - 1];
        var i = 0;
        while (i < content.Length && (content[i] == ' ' || content[i] == '\t'))
            i++;
        return content[..i];
    }

    /// <summary>True when line <paramref name="line"/> holds only whitespace (or is outside the document).</summary>
    public bool IsBlank(int line) => !Contains(line) || string.IsNullOrWhiteSpace(_lines[line - 1]);

    /// <summary>
    /// The document's indent unit: a tab when the first indented line starts with one, else that line's
    /// leading spaces (up to eight); four spaces when no line is indented.
    /// </summary>
    public string IndentUnit => _indentUnit.Value;

    /// <summary>The tokens of one lex of the text, comment trivia preserved (empty if the lexer threw).</summary>
    public IReadOnlyList<Token> Tokens => _lex.Value.Tokens;

    /// <summary>
    /// The 1-based lines that start strictly inside a multi-line literal (<see cref="LiteralSpans"/>, the
    /// formatter's and the indent-only fallback's rule): their leading whitespace is the program's data.
    /// </summary>
    public IReadOnlySet<int> LiteralLines => _lex.Value.LiteralLines;

    /// <summary>
    /// The inline comment ending line <paramref name="line"/> (<see cref="Trivia.IsInline"/>: a comment after
    /// code on that line), or null when the line has none.
    /// </summary>
    public Trivia? InlineComment(int line) => _lex.Value.InlineComments.TryGetValue(line, out var trivia) ? trivia : null;

    /// <summary>
    /// True when <paramref name="node"/> is the first code on its line — nothing but indentation before it. A
    /// statement that shares its line with its block's header (<c>def f(self) -&gt; int: ...</c>, the stub body
    /// <c>...</c>) has no whole lines of its own to move.
    /// </summary>
    public bool BeginsItsLine(AstNode node) =>
        Contains(node.LineStart) && node.ColumnStart >= 1
        && string.IsNullOrWhiteSpace(_lines[node.LineStart - 1][..Math.Min(_lines[node.LineStart - 1].Length, node.ColumnStart - 1)]);

    /// <summary>True when a comment starts inside the 0-based offsets <c>[start, end)</c> — text a provider would copy or drop with it.</summary>
    public bool HasCommentIn(int start, int end) => _lex.Value.Comments.Any(c => c.Position >= start && c.Position < end);

    /// <summary>Every comment on lines <paramref name="first"/>..<paramref name="last"/> (1-based), in source order.</summary>
    public IReadOnlyList<Trivia> CommentsOn(int first, int last) =>
        _lex.Value.Comments.Where(c => c.Line >= first && c.Line <= last).ToList();

    /// <summary>
    /// The inline comment of line <paramref name="line"/> as it is written there — the whitespace that separates
    /// it from the code, then its text — for re-attaching to a re-spelled header; <c>""</c> when the line has none.
    /// </summary>
    public string AttachedComment(int line)
    {
        if (InlineComment(line) is not { } comment || !Contains(line))
            return "";
        var content = _lines[line - 1];
        var before = content[..Math.Min(content.Length, Math.Max(0, comment.Column - 1))];
        return before[before.TrimEnd(' ', '\t').Length..] + comment.Text;
    }

    /// <summary>The true last line of <paramref name="node"/> — <see cref="Extent.EndLine"/> over this table's tokens.</summary>
    public int EndLine(AstNode node) => Extent.EndLine(node, Tokens, this);

    /// <summary>Lines <paramref name="first"/>..<paramref name="last"/> (1-based, inclusive), verbatim.</summary>
    public Region Slice(int first, int last) => Region.Slice(this, first, last);

    /// <summary>
    /// The source text of <paramref name="node"/>, from its start position to its end position, verbatim
    /// (its own line breaks kept); null when either position is not on the document's lines.
    /// </summary>
    public string? NodeText(AstNode node) =>
        NodeSpan(node) is { } span ? Text.Substring(span.Start, span.End - span.Start) : null;

    /// <summary>
    /// The 0-based document offsets <c>[Start, End)</c> of <paramref name="node"/>; null when either position is
    /// not on the document's lines. An end column past its line's content is accepted only when it addresses
    /// the true end of a string literal that spans lines (the parser's <c>ColumnEnd = Column + Length</c> on the
    /// token's START line, #2295) — any other overshoot (a token whose length is not its source length, e.g. a
    /// normalized <c>.5</c>) is refused rather than read into the line break.
    /// </summary>
    public (int Start, int End)? NodeSpan(AstNode node)
    {
        if (node.LineStart == 0 || node.LineEnd == 0 || !Contains(node.LineStart) || !Contains(node.LineEnd))
            return null;

        var startCol = node.ColumnStart - 1;
        var endCol = node.ColumnEnd - 1;
        var startLine = _lines[node.LineStart - 1];
        var endLine = _lines[node.LineEnd - 1];
        if (startCol < 0 || startCol > startLine.Length || endCol < 0)
            return null;

        var start = StartOffset(node.LineStart) + startCol;
        var end = StartOffset(node.LineEnd) + endCol;
        if (endCol > endLine.Length && !EndsAMultiLineString(start, end, node.LineEnd))
            return null;
        if (end < start || end > Text.Length)
            return null;

        // The span ends at the node's last CODE character: a block-form expression (a `match` whose last arm
        // ends a line) records its end at the NEXT line's column 1, and a span through it would take the line
        // break — and any trailing comment — with the expression (P47, #2232).
        var codeEnd = -1;
        foreach (var token in Tokens)
        {
            if (token.Position >= end)
                break;
            if (token.Position < start || token.Length <= 0
                || token.Type is TokenType.Newline or TokenType.Indent or TokenType.Dedent or TokenType.Eof)
                continue;
            codeEnd = Math.Max(codeEnd, Math.Min(token.Position + token.Length, end));
        }

        if (codeEnd > start && codeEnd < end)
            end = codeEnd;
        return (start, end);
    }

    /// <summary>The LSP position (0-based line, UTF-16 column) of a 0-based document offset.</summary>
    public Position PositionOf(int offset)
    {
        var line = LineOfOffset(offset);
        return new Position(line - 1, offset - StartOffset(line));
    }

    /// <summary>
    /// True when <paramref name="end"/> is the end offset of a string literal token of the node that starts on
    /// <paramref name="lineEnd"/> and whose last character lies on a later line.
    /// </summary>
    private bool EndsAMultiLineString(int start, int end, int lineEnd)
    {
        foreach (var token in Tokens)
        {
            if (token.Type is not (TokenType.String or TokenType.RawString or TokenType.ByteString))
                continue;
            if (token.Position >= start && token.Line == lineEnd && token.Length > 0 && token.Position + token.Length == end
                && end <= Text.Length && LineOfOffset(end - 1) > lineEnd)
                return true;
        }

        return false;
    }

    /// <summary>
    /// The edit that replaces lines <paramref name="first"/>..<paramref name="last"/> (1-based, whole lines)
    /// with <paramref name="newLines"/>, each written with the document's break (plan D2): the range is
    /// <c>(first − 1, 0)</c> to <c>(last, 0)</c>. When <paramref name="last"/> is the document's final line
    /// (no break follows it) the range ends at that line's end and the text gets no final break; an empty
    /// <paramref name="newLines"/> there deletes the preceding line's break instead, so no blank line is left.
    /// </summary>
    public TextEdit WholeLineEdit(int first, int last, IReadOnlyList<string> newLines)
    {
        if (!Contains(first) || !Contains(last) || last < first)
            throw new ArgumentOutOfRangeException(nameof(first), $"lines {first}..{last} are not lines of a {Count}-line document");

        var lineBreak = DocumentLineBreak;
        if (last < Count)
        {
            return new TextEdit
            {
                Range = new LspRange(new Position(first - 1, 0), new Position(last, 0)),
                NewText = newLines.Count == 0 ? "" : string.Join(lineBreak, newLines) + lineBreak,
            };
        }

        var endOfLast = new Position(last - 1, _lines[last - 1].Length);
        if (newLines.Count > 0)
            return new TextEdit { Range = new LspRange(new Position(first - 1, 0), endOfLast), NewText = string.Join(lineBreak, newLines) };

        var start = first > 1 ? new Position(first - 2, _lines[first - 2].Length) : new Position(0, 0);
        return new TextEdit { Range = new LspRange(start, endOfLast), NewText = "" };
    }

    private string DetectIndentUnit()
    {
        // Only a line that STARTS a logical line carries block indentation: a continuation line inside
        // brackets (`f(  # c⏎ b)`) or a line inside a multi-line string may sit at any column (P47, #2232).
        var tokens = Tokens;
        var logicalStarts = new HashSet<int>();
        var atStart = true;
        foreach (var token in tokens)
        {
            if (token.Type is TokenType.Newline or TokenType.Indent or TokenType.Dedent)
            {
                atStart = true;
                continue;
            }

            if (token.Type == TokenType.Eof)
                break;
            if (atStart)
                logicalStarts.Add(token.Line);
            atStart = false;
        }

        const int maxDetectableIndent = 8;
        for (var i = 0; i < _lines.Count; i++)
        {
            // A document that does not lex clean (a tab or 2-space indent is refused, SPY0012/SPY0013) keeps the
            // plain first-indented-line reading.
            if (_lex.Value.LexedClean && !logicalStarts.Contains(i + 1))
                continue;
            var content = _lines[i];
            if (content.Length > 0 && content[0] == '\t')
                return "\t";

            var spaces = 0;
            while (spaces < content.Length && content[spaces] == ' ')
                spaces++;

            if (spaces > 0 && spaces <= maxDetectableIndent)
                return new string(' ', spaces);
        }

        return "    ";
    }

    /// <summary>The facts of one trivia-preserving lex of the document.</summary>
    private sealed record LexFacts(
        IReadOnlyList<Token> Tokens,
        IReadOnlySet<int> LiteralLines,
        IReadOnlyDictionary<int, Trivia> InlineComments,
        IReadOnlyList<Trivia> Comments,
        bool LexedClean)
    {
        public static LexFacts Of(string text)
        {
            // FileCompilationPipeline.Lex's lexer, built here so its recovery resumes reach LiteralSpans
            // (an f-string abandoned by error recovery must not pair with the next one, P22g).
            var lexer = new CompilerLexer(new SourceText(text, "<code-action>"), NullLogger.Instance, preserveTrivia: true);
            IReadOnlyList<Token> tokens;
            try
            {
                tokens = lexer.TokenizeAll();
            }
            catch (Exception)
            {
                // no tokens: no literal, comment or token extent is known
                return new LexFacts(Array.Empty<Token>(), new HashSet<int>(), new Dictionary<int, Trivia>(), Array.Empty<Trivia>(), false);
            }

            var literalLines = LiteralSpans.LinesStartingInside(text, LiteralSpans.Of(tokens, lexer.RecoveryResumes));
            var inline = new Dictionary<int, Trivia>();
            var comments = new List<Trivia>();
            foreach (var token in tokens)
            {
                Add(token.LeadingTrivia);
                Add(token.TrailingTrivia);
            }

            comments.Sort((a, b) => a.Position.CompareTo(b.Position));
            return new LexFacts(tokens, literalLines, inline, comments, !lexer.Diagnostics.HasErrors);

            void Add(IReadOnlyList<Trivia>? trivia)
            {
                if (trivia == null)
                    return;
                foreach (var t in trivia)
                {
                    if (t.Kind != TriviaKind.Comment)
                        continue;
                    comments.Add(t);
                    if (t.IsInline)
                        inline.TryAdd(t.Line, t);
                }
            }
        }
    }
}

/// <summary>
/// The true extent of a node in the source (P47, #2232, plan D1).
/// </summary>
internal static class Extent
{
    /// <summary>
    /// The 1-based last line of <paramref name="node"/>: <c>max(node.LineEnd, …)</c> over the end line of every
    /// one of the node's own tokens — a token at or after the node's start offset, starting on a line no later
    /// than <c>LineEnd</c> (and, on <c>LineEnd</c> itself, before <c>ColumnEnd</c>) — whose last character
    /// (<c>Position + Length − 1</c>, <see cref="Token.SourceLength"/> for a literal) lies on a later line.
    ///
    /// <para>It compensates for ONE parser fact: a node whose last token spans lines (a triple-quoted string,
    /// <c>x = """a⏎b"""</c>) records <c>LineEnd = Previous.Line</c>, the token's START line, so a provider
    /// slicing through <c>LineEnd</c> leaves the literal's tail lines behind. The rule is monotone and reads
    /// only the node's own tokens, so once the parser records the true end it returns <c>LineEnd</c> unchanged.
    /// TODO(#2295): an identity once the parser's LineEnd/ColumnEnd for a multi-line last token is fixed —
    /// then inline this to <c>node.LineEnd</c>.</para>
    ///
    /// <para>For a STATEMENT a second parser fact matters: a statement that ends a block (a bodiless
    /// <c>def f(self) -&gt; int: ...</c> in an interface, a class field) records a <c>LineEnd</c> that runs on to
    /// the next statement's line — trusting it would slice or delete that statement. So a statement's last line
    /// is read from its own tokens alone (<see cref="StatementEndLine"/>): through the depth-0 <c>Newline</c> that
    /// ends its logical line, or — for a compound statement — through the <c>Dedent</c> that closes its body.</para>
    /// </summary>
    public static int EndLine(AstNode node, IReadOnlyList<Token> tokens, SourceLines lines)
    {
        if (node is Sharpy.Compiler.Parser.Ast.DecoratedStatement decorated)
            return EndLine(decorated.Statement, tokens, lines);
        if (node is Sharpy.Compiler.Parser.Ast.Statement statement && StatementEndLine(statement, tokens, lines) is { } statementEnd)
            return statementEnd;

        var end = node.LineEnd;
        if (!lines.Contains(node.LineStart) || node.ColumnStart < 1)
            return end;

        var startOffset = lines.StartOffset(node.LineStart) + node.ColumnStart - 1;
        foreach (var token in tokens)
        {
            if (token.Position < startOffset || token.Length <= 0)
                continue;
            if (token.Line > node.LineEnd)
                break;
            if (token.Type is TokenType.Newline or TokenType.Indent or TokenType.Dedent or TokenType.Eof)
                continue;
            if (token.Line == node.LineEnd && node.ColumnEnd > 0 && token.Column >= node.ColumnEnd)
                continue;

            var last = Math.Min(token.Position + token.Length - 1, lines.Text.Length - 1);
            var tokenEndLine = lines.LineOfOffset(last);
            if (tokenEndLine > end)
                end = tokenEndLine;
        }

        return end;
    }

    /// <summary>
    /// The last line of <paramref name="statement"/> read from its tokens: from its first token, through the
    /// bracket-depth-0 <c>Newline</c> that ends its logical line (unless an <c>Indent</c> follows — a compound
    /// statement's header), or the <c>Dedent</c> that closes the body it opened; the line of the last character
    /// of the last code token before that point (a multi-line literal's last line). Null when the statement's
    /// first token is not found.
    /// </summary>
    private static int? StatementEndLine(Sharpy.Compiler.Parser.Ast.Statement statement, IReadOnlyList<Token> tokens, SourceLines lines) =>
        StatementEndOffset(statement, tokens, lines) is { } endOffset ? lines.LineOfOffset(Math.Max(0, endOffset - 1)) : null;

    /// <summary>
    /// The 0-based offset just past <paramref name="statement"/>'s last code character, read from its tokens as
    /// <see cref="StatementEndLine"/> reads its last line — trailing whitespace and a trailing comment excluded,
    /// a decorated statement through its declaration. Null when the statement's first token is not found. A
    /// selection COVERS a statement exactly when it starts at or before the statement's first character and ends
    /// at or after this offset (<see cref="SelectionAnalyzer"/>).
    /// </summary>
    public static int? StatementEndOffset(Sharpy.Compiler.Parser.Ast.Statement statement, IReadOnlyList<Token> tokens, SourceLines lines)
    {
        if (statement is Sharpy.Compiler.Parser.Ast.DecoratedStatement decorated)
            return StatementEndOffset(decorated.Statement, tokens, lines);

        if (!lines.Contains(statement.LineStart) || statement.ColumnStart < 1)
            return null;

        var startOffset = lines.StartOffset(statement.LineStart) + statement.ColumnStart - 1;
        var first = -1;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Position >= startOffset && tokens[i].Type is not (TokenType.Indent or TokenType.Dedent or TokenType.Newline))
            {
                first = i;
                break;
            }
        }

        if (first < 0 || tokens[first].Position != startOffset)
            return null;

        int? end = null;
        var depth = 0;
        var blocks = 0;
        for (var i = first; i < tokens.Count; i++)
        {
            var token = tokens[i];
            switch (token.Type)
            {
                case TokenType.Eof:
                    return end;
                case TokenType.Indent:
                    blocks++;
                    continue;
                case TokenType.Dedent:
                    blocks--;
                    if (blocks <= 0 && !ContinuesWithClause(tokens, i + 1))
                        return end;
                    continue;
                case TokenType.Newline:
                    if (depth <= 0 && blocks == 0 && (i + 1 >= tokens.Count || tokens[i + 1].Type != TokenType.Indent))
                        return end;
                    continue;
                case TokenType.LeftParen or TokenType.LeftBracket or TokenType.LeftBrace:
                    depth++;
                    break;
                case TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace:
                    depth--;
                    break;
            }

            if (token.Length <= 0 || token.Position < 0)
                continue;
            var tokenEnd = Math.Min(token.Position + token.Length, lines.Text.Length);
            if (end is null || tokenEnd > end)
                end = tokenEnd;
        }

        return end;
    }

    /// <summary>
    /// True when the token right after the Dedent that closed the statement's body begins a further clause of
    /// the SAME statement (another Dedent first means the clause belongs to an enclosing statement).
    /// </summary>
    private static bool ContinuesWithClause(IReadOnlyList<Token> tokens, int index) =>
        index < tokens.Count && tokens[index].Type is TokenType.Elif or TokenType.Else or TokenType.Except or TokenType.Finally;
}

/// <summary>
/// A run of whole source lines, verbatim (P47, #2232, plan D1): trailing comments, own-line comments and
/// blank lines included. <see cref="First"/>/<see cref="Last"/> are 1-based document lines.
/// </summary>
internal sealed record Region(int First, int Last, IReadOnlyList<string> Lines)
{
    /// <summary>Lines <paramref name="first"/>..<paramref name="last"/> (1-based, inclusive) of <paramref name="source"/>, verbatim.</summary>
    public static Region Slice(SourceLines source, int first, int last)
    {
        if (!source.Contains(first) || !source.Contains(last) || last < first)
            throw new ArgumentOutOfRangeException(nameof(first), $"lines {first}..{last} are not lines of a {source.Count}-line document");

        var lines = new string[last - first + 1];
        for (var i = 0; i < lines.Length; i++)
            lines[i] = source.Line(first + i);
        return new Region(first, last, lines);
    }

    /// <summary>
    /// The region's lines with one uniform indentation change: every non-blank line's leading whitespace
    /// grows by <paramref name="delta"/> characters of <paramref name="fill"/> (a positive delta) or loses
    /// up to <c>|delta|</c> leading spaces/tabs (a negative one — a line indented less than that ends at
    /// column 0). A blank line and a line in <paramref name="literalLines"/> (1-based document lines that
    /// start inside a multi-line literal, <see cref="SourceLines.LiteralLines"/>) are byte-identical: shifting
    /// a literal line changes the string's value (plan D3).
    /// </summary>
    public static IReadOnlyList<string> Reindent(Region region, int delta, IReadOnlySet<int> literalLines, char fill = ' ')
    {
        var result = new string[region.Lines.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var line = region.Lines[i];
            if (delta == 0 || string.IsNullOrWhiteSpace(line) || literalLines.Contains(region.First + i))
            {
                result[i] = line;
            }
            else if (delta > 0)
            {
                result[i] = new string(fill, delta) + line;
            }
            else
            {
                var strip = 0;
                while (strip < -delta && strip < line.Length && (line[strip] == ' ' || line[strip] == '\t'))
                    strip++;
                result[i] = line[strip..];
            }
        }

        return result;
    }

    /// <summary>The region's lines joined by <paramref name="lineBreak"/> (no break after the last).</summary>
    public string Join(string lineBreak) => string.Join(lineBreak, Lines);
}
