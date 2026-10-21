using FluentAssertions;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;

namespace Sharpy.Compiler.Tests.Lexer;

public partial class LiteralLossScannerDifferentialTests
{
    /// <summary>
    /// The facts a CLEAN lex determines about each line of a document (P68 Phase 3, #2300 row C5): the oracle the
    /// injection differential judges the literal-loss scanner's verdicts on dropped text against. Every fact is read
    /// off the clean lexer's own tokens and <see cref="LexerNs.LiteralSpans.Of"/>, never off a second hand model of
    /// the text, so a scanner arm that disagrees with the lexer cannot agree with this oracle by sharing its mistake.
    /// <list type="bullet">
    /// <item><see cref="OpenerCrossing"/> — a literal span starts in <c>[at, lineEnd)</c> and ends past the line's
    /// break (the truth behind <see cref="LexerNs.LiteralLoss.DroppedOpener"/>).</item>
    /// <item><see cref="Continued"/> — a real bracket (f-string contents excluded) is open at the line's end, or the
    /// line ends in a backslash continuation (no <c>Newline</c> token at its end at depth 0; the backslash is checked
    /// against the text — an inconsistency throws rather than guessing).</item>
    /// <item><see cref="BracketOpenerLine"/> — the 1-based line of the outermost real bracket open at the line's end.</item>
    /// <item><see cref="ColonOnLogicalLine"/> — a <c>Colon</c> token at bracket depth 0 outside every f-string on the
    /// logical line containing the line, up to its end (<c>:=</c> is a different token).</item>
    /// <item><see cref="TokenBefore"/> — a token of that logical line was read before an offset (an abort there).</item>
    /// </list>
    /// <b>Domain restriction</b> (/verify-plan, Dimension 4): <see cref="Continued"/>, <see cref="BracketOpenerLine"/>
    /// and <see cref="PossibleHeader"/> are clean facts only for a line whose END is not inside a literal span — a line
    /// where a multi-line literal opens also ends with no <c>Newline</c> at bracket depth 0, which the backslash reading
    /// would misjudge. They THROW outside the domain, so a caller cannot assert them there by accident; on such a line
    /// only <see cref="OpenerCrossing"/> is a clean fact, and the R-FR/R-FV rules (<c>LiteralStateTests</c>) own the rest.
    /// </summary>
    internal sealed class CleanLineFacts
    {
        private readonly LexerNs.Token[] _tokens;          // non-synthetic tokens (Position ≥ 0), in source order
        private readonly int[] _logical;                   // the logical line of each token: Newline tokens before it
        private readonly bool[] _inFString;                // inside an f-/t-string (FStringStart … FStringEnd), the delimiters included
        private readonly int[] _depthBefore;               // real bracket depth before each token
        private readonly int[] _depthAfter;                // ... and after it
        private readonly int[] _outermostAfter;            // 1-based line of the outermost real bracket open after it, or 0
        private readonly int[] _lastBeforeEnd;             // per line: the last token index with Position < lineEnd, or -1
        private readonly HashSet<int> _newlinePositions = new();

        public string Source { get; }

        /// <summary>The lexer's literal spans (<see cref="LexerNs.LiteralSpans.Of"/>), the truth every literal fact reads.</summary>
        public IReadOnlyList<(int Start, int End)> Spans { get; }

        /// <summary>Each line's <c>[start, end)</c>, its break excluded (<see cref="LineRanges"/>).</summary>
        public IReadOnlyList<(int Start, int End)> Lines { get; }

        public IReadOnlyList<LexerNs.Token> Tokens => _tokens;

        private CleanLineFacts(string source, List<LexerNs.Token> tokens)
        {
            Source = source;
            Spans = LexerNs.LiteralSpans.Of(tokens);
            Lines = LineRanges(source);
            _tokens = tokens.Where(t => t.Position >= 0).ToArray();
            var n = _tokens.Length;
            _logical = new int[n];
            _inFString = new bool[n];
            _depthBefore = new int[n];
            _depthAfter = new int[n];
            _outermostAfter = new int[n];

            var logical = 0;
            var fstring = 0;
            var brackets = new List<int>();   // opener lines, outermost first
            for (var i = 0; i < n; i++)
            {
                var token = _tokens[i];
                _logical[i] = logical;
                _depthBefore[i] = brackets.Count;
                if (token.Type == TokenType.FStringStart)
                    fstring++;
                _inFString[i] = fstring > 0;
                if (token.Type == TokenType.FStringEnd && fstring > 0)
                    fstring--;
                else if (fstring == 0 && token.Type is TokenType.LeftParen or TokenType.LeftBracket or TokenType.LeftBrace)
                    brackets.Add(token.Line);
                else if (fstring == 0 && token.Type is TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace && brackets.Count > 0)
                    brackets.RemoveAt(brackets.Count - 1);
                _depthAfter[i] = brackets.Count;
                _outermostAfter[i] = brackets.Count > 0 ? brackets[0] : 0;
                if (token.Type == TokenType.Newline)
                {
                    logical++;
                    _newlinePositions.Add(token.Position);
                }
            }

            _lastBeforeEnd = new int[Lines.Count];
            var t = 0;
            for (var k = 0; k < Lines.Count; k++)
            {
                while (t < n && _tokens[t].Position < Lines[k].End)
                    t++;
                _lastBeforeEnd[k] = t - 1;
            }
        }

        /// <summary>The facts of <paramref name="source"/>, or null when it does not lex clean (no oracle there).</summary>
        public static CleanLineFacts? Of(string source)
        {
            var lexer = new LexerNs.Lexer(source);
            var tokens = lexer.TokenizeAll();
            return lexer.Diagnostics.HasErrors ? null : new CleanLineFacts(source, tokens);
        }

        /// <summary>The length of line <paramref name="k"/>'s break: 2 for <c>\r\n</c>, 1 for <c>\n</c> or <c>\r</c>, 0 at the end of the source.</summary>
        public int LineBreakLength(int k)
        {
            var end = Lines[k].End;
            if (end >= Source.Length)
                return 0;
            return Source[end] == '\r' && end + 1 < Source.Length && Source[end + 1] == '\n' ? 2 : 1;
        }

        private static bool IsCode(LexerNs.Token token)
            => token.Type is not (TokenType.Indent or TokenType.Dedent or TokenType.Newline or TokenType.Eof);

        /// <summary>A literal span crosses line <paramref name="k"/>'s break.</summary>
        public bool EndInsideLiteral(int k)
        {
            var end = Lines[k].End;
            return Spans.Any(s => s.Start < end && s.End > end);
        }

        /// <summary>Line <paramref name="k"/> starts inside a literal span (a close line or an interior line of a literal that spans lines).</summary>
        public bool StartsInsideLiteral(int k)
        {
            var start = Lines[k].Start;
            return Spans.Any(s => s.Start < start && s.End > start);
        }

        /// <summary>The CLOSE line of a literal that spanned lines: a span that started on an earlier line ends on line <paramref name="k"/>.</summary>
        public bool IsCloseLine(int k)
        {
            var (start, end) = Lines[k];
            return Spans.Any(s => s.Start < start && s.End > start && s.End <= end);
        }

        /// <summary>A literal span starts in <c>[at, lineEnd)</c> of line <paramref name="k"/> and ends past its break.</summary>
        public bool OpenerCrossing(int k, int at)
        {
            var end = Lines[k].End;
            return Spans.Any(s => s.Start >= at && s.Start < end && s.End > end);
        }

        /// <summary>Line <paramref name="k"/> holds a code token (not <c>Indent</c>/<c>Dedent</c>/<c>Newline</c>/<c>Eof</c>).</summary>
        public bool HasCode(int k)
        {
            var (start, end) = Lines[k];
            return _tokens.Any(t => t.Position >= start && t.Position < end && IsCode(t));
        }

        /// <summary>The first code-token start on line <paramref name="k"/> that lies outside every literal span (a span's own start counts as outside), or null.</summary>
        public int? FirstCodeTokenStart(int k)
        {
            var (start, end) = Lines[k];
            foreach (var token in _tokens)
            {
                if (token.Position < start || !IsCode(token))
                    continue;
                if (token.Position >= end)
                    break;
                var at = token.Position;
                if (!Spans.Any(s => s.Start < at && at < s.End))
                    return at;
            }
            return null;
        }

        /// <summary>Line <paramref name="k"/>'s first code token is the first token of its logical line (no code token of the same logical line precedes it), and the line does not start inside a literal.</summary>
        public bool StartsLogicalLine(int k)
        {
            if (StartsInsideLiteral(k))
                return false;
            var (start, end) = Lines[k];
            for (var i = 0; i < _tokens.Length; i++)
            {
                if (_tokens[i].Position < start || !IsCode(_tokens[i]))
                    continue;
                if (_tokens[i].Position >= end)
                    return false;
                for (var j = i - 1; j >= 0; j--)
                {
                    if (_tokens[j].Type is TokenType.Indent or TokenType.Dedent)
                        continue;
                    return _tokens[j].Type == TokenType.Newline;
                }
                return true;
            }
            return false;
        }

        private void RequireDomain(int k, string fact)
        {
            if (EndInsideLiteral(k))
                throw new InvalidOperationException($"{fact}(L{k + 1}) is not a clean fact: a literal span crosses the line's end (domain restriction)");
            if (!HasCode(k))
                throw new InvalidOperationException($"{fact}(L{k + 1}) asked of a line with no code token");
        }

        private int DepthAtEnd(int k) => _lastBeforeEnd[k] < 0 ? 0 : _depthAfter[_lastBeforeEnd[k]];

        /// <summary>
        /// Line <paramref name="k"/> continues on the next: a real bracket is open at its end, or it ends in a backslash
        /// continuation. Throws outside the domain (<see cref="EndInsideLiteral"/>), and when the token reading (depth 0,
        /// no <c>Newline</c> at the end, not the last line) is not confirmed by a backslash in the text.
        /// </summary>
        public bool Continued(int k)
        {
            RequireDomain(k, nameof(Continued));
            if (DepthAtEnd(k) > 0)
                return true;
            var end = Lines[k].End;
            if (end >= Source.Length || _newlinePositions.Contains(end))
                return false;
            var last = end - 1;
            while (last >= Lines[k].Start && Source[last] is ' ' or '\t')
                last--;
            if (last < Lines[k].Start || Source[last] != '\\')
                throw new InvalidOperationException($"oracle inconsistent at L{k + 1}: depth 0, no Newline at the line's end, yet no trailing backslash :: {Source[Lines[k].Start..end]}");
            return true;
        }

        /// <summary>The 1-based line of the outermost real bracket open at line <paramref name="k"/>'s end, or null. Throws outside the domain.</summary>
        public int? BracketOpenerLine(int k)
        {
            RequireDomain(k, nameof(BracketOpenerLine));
            var last = _lastBeforeEnd[k];
            return last < 0 || _outermostAfter[last] == 0 ? null : _outermostAfter[last];
        }

        /// <summary>The logical line of line <paramref name="k"/>'s first code token.</summary>
        private int LogicalOf(int k)
        {
            var (start, end) = Lines[k];
            for (var i = 0; i < _tokens.Length; i++)
            {
                if (_tokens[i].Position >= start && _tokens[i].Position < end && IsCode(_tokens[i]))
                    return _logical[i];
            }
            throw new InvalidOperationException($"L{k + 1} has no code token");
        }

        /// <summary>A <c>Colon</c> token at real bracket depth 0, outside every f-string, on the logical line containing line <paramref name="k"/>, up to its end.</summary>
        public bool ColonOnLogicalLine(int k)
        {
            var logical = LogicalOf(k);
            var end = Lines[k].End;
            for (var i = 0; i < _tokens.Length && _tokens[i].Position < end; i++)
            {
                if (_logical[i] == logical && _tokens[i].Type == TokenType.Colon && !_inFString[i] && _depthBefore[i] == 0)
                    return true;
            }
            return false;
        }

        /// <summary>A code token of the logical line containing line <paramref name="k"/> starts before <paramref name="abortAt"/> (the lexer read it before aborting there).</summary>
        public bool TokenBefore(int k, int abortAt)
        {
            var logical = LogicalOf(k);
            for (var i = 0; i < _tokens.Length && _tokens[i].Position < abortAt; i++)
            {
                if (_logical[i] == logical && IsCode(_tokens[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// What <c>RecoveryResumesAfterAPossibleHeader</c> means for an abort at <paramref name="abortAt"/> on line
        /// <paramref name="k"/> whose given-up text is read cleanly (no unterminated literal: the end is readable): the
        /// line does not continue, a token of its logical line was read before the abort (lead ruling L8: a line dropped
        /// before any token is read is never one), and the logical line holds a depth-0 colon. Throws outside the domain.
        /// </summary>
        public bool PossibleHeader(int k, int abortAt)
            => !Continued(k) && TokenBefore(k, abortAt) && ColonOnLogicalLine(k);
    }

    /// <summary>
    /// The oracle's own hand rows (it is an instrument; verification-contract §2 applies to it): one clean document per
    /// fact arm, each with the expected value of every fact on its 1-based line. <c>null</c> in a domain-restricted
    /// column means the line's end is inside a literal and the fact must THROW.
    /// </summary>
    public static TheoryData<string, string, int, bool, bool, bool?, int?, bool> CleanFactRows => new()
    {
        // name, source, line, endInsideLiteral, closeLine, continued, bracketOpenerLine, colonOnLogicalLine
        { "a statement", "x = 1\n", 1, false, false, false, null, false },
        { "a bracket open at the end", "x = foo(1,\n    2)\n", 1, false, false, true, 1, false },
        { "the bracket closed on the next line", "x = foo(1,\n    2)\n", 2, false, false, false, null, false },
        { "a backslash continuation", "y = 1 + \\\n    2\n", 1, false, false, true, null, false },
        { "a backslash continuation, CRLF", "y = 1 + \\\r\n    2\r\n", 1, false, false, true, null, false },
        { "the line after a backslash", "y = 1 + \\\n    2\n", 2, false, false, false, null, false },
        { "the last line with no break", "x = (1)", 1, false, false, false, null, false },
        { "the last line with no break, a bracket spanning lines", "x = (1,\n     2)", 1, false, false, true, 1, false },
        { "nested: the outermost opener", "a = [(1,\n  2), (3,\n 4)]\n", 2, false, false, true, 1, false },
        { "a header colon", "if x:\n    y = 1\n", 1, false, false, false, null, true },
        { "a colon on the logical line BEFORE", "if x:\n    y = 1\n", 2, false, false, false, null, false },
        { "a header colon after a bracket spanning lines", "def f(a,\n      b):\n    pass\n", 2, false, false, false, null, true },
        { "no colon yet on the header's first line", "def f(a,\n      b):\n    pass\n", 1, false, false, true, 1, false },
        { "a colon in a bracket", "d = {a: 1}\n", 1, false, false, false, null, false },
        { "a walrus is no colon", "y := 1\n", 1, false, false, false, null, false },
        { "a colon in a format spec", "s = f\"{x:>4}\"\n", 1, false, false, false, null, false },
        { "a lambda colon in a field", "s = f\"{(lambda: 1)()}\" + 'a'\n", 1, false, false, false, null, false },
        { "a field's bracket around a real one", "print(f\"{a[0]}\",\n    1)\n", 1, false, false, true, 1, false },
        { "a comment holding a bracket", "x = 1  # (\n", 1, false, false, false, null, false },
        { "a string holding a bracket and a colon", "x = \"(:\"\n", 1, false, false, false, null, false },
        { "a triple opener: the end is inside a literal", "s = \"\"\"\nabc\n\"\"\"\n", 1, true, false, null, null, false },
        { "the triple's close line", "s = \"\"\"\nabc\n\"\"\" + x\n", 3, false, true, false, null, false },
        { "a field spanning lines: the end is inside a literal", "x = f\"{foo(1,\n2)}\"\n", 1, true, false, null, null, false },
        { "a lone-CR triple opener", "s = \"\"\"\rabc\r\"\"\"\r", 1, true, false, null, null, false },
    };

    [Theory]
    [MemberData(nameof(CleanFactRows))]
    public void CleanFacts_HandRows(string name, string source, int line, bool endInsideLiteral, bool closeLine, bool? continued,
        int? bracketOpenerLine, bool colonOnLogicalLine)
    {
        var lexed = CleanLineFacts.Of(source);
        lexed.Should().NotBeNull("{0} must lex clean to be inside the oracle", name);
        var facts = lexed!;
        var k = line - 1;

        facts.EndInsideLiteral(k).Should().Be(endInsideLiteral, name);
        facts.IsCloseLine(k).Should().Be(closeLine, name);
        facts.ColonOnLogicalLine(k).Should().Be(colonOnLogicalLine, name);
        if (continued is { } expected)
        {
            facts.Continued(k).Should().Be(expected, name);
            facts.BracketOpenerLine(k).Should().Be(bracketOpenerLine, name);
        }
        else
        {
            FluentActions.Invoking(() => facts.Continued(k)).Should().Throw<InvalidOperationException>("{0}: outside the domain", name);
            FluentActions.Invoking(() => facts.BracketOpenerLine(k)).Should().Throw<InvalidOperationException>("{0}: outside the domain", name);
            facts.OpenerCrossing(k, facts.Lines[k].Start).Should().BeTrue("{0}: the literal crossing the end starts on the line", name);
        }
    }

    /// <summary>
    /// <see cref="CleanLineFacts.OpenerCrossing"/>, <see cref="CleanLineFacts.TokenBefore"/>,
    /// <see cref="CleanLineFacts.FirstCodeTokenStart"/> and <see cref="CleanLineFacts.StartsLogicalLine"/> by offset:
    /// an opener after the offset crosses, one before it does not; a token read before the abort is one of the logical
    /// line's; a continuation line does not start its logical line.
    /// </summary>
    [Fact]
    public void CleanFacts_OffsetFacts()
    {
        const string source = "x = 'a' + \"\"\"\nk\n\"\"\"\ny = foo(1,\n    2)\n";
        var facts = CleanLineFacts.Of(source)!;
        var triple = source.IndexOf("\"\"\"", StringComparison.Ordinal);

        facts.OpenerCrossing(0, 0).Should().BeTrue();
        facts.OpenerCrossing(0, triple).Should().BeTrue("the span's own start is inside [at, lineEnd)");
        facts.OpenerCrossing(0, triple + 1).Should().BeFalse("the opener starts before the offset");
        facts.TokenBefore(0, 0).Should().BeFalse();
        facts.TokenBefore(0, source.IndexOf('\'')).Should().BeTrue("x and = were read");
        facts.FirstCodeTokenStart(0).Should().Be(0);
        facts.FirstCodeTokenStart(2).Should().BeNull("the close line's only code is the literal that started above");
        facts.FirstCodeTokenStart(3).Should().Be(source.IndexOf('y'));
        facts.StartsLogicalLine(3).Should().BeTrue();
        facts.StartsLogicalLine(4).Should().BeFalse("the line continues the bracket opened above");
        facts.TokenBefore(4, facts.Lines[4].Start).Should().BeTrue("the logical line's tokens on the line above were read");
    }
}
