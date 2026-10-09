namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
    private int _lastMultiLineCloseLine = -1;   // see NoteMultiLineLiteralClosed

    /// <summary>
    /// Error recovery is about to skip the rest of the current line (<see cref="RecoverFromError"/>):
    /// <see cref="LiteralLoss.DroppedOpener"/> when that text holds a literal that can span lines. At an
    /// indentation error (SPY0011–SPY0014) <see cref="_position"/> is the line start
    /// (<see cref="HandleLineStartIndentation"/> restores it before measuring), so the span is the whole
    /// line; after a mid-line abort it is the rest of the line (<c>x = $"""</c>). A mid-line abort INSIDE a
    /// string literal (an invalid escape, non-ASCII in a <c>b</c> string, a lone <c>}</c> in an f-string)
    /// leaves <see cref="_position"/> inside it, where the scanner would pair the quotes the wrong way
    /// (<c>"\q" + """</c> read from the <c>q</c> is <c>" + "</c> then <c>""</c>): when <paramref name="fromTheAbortedLiteral"/>
    /// and the token being read (<see cref="_tokenStart"/>) is a string literal that began on this line, the
    /// span starts at its prefix instead. An unterminated short string is then the whole span up to its line
    /// break, which the scanner skips (a short string ends at its line). After an unclosed replacement field
    /// (<see cref="ReportUnclosedField"/>, single-quoted strings only) the lexer itself has ruled that the field
    /// was being typed and the line after its bracket is code: the span stays at the bracket.
    /// </summary>
    private void NoteDroppedSpan(bool fromTheAbortedLiteral)
    {
        var start = UnreadTextStart(fromTheAbortedLiteral);
        var end = NoteUnreadLine(start);
        if (HoldsALiteralSpanningLines(_source.AsSpan(start, end - start)))
            LiteralLoss |= LiteralLoss.DroppedOpener;
    }

    /// <summary>
    /// The lexer gives up the text from <paramref name="start"/> (<see cref="UnreadTextStart"/>) to the end of the
    /// current line: error recovery skips it (<see cref="NoteDroppedSpan"/>), or a stop in <see cref="TokenizeAll"/>
    /// (the error budget, the end of the source) reads no further. The ONE close-line rule every such hook calls —
    /// arm (b) of <see cref="LiteralLoss.RePairedCloser"/> (owner ruling R-FR, #2275; narrowed by R-FV, #2280): on the
    /// line where a literal that spanned lines closed (<see cref="NoteMultiLineLiteralClosed"/>), text the lexer never
    /// reads that leaves a quote UNPAIRED at the line's end (<see cref="LeavesAQuoteUnpairedAtLineEnd"/>) holds an
    /// orphan quote the closer may have re-paired with (<c>t = '"""$'</c>, <c>t = '"""`'</c>, <c>t = '""" '</c> below a
    /// stray <c>"""</c>) — R-FK's rule for dropped lines, applied at the close line, whatever the abort's code. The
    /// aborted literal's OWN delimiters pair (<c>""" + "\q"</c>, <c>""" + f"{x!q}"</c>: the text starts at its prefix,
    /// and its closing quote closes it), so a closer line ending in a literal that aborts inside itself lost nothing
    /// and records nothing (#2280: before R-FV any quote in the text counted, and every route lost the repair). A
    /// close line whose given-up text holds no quote (<c>""" + 1__2</c>) records nothing either; a report that drops
    /// no text (the dedented-string indentation errors) never
    /// reaches here. <see cref="_line"/> is the abort's line: <see cref="ReportUnclosedField"/> moves it back to
    /// the field's bracket for a single-quoted literal, and with a bracket open inside a triple-quoted one the abort
    /// is inside a literal anyway (<see cref="LiteralLoss.AbortInsideLiteral"/>). Returns the end of the line.
    /// </summary>
    private int NoteUnreadLine(int start)
    {
        var end = _position;
        while (end < _source.Length && !IsLineBreak(_source[end]))
            end++;
        if (_line == _lastMultiLineCloseLine && LeavesAQuoteUnpairedAtLineEnd(_source.AsSpan(start, end - start)))
            LiteralLoss |= LiteralLoss.RePairedCloser;
        return end;
    }

    /// <summary>
    /// Where the text the lexer will not read starts, for <see cref="NoteDroppedSpan"/> (the rest of the
    /// line error recovery skips) and for the error-budget stop in <see cref="TokenizeAll"/> (the rest of
    /// the source): <see cref="_position"/>, or — when <paramref name="fromTheAbortedLiteral"/> and the token
    /// being read is a string literal that began on this line — that literal's prefix, so that the scanner
    /// pairs its quotes the way the lexer did (<c>"\q" + """abc"""</c> read from the <c>q</c> is
    /// <c>" + "</c> then an opener), and so that the close-line walk reads the literal's own delimiters as the pair they
    /// are (<c>"\q"</c> is a closed literal, not two orphan quotes; R-FV, #2280). The ONE start both hooks read: the
    /// budget stop landing inside an
    /// aborted short string (B1, BS-esc) is the same shape as the dropped line. A backtick-delimited name that
    /// began on this line starts the text the same way (P22g, R-FR): the lexer reads it to the line end before it
    /// reports SPY0018, so from <see cref="_position"/> the text would be EMPTY and an orphan quote inside the name
    /// (<c>t = '"""`'</c>) would never be seen. The scanner reads the name as opaque; the close-line walk reads an
    /// UNTERMINATED name's text as text it cannot pair, so a quote in it still counts (R-FV).
    /// </summary>
    private int UnreadTextStart(bool fromTheAbortedLiteral)
    {
        if (fromTheAbortedLiteral && _tokenStart < _position
            && _source.AsSpan(_tokenStart, _position - _tokenStart).IndexOfAny('\n', '\r') < 0
            && (_source[_tokenStart] == '`' || StringLiteralStarts.Any(s => IsStringLiteralStartAt(s.Prefix, _tokenStart))))
        {
            return _tokenStart;
        }
        return _position;
    }

    /// <summary>
    /// Called by the five arms that close a triple-quoted literal (<see cref="ReadTripleQuotedString"/>
    /// for plain and <c>d</c>, <see cref="ReadTripleQuotedByteString"/>, the triple arms of
    /// <see cref="ReadRawString"/> and <see cref="ReadDedentedRawString"/>, and the f-/t-/df-string
    /// <c>FStringEnd</c> triple arm) with <see cref="_position"/> just past the closer and the line the
    /// literal started on. When the literal spanned lines, records the closer's context for
    /// <see cref="LiteralLoss.RePairedCloser"/>: (a) the closer is immediately followed by a quote character —
    /// implicit concatenation is not Sharpy syntax, so this never occurs in a program that parses: the closer
    /// re-paired with a quote inside a short string (<c>t = '"""'</c> below a stray <c>"""</c>); (b) the close
    /// line, so that <see cref="NoteUnreadLine"/> can tell text the lexer gives up on it that holds a quote
    /// character (<c>t = '""" '</c>, <c>t = '"""$'</c>; R-FR). A literal closed on the line it opened on cannot have lost multi-line content (the
    /// stray and the opener would share a line), so neither arm records it: <c>x = """a""" + 'b</c> (owner
    /// ruling 2026-10-07, arm b) and <c>z = """a""""</c> (arm a, by the same rationale) being typed record nothing.
    /// </summary>
    private void NoteMultiLineLiteralClosed(int startLine)
    {
        if (startLine >= _line)
            return;
        _lastMultiLineCloseLine = _line;
        if (_position < _source.Length && _source[_position] is '"' or '\'')
            LiteralLoss |= LiteralLoss.RePairedCloser;
    }

    /// <summary>
    /// Whether a dropped line continues on the next (<see cref="RecoveryResumesAfterAContinuedLine"/>): a bracket is
    /// still open at its END, or it ends in a backslash outside a comment. Judged from the line's end state — the
    /// lexer's bracket depth where it gave the line up (<paramref name="bracketDepth"/>) plus the brackets in the
    /// text it gave up (<paramref name="given"/>, read from the aborted literal's own start), with a short string,
    /// a backtick name and a comment read as the lexer reads them — never from the depth at the abort alone:
    /// <c>if foo($):</c> closes its bracket in the given-up text and does not continue (the next line is the body,
    /// whose block the indent-only check must judge); <c>x = $ foo(1,</c> opens one there and does (the next line
    /// is the user's continuation, which the fallback aligns). A literal that opens in the given-up text and does
    /// not close on the line is content to the line end, as the lexer would read it; a bracket in a string or a
    /// comment is text. (P22g verify round: the first spelling read <c>_bracketDepth > 0</c> at the abort and the
    /// last skipped character, so a closed bracket counted, an opened one did not, and <c># C:\</c> continued.)
    /// The two arms are told apart (<see cref="LineEnd"/>): only an open bracket is <see cref="BracketLeftOpenAtLine"/>.
    /// The same walk notes a <c>:</c> at depth 0 (not <c>:=</c>) for <see cref="RecoveryResumesAfterAPossibleHeader"/>.
    /// </summary>
    private static LineEnd DroppedLineContinues(ReadOnlySpan<char> given, int bracketDepth)
    {
        var depth = bracketDepth;
        var openedInGivenUpText = false;
        var colonAtDepthZero = false;
        var endUnreadable = false;
        var endsInBackslash = false;
        var i = 0;
        while (i < given.Length && !IsLineBreak(given[i]))
        {
            var c = given[i];
            if (c == '#')
                break;
            if (c is ' ' or '\t')
            {
                i++;
                continue;
            }

            endsInBackslash = c == '\\';
            if (c is '"' or '\'')
            {
                var literal = OpenLiteral.At(given, ref i);
                endUnreadable = true;   // until its closing quote is read
                while (i < given.Length && !IsLineBreak(given[i]))
                {
                    if (given[i] == '\\' && !literal.Raw && i + 1 < given.Length && !IsLineBreak(given[i + 1]))
                    {
                        i += 2;
                        continue;
                    }
                    if (given[i] == literal.Quote && (!literal.Triple || IsTripleAt(given, i)))
                    {
                        i += literal.Triple ? 3 : 1;
                        endUnreadable = false;
                        break;
                    }
                    i++;
                }
                continue;
            }
            if (c == '`')
            {
                var name = i;
                SkipBacktickName(given, ref i);
                endUnreadable = !(i - 1 > name && given[i - 1] == '`');
                continue;
            }

            if (c is '(' or '[' or '{')
            {
                if (depth++ == 0)
                    openedInGivenUpText = true;
            }
            else if (c is (')' or ']' or '}') && depth > 0)
                depth--;
            else if (c == ':' && depth == 0 && (i + 1 >= given.Length || given[i + 1] != '='))
                colonAtDepthZero = true;
            i++;
        }
        return new LineEnd(BracketOpen: depth > 0, Backslash: endsInBackslash, OpenedInGivenUpText: depth > 0 && openedInGivenUpText,
            ColonAtDepthZero: colonAtDepthZero, EndUnreadable: endUnreadable);
    }

    /// <summary>
    /// The END state of a line the lexer gives up (<see cref="DroppedLineContinues"/>): a bracket still open
    /// (<c>BracketOpen</c>; <c>OpenedInGivenUpText</c> when the outermost one left open was
    /// opened in the given-up text — <c>x = $ foo(1,</c>, or <c>$) + bar(2,</c> after the bracket open at the abort
    /// closed — rather than before the abort), and a trailing backslash outside a comment
    /// (<c>Backslash</c>). Either continues the line on the next; only the bracket is left open. <c>ColonAtDepthZero</c>:
    /// the given-up text holds a <c>:</c> outside every bracket, string, comment and backtick name (a possible block
    /// header's colon; <c>:=</c> is not one). <c>EndUnreadable</c>: the line ends inside an unterminated literal or
    /// backtick name (or an unclosed field), so a colon after the read text cannot be ruled out (lead ruling L11).
    /// </summary>
    private readonly record struct LineEnd(bool BracketOpen, bool Backslash, bool OpenedInGivenUpText, bool ColonAtDepthZero, bool EndUnreadable);

    /// <summary>A backtick-delimited name, read as the lexer reads one: it ends at its backtick or at the line break.</summary>
    private static void SkipBacktickName(ReadOnlySpan<char> text, ref int i)
    {
        i++;
        while (i < text.Length && text[i] != '`' && !IsLineBreak(text[i]))
            i++;
        if (i < text.Length && text[i] == '`')
            i++;
    }

    /// <summary>
    /// Whether <paramref name="text"/> holds a literal that can span lines (#2271, P22f decision 2): the
    /// ONE test behind <see cref="LiteralLoss.DroppedOpener"/> (the text error recovery skips) and
    /// <see cref="LiteralLoss.UnreadRemainder"/> (the source left when the error budget stops the lexer).
    /// Walks the text the way the lexer would read it from its start, reading on past an error as if it were
    /// mended where it stands (<c>"\q" + """</c> holds an opener): a <c>#</c> outside a literal skips to the
    /// end of its line; a backtick-delimited name is opaque (<c>`it's`</c> holds no quote); at a quote character, with the prefix letters immediately before it, a literal opens
    /// and is read to its closer. A line break (or the end of the text) inside a literal → true when that
    /// literal is triple-quoted or a replacement field is open in it or around it (a field may span lines);
    /// otherwise the literal ends at its line (a short string, an f-/t-string with no open field) and the walk
    /// continues after the line break. Inside an <c>f</c>/<c>t</c>/<c>df</c> literal, as
    /// <see cref="NextFStringToken"/> reads it:
    /// <list type="bullet">
    /// <item>in its text, <c>{{</c>/<c>}}</c> are escaped braces and <c>{</c> opens a replacement field;</item>
    /// <item>in a field's expression, braces and brackets nest, <c>}</c> at its top level closes it and <c>:</c>
    /// at its top level starts its format spec; a <c>#</c> is a comment to the end of the line (the field
    /// stays open past it); a backtick-delimited name is opaque; a quote opens a NESTED literal read by these
    /// same rules, so its quotes and braces are not the enclosing literal's (<c>f"{'"'}"</c>,
    /// <c>f"{'{'}"</c>, <c>f"{"""a"""}"</c>, <c>f"{f'{x}'}"</c>) — except the enclosing single-quoted
    /// literal's own quote when no literal opened there closes on its line: that quote is its closer
    /// (<c>f"{x" + 'z'</c>, the lexer's <c>expecting '}'</c>);</item>
    /// <item>in a format spec, <c>{{</c> is an escaped brace, <c>{</c> opens a nested field, <c>}</c> closes
    /// the field and any other quote is spec text; the literal's own quote (a triple at a triple, for a
    /// triple-quoted literal) is where the lexer aborts (SPY0022, <c>expecting '}'</c>) and reads nothing more on
    /// the line (#2276) — the quote's role is unknown, so every open literal is abandoned there and the rest of
    /// the text is walked afresh twice: with the quote opening a literal (<c>">}" + """</c>: a closed short string,
    /// then an opener) and with the quote closing the literal (<c>>}" + """</c>: <c>" + "</c>, then <c>""</c>) — true
    /// when EITHER walk holds a literal spanning lines (a missing fact is a silent wrong edit, a spurious one a
    /// lost repair). <c>x = f"{x:"</c> being typed holds none (both walks read a short string that ends at its line).
    /// The fresh walks are memoized per start, so a line with k such quotes costs at most two walks each.</item>
    /// </list>
    /// Backslash escapes are honoured except in <c>r</c>/<c>dr</c> literals, as the lexer reads them. A
    /// short string holding a triple (<c>'"""'</c>) or a closed triple on its line (<c>"""abc"""</c>)
    /// opens nothing that spans lines: a dropped line holding one keeps its repair. Known limit (#2274): an
    /// opener swallowed by an unterminated short string on its line (<c>x = 'abc """</c> → false).
    /// </summary>
    internal static bool HoldsALiteralSpanningLines(ReadOnlySpan<char> text) => HoldsALiteralSpanningLines(text, out _);

    /// <summary>
    /// <see cref="HoldsALiteralSpanningLines(ReadOnlySpan{char})"/>, reporting how many fresh walks the spec-quote
    /// aborts started (<paramref name="freshWalks"/>): at most two per abort position, memoized — the bound the
    /// complexity guard asserts, not a clock.
    /// </summary>
    internal static bool HoldsALiteralSpanningLines(ReadOnlySpan<char> text, out int freshWalks)
    {
        Dictionary<int, bool>? walks = null;
        var holds = WalkFrom(text, 0, WalkMode.SpansLines, ref walks);
        freshWalks = walks?.Count ?? 0;
        return holds;
    }

    /// <summary>
    /// Arm (b) of <see cref="LiteralLoss.RePairedCloser"/> (<see cref="NoteUnreadLine"/>; owner ruling R-FV, #2280):
    /// whether the text the lexer gives up on a close line leaves a quote UNPAIRED at the line's end — each quote's
    /// role read from a walk of the text (<see cref="WalkMode.CloseLine"/>), not its presence. True iff, at the line's
    /// end (or the text's), a literal is open — short or triple: the orphan's closing quote after a re-paired closer
    /// opens one (<c>$'</c>, <c>"\q"'</c>) — OR text the walk cannot pair holds a quote character:
    /// <list type="bullet">
    /// <item>the remainder after a <c>#</c> outside a literal — a comment to the lexer, but string text to a user whose
    /// orphan swallowed it: read as a comment the walk loses <c>$  # it's'</c> (the stray-closed twin), read as text
    /// it pairs that twin's two apostrophes and loses it again, so a quote anywhere in it counts;</item>
    /// <item>the text of an UNTERMINATED backtick name, given up with the line (<c>`'</c>, <c>`x'</c>,
    /// <c>`it's</c>).</item>
    /// </list>
    /// A TERMINATED backtick name is opaque (<c>`it's`</c>), as the lexer reads it. A closed literal contributes
    /// nothing: the aborted literal's own delimiters pair (<c>"\q"</c>, <c>f"{x!q}"</c>, <c>f"}"</c>). At the literal's
    /// own quote inside a format spec the fork keeps the scanner's "either walk" rule (#2276), so <c>f"{x:">}"</c> holds
    /// one (reading the quote as the closer leaves <c>"</c> open). Known limits (#2274, lead ruling L2): with no stray
    /// above, <c>`it's</c>, <c>f"{x:">}"</c> and <c>$  # it's</c> lost nothing yet hold a quote the walk cannot pair —
    /// their stray-closed twins (<c>`it's'</c>, <c>f"{x:">}"'</c>, <c>$  # it's'</c>) are why.
    /// </summary>
    internal static bool LeavesAQuoteUnpairedAtLineEnd(ReadOnlySpan<char> text)
    {
        Dictionary<int, bool>? walks = null;
        return WalkFrom(text, 0, WalkMode.CloseLine, ref walks);
    }

    /// <summary>What a walk of <see cref="WalkFrom"/> answers.</summary>
    private enum WalkMode
    {
        /// <summary><see cref="HoldsALiteralSpanningLines"/>: a literal open at a line break that continues past it.</summary>
        SpansLines,

        /// <summary><see cref="LeavesAQuoteUnpairedAtLineEnd"/>: a quote left unpaired at the end of the first line.</summary>
        CloseLine,
    }

    /// <summary>
    /// <see cref="WalkFrom"/> from <paramref name="start"/>, memoized in <paramref name="walks"/>: a fresh walk is a
    /// pure function of where it starts (no literal is open there) and of its <paramref name="mode"/> — each entry point
    /// starts its own memo, so one never mixes the modes.
    /// </summary>
    private static bool FreshWalk(ReadOnlySpan<char> text, int start, WalkMode mode, ref Dictionary<int, bool>? walks)
    {
        walks ??= new Dictionary<int, bool>();
        if (walks.TryGetValue(start, out var known))
            return known;
        var holds = WalkFrom(text, start, mode, ref walks);
        walks![start] = holds;  // the recursive walk never clears the memo it was handed
        return holds;
    }

    /// <summary>
    /// The walk of <see cref="HoldsALiteralSpanningLines"/> (<see cref="WalkMode.SpansLines"/>) or of
    /// <see cref="LeavesAQuoteUnpairedAtLineEnd"/> (<see cref="WalkMode.CloseLine"/>, which reads the first line only)
    /// from <paramref name="start"/>, with no literal open there. The literal reads are shared; the modes differ at a
    /// <c>#</c> and an unterminated backtick name outside a literal, and in what the line's end answers.
    /// </summary>
    private static bool WalkFrom(ReadOnlySpan<char> text, int start, WalkMode mode, ref Dictionary<int, bool>? walks)
    {
        var open = new List<OpenLiteral>();     // the literals open at i, outermost first
        var i = start;
        while (i < text.Length)
        {
            var c = text[i];
            if (mode == WalkMode.CloseLine && IsLineBreak(c))
                return open.Count > 0;  // the close line's end: a literal still open there leaves its quote unpaired
            if (open.Count == 0)
            {
                if (c == '#')
                {
                    var comment = i;
                    while (i < text.Length && !IsLineBreak(text[i]))
                        i++;
                    // on a close line the remainder is a comment to the lexer and string text to the user: unpairable
                    if (mode == WalkMode.CloseLine && text[comment..i].IndexOfAny('"', '\'') >= 0)
                        return true;
                }
                else if (c is '"' or '\'')
                    open.Add(OpenLiteral.At(text, ref i));
                else if (c == '`')
                {
                    var name = i;
                    SkipBacktickName(text, ref i);  // `it's` or `a#b` holds no quote and no comment
                    // an UNTERMINATED name's text is given up with the line: a quote in it is unpairable
                    if (mode == WalkMode.CloseLine && !(i - 1 > name && text[i - 1] == '`') && text[name..i].IndexOfAny('"', '\'') >= 0)
                        return true;
                }
                else
                    i++;
                continue;
            }

            var literal = open[^1];
            var field = literal.Fields is { Count: > 0 } fields ? fields[^1] : null;
            if (IsLineBreak(c))
            {
                if (SpansTheLineBreak(open))
                    return true;
                open.Clear();       // a short string ends at its line, and so does an f-/t-string with no open field
                continue;
            }

            if (field is not { InSpec: false })
            {
                // the literal's text, or a format spec
                if (c == '\\' && field != null)
                {
                    // spec text: only an escaped closing quote is two characters (f"{x:\">4}")
                    i += i + 1 < text.Length && text[i + 1] == literal.Quote ? 2 : 1;
                    continue;
                }
                if (c == '\\' && !literal.Raw)
                {
                    i += i + 1 < text.Length && !IsLineBreak(text[i + 1]) ? 2 : 1;
                    continue;
                }
                if (c == literal.Quote && (!literal.Triple || IsTripleAt(text, i)))
                {
                    var quoteLength = literal.Triple ? 3 : 1;
                    if (field != null)
                    {
                        // the literal's own quote in a format spec: the lexer's abort (#2276) — the quote's role is unknown
                        return FreshWalk(text, i, mode, ref walks) || FreshWalk(text, i + quoteLength, mode, ref walks);
                    }

                    i += quoteLength;
                    open.RemoveAt(open.Count - 1);
                    continue;
                }
                if (literal.Formatted && c == '{')
                {
                    // `{{` is an escaped brace in the text and in a spec; `{` opens a (nested) field
                    if (i + 1 < text.Length && text[i + 1] == '{')
                        i++;
                    else
                        (literal.Fields ??= new List<OpenField>()).Add(new OpenField());
                }
                else if (literal.Formatted && c == '}')
                {
                    // in a spec `}` ends the spec and closes its field; in the text `}}` is an escaped brace
                    if (field != null)
                        literal.CloseInnermostField();
                    else if (i + 1 < text.Length && text[i + 1] == '}')
                        i++;
                }
                i++;
                continue;
            }

            // a replacement field's expression
            switch (c)
            {
                case '#':
                    return true;    // a comment runs to the end of the line, and the field stays open past it
                case '{':
                    field.Braces++;
                    break;
                case '}' when field.Braces == 0:
                    literal.CloseInnermostField();
                    break;
                case '}':
                    field.Braces--;
                    break;
                case '(' or '[':
                    field.Brackets++;
                    break;
                case ')' or ']' when field.Brackets > 0:
                    field.Brackets--;
                    break;
                case ':' when field.Braces == 0 && field.Brackets == 0:
                    field.InSpec = true;
                    break;
                case '`':
                    SkipBacktickName(text, ref i);
                    continue;
                case '"' or '\'':
                    if (c == literal.Quote && !literal.Triple && !IsTripleAt(text, i) && !ClosesOnItsLine(text, i))
                    {
                        i++;
                        open.RemoveAt(open.Count - 1);
                    }
                    else
                        open.Add(OpenLiteral.At(text, ref i));
                    continue;
            }
            i++;
        }
        return open.Count > 0 && (mode == WalkMode.CloseLine || SpansTheLineBreak(open));
    }

    /// <summary>
    /// The text reached a line break (or its end) with the <paramref name="open"/> literals unclosed: a
    /// triple-quoted one, or one with a replacement field open (a literal nested in a field has a field open
    /// around it), continues on the next line.
    /// </summary>
    private static bool SpansTheLineBreak(List<OpenLiteral> open)
        => open.Count > 1 || open[^1].Triple || open[^1].Fields is { Count: > 0 };

    /// <summary>
    /// <see cref="SkipStringLiteralInPrescan(int, out bool)"/> over <paramref name="text"/> for the
    /// single-quoted literal whose quote is at <paramref name="quoteAt"/>: a backslash skips the next
    /// character; a line break before the closing quote → false.
    /// </summary>
    private static bool ClosesOnItsLine(ReadOnlySpan<char> text, int quoteAt)
    {
        var j = quoteAt + 1;
        while (j < text.Length)
        {
            if (text[j] == '\\')
                j += 2;
            else if (text[j] == text[quoteAt])
                return true;
            else if (IsLineBreak(text[j]))
                return false;
            else
                j++;
        }
        return false;
    }

    private static bool IsTripleAt(ReadOnlySpan<char> text, int quoteAt)
        => quoteAt + 2 < text.Length && text[quoteAt + 1] == text[quoteAt] && text[quoteAt + 2] == text[quoteAt];

    /// <summary>A literal <see cref="HoldsALiteralSpanningLines"/> has read the opener of and not yet closed.</summary>
    private sealed class OpenLiteral
    {
        public char Quote { get; private init; }
        public bool Triple { get; private init; }
        public bool Raw { get; private init; }
        public bool Formatted { get; private init; }

        /// <summary>The replacement fields open in this literal, innermost last (an f-/t-/df-string only).</summary>
        public List<OpenField>? Fields { get; set; }

        /// <summary>Closes the innermost open replacement field.</summary>
        public void CloseInnermostField()
        {
            var fields = Fields!;
            fields.RemoveAt(fields.Count - 1);
        }

        /// <summary>Opens the literal whose quote is at <paramref name="i"/> and moves past its opening quotes.</summary>
        public static OpenLiteral At(ReadOnlySpan<char> text, ref int i)
        {
            var prefix = PrefixBefore(text, i);
            var literal = new OpenLiteral
            {
                Quote = text[i],
                Triple = IsTripleAt(text, i),
                Raw = prefix is "r" or "dr",
                Formatted = prefix is "f" or "t" or "df",
            };
            i += literal.Triple ? 3 : 1;
            return literal;
        }
    }

    /// <summary>A replacement field <see cref="HoldsALiteralSpanningLines"/> has read the <c>{</c> of and not yet closed.</summary>
    private sealed class OpenField
    {
        public bool InSpec { get; set; }      // past the ':' that starts its format spec
        public int Braces { get; set; }       // {} nesting within its expression
        public int Brackets { get; set; }     // ()/[] nesting within its expression
    }

    private static bool IsLineBreak(char c) => c is '\n' or '\r';

    /// <summary>
    /// The string prefix spelled immediately before the quote at <paramref name="quoteAt"/>: the word that
    /// ends there when it is one the lexer recognises (<see cref="StringLiteralPrefixes"/>), else empty — a
    /// longer word (<c>xf"</c>, <c>rf"</c>) is an identifier followed by a plain string.
    /// </summary>
    private static string PrefixBefore(ReadOnlySpan<char> text, int quoteAt)
    {
        var start = quoteAt;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
            start--;
        var word = text[start..quoteAt];
        foreach (var (prefix, _) in StringLiteralStarts)
        {
            if (prefix.Length > 0 && word.SequenceEqual(prefix))
                return prefix;
        }
        return "";
    }
}
