using System.Globalization;
using Sharpy.Compiler.Lexer;

namespace Sharpy.TestInfrastructure.Lexing;

/// <summary>
/// The token half of the position instrument (#2300 row C2, P68): every token's (line, column, position, length)
/// must map back to the source slice it was read from. Built here, in TestInfrastructure, so P67 (#2295 —
/// node end-position authority) extends it with NODE ends rather than writing a second model.
/// <para>
/// <see cref="Violations(string, IReadOnlyList{Token}, IReadOnlyList{int}?, bool)"/> yields one line per broken
/// rule; an empty result is the claim. The rules, by token kind:
/// </para>
/// <list type="bullet">
/// <item><b>R1</b> — <c>LineStarts[Line-1] + Column - 1 == Position</c> for every token, the position lying on that
/// line (before the next line's start — a column run past the line's end sums to the right offset on the wrong
/// line, the shape of a line break consumed without <c>_line++</c>). A token with no position
/// (<c>Position &lt; 0</c>) or a line outside the source is itself a violation. <c>Indent</c>/<c>Dedent</c> are
/// written at column 1 with the position AFTER the indentation (<c>Lexer.Indentation.cs</c>), so for them R1 also
/// accepts column 1 with only spaces between the line start and the position. R1 is vacuous for a token at column 1
/// (a dropped <c>_column++</c> inside a construct is seen by the FOLLOWING token on the same line) — callers that
/// want a construct's column advance observed must put a token after it on its line.</item>
/// <item><b>R2</b> — <c>Position + span ≤ source.Length</c> (span per R7).</item>
/// <item><b>R3</b> — verbatim kinds (keywords, non-backtick identifiers, operators, delimiters, f-string
/// start/end/brace tokens): the slice equals <c>Value</c>. <c>Integer</c>/<c>Float</c> carry a NORMALIZED value
/// and no <c>SourceLength</c>: the source run from <c>Position</c> with underscores removed, the base prefix's case
/// folded (<c>0X</c>→<c>0x</c>, <c>0B</c>→<c>0b</c>, <c>0O</c>→<c>0o</c>) and a leading <c>.</c> read as <c>0.</c>
/// must spell <c>Value</c>; that run is the number's span.</item>
/// <item><b>R4</b> — <c>String</c>/<c>RawString</c>/<c>ByteString</c>: the slice (<c>SourceLength</c>) starts with
/// a prefix the type admits plus a quote run (one or three) and ends with the same quote run; a backtick
/// <c>Identifier</c>'s slice is <c>`</c> + <c>Value</c> + <c>`</c>. <c>FStringConversion</c> (no
/// <c>SourceLength</c>; Value is the flag letter) spans <c>!</c> + <c>Value</c>; <c>FStringSelfDoc</c>'s slice is
/// <c>=</c> followed by hole trivia only.</item>
/// <item><b>R5</b> — <c>Newline</c>: <c>Value</c> is <c>"\n"</c>, the slice at <c>Position</c> is a line break
/// (<c>\r\n</c>, <c>\n</c> or a lone <c>\r</c>) and the next line starts right after it.</item>
/// <item><b>R6</b> — the last token is <c>Eof</c> and <c>Eof.Position == source.Length</c> (≤ when the end was
/// given up — a budget stop reads no further).</item>
/// <item><b>R7</b> — tiling: consecutive non-synthetic tokens (all but <c>Indent</c>/<c>Dedent</c>/<c>Eof</c>) do
/// not overlap, and the gap text between them — and before the first and after the last — is whitespace,
/// comments, line breaks and backslash continuations only; the gap before the first <c>FStringFormatSpec</c> of a
/// field may end in the spec's <c>:</c> (no token carries it). A synthetic token lies between its neighbours.
/// <b>Recorded limit:</b> <c>FStringText</c>/<c>FStringFormatSpec</c> carry no <c>SourceLength</c> (their Value is
/// decoded text), so their span is read as <c>next.Position - Position</c> — only the (line, column) ↔ position
/// identity and the tiling order are asserted for them, never slice == Value (a cell for #2300's C2 row).</item>
/// </list>
/// A gap holding a recovery resume (<see cref="Lexer.RecoveryResumes"/>) is text error recovery dropped, and the
/// last gap is the text an abort at the end gave up; <paramref name="recoveryResumes"/>/<c>endGivenUp</c> exempt
/// exactly those gaps' CONTENT from R7 (never their order). <see cref="Violations(string, Lexer, IReadOnlyList{Token})"/>
/// reads both from the lexer.
/// </summary>
public static class TokenPositionReconstruction
{
    /// <summary>
    /// The 0-based offset where each 1-based line starts (index <c>k</c> is line <c>k + 1</c>). <c>\r\n</c>,
    /// <c>\n</c> and a lone <c>\r</c> each end a line, as in the lexer (<c>LiteralSpans.LinesStartingInside</c>);
    /// a source ending in a line break has a last, empty line starting at <c>source.Length</c>.
    /// </summary>
    public static IReadOnlyList<int> LineStarts(string source)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (c != '\n' && c != '\r')
                continue;
            if (c == '\r' && i + 1 < source.Length && source[i + 1] == '\n')
                i++;
            starts.Add(i + 1);
        }
        return starts;
    }

    /// <summary>
    /// <see cref="Violations(string, IReadOnlyList{Token}, IReadOnlyList{int}?, bool)"/> with the dropped text read
    /// from <paramref name="lexer"/>: its <see cref="Lexer.RecoveryResumes"/>, and the end given up when it reported
    /// any error (an abort on the last line or a budget stop runs no recovery, so no resume marks that text).
    /// </summary>
    public static IEnumerable<string> Violations(string source, Lexer lexer, IReadOnlyList<Token> tokens) =>
        Violations(source, tokens, lexer.RecoveryResumes, endGivenUp: lexer.Diagnostics.HasErrors);

    /// <summary>One line per violated rule (R1–R7, see the type's summary); empty when every token reconstructs.</summary>
    public static IEnumerable<string> Violations(string source, IReadOnlyList<Token> tokens,
        IReadOnlyList<int>? recoveryResumes = null, bool endGivenUp = false)
    {
        var lineStarts = LineStarts(source);
        var resumes = recoveryResumes ?? Array.Empty<int>();

        // R1 / R4 / R5 / R3 per token; spans for R2 / R7.
        var spans = new int[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            foreach (var violation in PositionRule(source, lineStarts, token))
                yield return violation;
            if (token.Position < 0 || token.Position > source.Length)
            {
                spans[i] = 0;
                continue;
            }

            var (span, kindViolation) = SpanOf(source, lineStarts, tokens, i);
            spans[i] = span;
            if (kindViolation != null)
                yield return kindViolation;
            if (token.Position + span > source.Length)
                yield return $"R2 {Describe(token)}: span {span} runs past the source end {source.Length}";
        }

        // R6
        if (tokens.Count == 0 || tokens[^1].Type != TokenType.Eof)
            yield return "R6 the token list does not end with Eof";
        else if (endGivenUp ? tokens[^1].Position > source.Length : tokens[^1].Position != source.Length)
            yield return $"R6 {Describe(tokens[^1])}: Eof is not at the source end {source.Length}";

        // R7 tiling over the non-synthetic tokens; synthetic tokens lie between their neighbours.
        var end = 0;          // end of the previous non-synthetic token
        var previous = "start of source";
        var pendingSynthetic = new List<Token>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Position < 0 || token.Position > source.Length)
                continue;
            if (IsSynthetic(token.Type))
            {
                if (token.Position < end)
                    yield return $"R7 {Describe(token)}: synthetic token before the end {end} of {previous}";
                pendingSynthetic.Add(token);
                if (token.Type != TokenType.Eof)
                    continue;
                foreach (var violation in Gap(source, end, token.Position, previous, Describe(token), resumes, exempt: endGivenUp, specColon: false))
                    yield return violation;
                continue;
            }

            if (token.Position < end)
            {
                yield return $"R7 {Describe(token)}: overlaps {previous}, which ends at {end}";
            }
            else
            {
                foreach (var violation in Gap(source, end, token.Position, previous, Describe(token), resumes, exempt: false,
                             specColon: token.Type == TokenType.FStringFormatSpec))
                    yield return violation;
            }
            foreach (var synthetic in pendingSynthetic)
            {
                if (synthetic.Position > token.Position)
                    yield return $"R7 {Describe(synthetic)}: synthetic token after the start of {Describe(token)}";
            }
            pendingSynthetic.Clear();
            end = System.Math.Max(end, token.Position + spans[i]);
            previous = Describe(token);
        }
    }

    private static bool IsSynthetic(TokenType type) => type is TokenType.Indent or TokenType.Dedent or TokenType.Eof;

    private static string Describe(Token token) =>
        $"{token.Type} '{Escape(token.Value)}' L{token.Line}:C{token.Column}@{token.Position}";

    private static string Escape(string text)
    {
        var shown = text.Length > 24 ? text[..24] + "…" : text;
        return shown.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
    }

    /// <summary>R1: the token's (line, column) is its position read through the line-start table.</summary>
    private static IEnumerable<string> PositionRule(string source, IReadOnlyList<int> lineStarts, Token token)
    {
        if (token.Position < 0)
        {
            yield return $"R1 {Describe(token)}: no position";
            yield break;
        }
        if (token.Position > source.Length)
        {
            yield return $"R1 {Describe(token)}: position past the source end {source.Length}";
            yield break;
        }
        if (token.Line < 1 || token.Line > lineStarts.Count)
        {
            yield return $"R1 {Describe(token)}: line outside 1..{lineStarts.Count}";
            yield break;
        }

        var lineStart = lineStarts[token.Line - 1];
        var expected = lineStart + token.Column - 1;
        if (token.Line < lineStarts.Count && token.Position >= lineStarts[token.Line])
        {
            // A column past the line's end still sums to the position; the position must lie on the token's line.
            yield return $"R1 {Describe(token)}: position {token.Position} is not on line {token.Line} (line {token.Line + 1} starts at {lineStarts[token.Line]})";
            yield break;
        }
        if (expected == token.Position)
            yield break;

        // Indent/Dedent: column 1, position after the line's indentation.
        if (token.Type is TokenType.Indent or TokenType.Dedent && token.Column == 1 && token.Position >= lineStart
            && source.AsSpan(lineStart, token.Position - lineStart).TrimStart(' ').IsEmpty)
            yield break;

        yield return $"R1 {Describe(token)}: line start {lineStart} + column {token.Column} - 1 = {expected} ≠ position {token.Position}";
    }

    /// <summary>The token's source span (for R2/R7) and the kind rule it breaks (R3/R4/R5), if any.</summary>
    private static (int Span, string? Violation) SpanOf(string source, IReadOnlyList<int> lineStarts, IReadOnlyList<Token> tokens, int index)
    {
        var token = tokens[index];
        var at = token.Position;
        var rest = source.Length - at;
        switch (token.Type)
        {
            case TokenType.Indent or TokenType.Dedent or TokenType.Eof:
                return (0, token.Value.Length == 0 ? null : $"R3 {Describe(token)}: a synthetic token carries text");

            case TokenType.Newline:
                {
                    if (rest < 1 || source[at] is not ('\n' or '\r'))
                        return (token.Length, $"R5 {Describe(token)}: no line break at the position");
                    var length = source[at] == '\r' && rest > 1 && source[at + 1] == '\n' ? 2 : 1;
                    if (token.Value != "\n")
                        return (length, $"R5 {Describe(token)}: Newline value is not \"\\n\"");
                    if (token.Line >= lineStarts.Count || lineStarts[token.Line] != at + length)
                        return (length, $"R5 {Describe(token)}: the line after it does not start at {at + length}");
                    return (length, null);
                }

            case TokenType.Integer or TokenType.Float:
                return NumberSpan(source, token);

            case TokenType.String or TokenType.RawString or TokenType.ByteString:
                return (token.Length, QuotedRule(source, token));

            case TokenType.Identifier when token.IsBacktickEscaped:
                {
                    var length = token.Length;
                    var expected = "`" + token.Value + "`";
                    return (length, length <= rest && length == expected.Length && string.CompareOrdinal(source, at, expected, 0, length) == 0
                        ? null
                        : $"R4 {Describe(token)}: slice '{Escape(Slice(source, at, length))}' is not '{Escape(expected)}'");
                }

            case TokenType.FStringConversion:
                {
                    var expected = "!" + token.Value;
                    return (expected.Length, expected.Length <= rest && string.CompareOrdinal(source, at, expected, 0, expected.Length) == 0
                        ? null
                        : $"R4 {Describe(token)}: slice '{Escape(Slice(source, at, expected.Length))}' is not '{Escape(expected)}'");
                }

            case TokenType.FStringSelfDoc:
                {
                    var length = token.Length;
                    if (length < 1 || length > rest || source[at] != '=')
                        return (length, $"R4 {Describe(token)}: slice '{Escape(Slice(source, at, length))}' does not start with '='");
                    return (length, TriviaEnd(source, at + 1, at + length) == at + length
                        ? null
                        : $"R4 {Describe(token)}: slice '{Escape(Slice(source, at, length))}' holds more than '=' and trivia");
                }

            case TokenType.FStringText or TokenType.FStringFormatSpec:
                {
                    // Recorded limit: decoded Value, no SourceLength — the span is up to the next token.
                    var next = index + 1 < tokens.Count ? tokens[index + 1].Position : source.Length;
                    return (System.Math.Max(0, next - at), null);
                }

            default:
                {
                    var length = token.Value.Length;
                    if (token.SourceLength is { } sourceLength && sourceLength != length)
                        return (sourceLength, $"R3 {Describe(token)}: verbatim kind with SourceLength {sourceLength} ≠ Value length {length}");
                    return (length, length <= rest && string.CompareOrdinal(source, at, token.Value, 0, length) == 0
                        ? null
                        : $"R3 {Describe(token)}: slice '{Escape(Slice(source, at, length))}' ≠ Value");
                }
        }
    }

    private static string Slice(string source, int at, int length) =>
        at < 0 || at > source.Length ? "" : source.Substring(at, System.Math.Max(0, System.Math.Min(length, source.Length - at)));

    /// <summary>R3 for numbers: the normalized source run from the position spells Value; that run is the span.</summary>
    private static (int Span, string? Violation) NumberSpan(string source, Token token)
    {
        var at = token.Position;
        var i = at;
        var spelled = new System.Text.StringBuilder();
        if (i < source.Length && source[i] == '.')
        {
            spelled.Append("0.");
            i++;
        }
        else if (i + 1 < source.Length && source[i] == '0' && source[i + 1] is 'x' or 'X' or 'b' or 'B' or 'o' or 'O')
        {
            spelled.Append('0').Append(char.ToLower(source[i + 1], CultureInfo.InvariantCulture));
            i += 2;
        }
        while (spelled.Length < token.Value.Length && i < source.Length)
        {
            if (source[i] != '_')
                spelled.Append(source[i]);
            i++;
        }

        var span = i - at;
        return (span, spelled.ToString() == token.Value
            ? null
            : $"R3 {Describe(token)}: normalized slice '{Escape(spelled.ToString())}' ≠ Value");
    }

    private static readonly IReadOnlyDictionary<TokenType, string[]> QuotedPrefixes = new Dictionary<TokenType, string[]>
    {
        [TokenType.String] = new[] { "", "d" },
        [TokenType.RawString] = new[] { "r", "dr" },
        [TokenType.ByteString] = new[] { "b" },
    };

    /// <summary>R4 for string literals: prefix + quote run … same quote run.</summary>
    private static string? QuotedRule(string source, Token token)
    {
        var at = token.Position;
        var length = token.Length;
        if (length > source.Length - at)
            return $"R4 {Describe(token)}: span {length} runs past the source end";
        var slice = source.Substring(at, length);
        foreach (var prefix in QuotedPrefixes[token.Type].OrderByDescending(p => p.Length))
        {
            if (!slice.StartsWith(prefix, StringComparison.Ordinal) || slice.Length <= prefix.Length)
                continue;
            var quote = slice[prefix.Length];
            if (quote is not ('"' or '\''))
                continue;
            var run = new string(quote, 3);
            var triple = slice.Length >= prefix.Length + 6 && string.CompareOrdinal(slice, prefix.Length, run, 0, 3) == 0;
            var delimiter = triple ? run : quote.ToString();
            if (slice.Length >= prefix.Length + (2 * delimiter.Length) && slice.EndsWith(delimiter, StringComparison.Ordinal))
                return null;
        }
        return $"R4 {Describe(token)}: slice '{Escape(slice)}' is not a {token.Type} prefix + quote … quote";
    }

    /// <summary>R7: the text between two tokens is trivia (or exempt as dropped text).</summary>
    private static IEnumerable<string> Gap(string source, int from, int to, string previous, string next,
        IReadOnlyList<int> resumes, bool exempt, bool specColon)
    {
        if (from >= to)
            yield break;
        if (exempt || resumes.Any(r => r > from && r <= to))
            yield break;

        var triviaEnd = TriviaEnd(source, from, to);
        if (triviaEnd == to)
            yield break;
        if (specColon && triviaEnd == to - 1 && source[triviaEnd] == ':')
            yield break;
        yield return $"R7 gap [{from},{to}) between {previous} and {next} is not trivia: '{Escape(source.Substring(from, to - from))}'";
    }

    /// <summary>
    /// The first offset in <c>[from, to)</c> that is not trivia — spaces, tabs, form feeds, line breaks, a comment
    /// running to its line's end, a backslash immediately followed by a line break — or <paramref name="to"/>.
    /// </summary>
    private static int TriviaEnd(string source, int from, int to)
    {
        var i = from;
        while (i < to)
        {
            var c = source[i];
            if (c is ' ' or '\t' or '\f' or '\n' or '\r')
            {
                i++;
            }
            else if (c == '#')
            {
                while (i < to && source[i] is not ('\n' or '\r'))
                    i++;
            }
            else if (c == '\\' && i + 1 < to && source[i + 1] is '\n' or '\r')
            {
                i += 2;
            }
            else
            {
                break;
            }
        }
        return i;
    }
}
