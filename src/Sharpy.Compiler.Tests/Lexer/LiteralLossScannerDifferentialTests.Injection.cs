using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;

namespace Sharpy.Compiler.Tests.Lexer;

public partial class LiteralLossScannerDifferentialTests
{
    /// <summary>
    /// Every <see cref="InjectionStride"/>-th cell of the generated sequence is lexed (fixed, never random); 1 = all.
    /// The sequence is the ordered corpus × twins × lines × constructs, so the sample is a pure function of the tree.
    /// </summary>
    private const int InjectionStride = 1;

    /// <summary>The error constructs, in roster order (the <c>K</c> of the output line).</summary>
    private static readonly IReadOnlyList<(string Name, string Code)> InjectionConstructs = new[]
    {
        ("dollar", "SPY0015"),
        ("escape", "SPY0004"),
        ("conversion", "SPY0030"),
        ("tab", "SPY0012"),
    };

    /// <summary>
    /// One injected cell: the error construct, the clean document's 0-based line, where the text the lexer gives up
    /// starts in the CLEAN document (<paramref name="DroppedFrom"/>: the oracle's <c>at</c>), where it aborts
    /// (<paramref name="AbortAt"/>: tokens before it were read), and the edit — <paramref name="Remove"/> characters at
    /// <paramref name="EditAt"/> replaced by <paramref name="Insert"/>.
    /// </summary>
    internal sealed record Injection(string Construct, int Line, int DroppedFrom, int AbortAt, int EditAt, int Remove, string Insert, string Code);

    /// <summary>
    /// The roster of error constructs injected on line <paramref name="k"/> of a clean document (verification-contract
    /// §1's matrix: dropped-line text × injected construct; the text's content is the corpus and its twins):
    /// <list type="bullet">
    /// <item><c>dollar</c> — <c>$</c> at the line's first token start outside every literal (SPY0015): the given-up text
    /// is <c>[at, lineEnd)</c>.</item>
    /// <item><c>escape</c> — <c>\q</c> right after the opening quote of each non-raw short string token on the line that
    /// is a literal span of its own (not a literal nested in an f-string field) and closes on the line (SPY0004): the
    /// abort is INSIDE the literal, and <c>UnreadTextStart</c> starts the given-up text at its prefix.</item>
    /// <item><c>conversion</c> — <c>!q</c> before the <c>}</c> of each outermost replacement field of a single-line,
    /// top-level f-/t-string that has no conversion, format spec or self-documenting <c>=</c> (SPY0030): the given-up
    /// text starts at the f-string's prefix; the tokens of the f-string up to the abort were read.</item>
    /// <item><c>tab</c> — the line's leading whitespace replaced by a tab, when the line starts its logical line
    /// (SPY0012, an indentation error): the whole line is given up and no token of it was read.</item>
    /// </list>
    /// A line that starts inside a literal is skipped: an interior line has no site, and a CLOSE line's truth is the
    /// RePairedCloser rule (R-FR/R-FV, <c>LiteralStateTests</c>' hand rows), not a clean fact.
    /// </summary>
    internal static IEnumerable<Injection> Injections(CleanLineFacts facts)
    {
        var source = facts.Source;
        for (var k = 0; k < facts.Lines.Count; k++)
        {
            if (!facts.HasCode(k) || facts.StartsInsideLiteral(k))
                continue;
            var (lineStart, lineEnd) = facts.Lines[k];

            if (facts.FirstCodeTokenStart(k) is { } at)
                yield return new Injection("dollar", k, at, at, at, 0, "$", "SPY0015");

            foreach (var token in facts.Tokens)
            {
                if (token.Position < lineStart)
                    continue;
                if (token.Position >= lineEnd)
                    break;
                if (token.Type is not (TokenType.String or TokenType.ByteString))
                    continue;
                var span = (token.Position, token.Position + token.Length);
                if (!facts.Spans.Contains(span) || span.Item2 > lineEnd)
                    continue;   // nested in an f-string field (no span of its own), or not closed on its line
                var quote = source.IndexOfAny(new[] { '"', '\'' }, token.Position);
                if (quote + 2 < span.Item2 && source[quote + 1] == source[quote] && source[quote + 2] == source[quote])
                    continue;   // triple-quoted
                yield return new Injection("escape", k, token.Position, token.Position, quote + 1, 0, "\\q", "SPY0004");
            }

            foreach (var (fstringStart, exprEnd) in PlainOuterFields(facts, k, allowConversion: false))
                yield return new Injection("conversion", k, fstringStart, exprEnd, exprEnd, 0, "!q", "SPY0030");

            if (facts.StartsLogicalLine(k))
            {
                var indentEnd = lineStart;
                while (indentEnd < lineEnd && source[indentEnd] is ' ' or '\t')
                    indentEnd++;
                yield return new Injection("tab", k, lineStart, lineStart, lineStart, indentEnd - lineStart, "\t", "SPY0012");
            }
        }
    }

    /// <summary>
    /// The outermost replacement fields of the single-line, top-level f-/t-strings that start on line <paramref name="k"/>:
    /// (the f-string's start, the position of the field's closing <c>}</c>) for each field with no format spec and no
    /// self-documenting <c>=</c> (and no conversion unless <paramref name="allowConversion"/>).
    /// </summary>
    private static IEnumerable<(int FStringStart, int ExprEnd)> PlainOuterFields(CleanLineFacts facts, int k, bool allowConversion)
    {
        var (lineStart, lineEnd) = facts.Lines[k];
        var tokens = facts.Tokens;
        for (var i = 0; i < tokens.Count; i++)
        {
            var start = tokens[i];
            if (start.Position >= lineEnd)
                yield break;
            if (start.Type != TokenType.FStringStart || start.Position < lineStart)
                continue;
            if (!facts.Spans.Any(s => s.Start == start.Position && s.End <= lineEnd))
                continue;   // nested in a field (no span of its own), or not closed on its line
            var fdepth = 0;
            var field = 0;
            var plain = true;
            for (var j = i; j < tokens.Count; j++)
            {
                var token = tokens[j];
                if (token.Type == TokenType.FStringStart)
                    fdepth++;
                else if (token.Type == TokenType.FStringEnd && --fdepth == 0)
                {
                    i = j;
                    break;
                }
                if (fdepth != 1)
                    continue;
                if (token.Type == TokenType.FStringExprStart && field++ == 0)
                    plain = true;
                else if (token.Type == TokenType.FStringExprEnd && --field == 0 && plain)
                    yield return (start.Position, token.Position);
                else if (field == 1 && (token.Type is TokenType.FStringFormatSpec or TokenType.FStringSelfDoc
                    || (token.Type == TokenType.FStringConversion && !allowConversion)))
                    plain = false;
            }
        }
    }

    /// <summary>
    /// The CLEAN twins each corpus document is judged in, beside itself — corpus-derived constructs that put scanner arms
    /// the fixtures never reach into the given-up text (the C5 content axis), each a document the lexer must still read
    /// clean (a twin that does not is reported, never skipped):
    /// <list type="bullet">
    /// <item><c>@backtick</c> — every identifier outside an f-string respelled as a backtick name whose text holds
    /// <c>:("""'#</c>: the name must stay opaque to the scanner (<c>DroppedLineContinues</c>' and <c>WalkFrom</c>'s
    /// <c>SkipBacktickName</c> arms; no fixture spells a backtick name holding a bracket, colon, quote or <c>#</c>).</item>
    /// <item><c>@escaped-quote</c> — every top-level, non-raw short string gets its OWN quote, escaped, right after its
    /// opening quote (<c>"\"abc"</c>): the scanner must read the escape as the lexer does (<c>DroppedLineContinues</c>'
    /// escape arm — no fixture's short string holds an odd number of escaped own quotes, so without the twin a scanner
    /// that closed the string at <c>\"</c> agreed on every cell). Built only when the document has such a string.</item>
    /// <item><c>@unbalanced-brace</c> — <c>{{#</c> right after the opening quote(s) of every single-line, top-level
    /// f-/t-string: an escaped brace with no <c>}}</c> to pair it, then a <c>#</c> that is TEXT there. A scanner that
    /// read the <c>{{</c> as a field opener (<c>WalkFrom</c>'s escaped-brace arm dropped) reads the <c>#</c> as a comment
    /// in a field and the line as ending inside one. Every fixture's <c>{{</c> is balanced by a later <c>}}</c>, which
    /// pairs the misread out. A lone <c>{{</c> followed by plain text (<c>f"a{{b"</c>) pairs out too: the
    /// enclosing quote in a field closes the literal when no literal opened there closes on its line.
    /// Built only when the document has such an f-string.</item>
    /// <item><c>@field-split</c> — a line break inserted before the <c>}</c> of every outermost field of each
    /// single-line, top-level single-quoted f-/t-string with no spec or self-documenting <c>=</c> (PEP 701 whitespace in a
    /// hole, <c>fstrings/multiline_hole_2022</c>): the f-string's line now ends inside an open field, so the scanner's
    /// field reading — nested literals, the enclosing quote (<c>ClosesOnItsLine</c>), escapes and braces in the text —
    /// decides <c>DroppedOpener</c> there. Built only when the document has such a field.</item>
    /// </list>
    /// </summary>
    internal static IEnumerable<(string Twin, string Source)> Twins(CleanLineFacts facts)
    {
        var source = facts.Source;
        yield return ("", source);

        var backtick = new StringBuilder(source.Length * 2);
        var at = 0;
        var fdepth = 0;
        foreach (var token in facts.Tokens)
        {
            if (token.Type == TokenType.FStringStart)
                fdepth++;
            else if (token.Type == TokenType.FStringEnd && fdepth > 0)
                fdepth--;
            else if (fdepth == 0 && token.Type == TokenType.Identifier)
            {
                backtick.Append(source, at, token.Position - at).Append('`').Append(token.Value).Append(":(\"\"\"'#`");
                at = token.Position + token.Length;
            }
        }
        yield return ("@backtick", backtick.Append(source, at, source.Length - at).ToString());

        var escaped = new StringBuilder(source.Length + 64);
        at = 0;
        foreach (var token in facts.Tokens)
        {
            if (token.Type is not (TokenType.String or TokenType.ByteString) || !facts.Spans.Contains((token.Position, token.Position + token.Length)))
                continue;
            var quote = source.IndexOfAny(new[] { '"', '\'' }, token.Position);
            if (quote + 2 < token.Position + token.Length && source[quote + 1] == source[quote] && source[quote + 2] == source[quote])
                continue;   // triple-quoted
            escaped.Append(source, at, quote + 1 - at).Append('\\').Append(source[quote]);
            at = quote + 1;
        }
        if (at > 0)
            yield return ("@escaped-quote", escaped.Append(source, at, source.Length - at).ToString());

        var unbalanced = new StringBuilder(source.Length + 64);
        at = 0;
        fdepth = 0;
        foreach (var token in facts.Tokens)
        {
            if (token.Type == TokenType.FStringEnd && fdepth > 0)
                fdepth--;
            if (token.Type != TokenType.FStringStart || fdepth++ != 0)
                continue;
            var span = facts.Spans.FirstOrDefault(s => s.Start == token.Position);
            if (span.End <= span.Start || source.AsSpan(span.Start, span.End - span.Start).IndexOfAny('\n', '\r') >= 0)
                continue;   // not a top-level span, or it spans lines
            var textStart = token.Position + token.Value.Length;   // past the prefix and the opening quote(s)
            unbalanced.Append(source, at, textStart - at).Append("{{#");
            at = textStart;
        }
        if (at > 0)
            yield return ("@unbalanced-brace", unbalanced.Append(source, at, source.Length - at).ToString());

        var lineBreak = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : source.Contains('\r') ? "\r" : "\n";
        var splits = new List<int>();
        for (var k = 0; k < facts.Lines.Count; k++)
        {
            foreach (var (fstringStart, exprEnd) in PlainOuterFields(facts, k, allowConversion: true))
            {
                var quote = source.IndexOfAny(new[] { '"', '\'' }, fstringStart);
                if (!(quote + 2 < source.Length && source[quote + 1] == source[quote] && source[quote + 2] == source[quote]))
                    splits.Add(exprEnd);
            }
        }
        if (splits.Count > 0)
        {
            var split = new StringBuilder(source.Length + (splits.Count * 2));
            at = 0;
            foreach (var offset in splits)
            {
                split.Append(source, at, offset - at).Append(lineBreak);
                at = offset;
            }
            yield return ("@field-split", split.Append(source, at, source.Length - at).ToString());
        }
    }

    /// <summary><paramref name="facts"/>' source through line <paramref name="cell"/>.Line's break, with the cell's edit.</summary>
    private static string InjectedPrefix(CleanLineFacts facts, Injection cell)
    {
        var cut = facts.Lines[cell.Line].End + facts.LineBreakLength(cell.Line);
        var source = facts.Source;
        return string.Concat(source.AsSpan(0, cell.EditAt), cell.Insert, source.AsSpan(cell.EditAt + cell.Remove, cut - cell.EditAt - cell.Remove));
    }

    /// <summary>
    /// The verdicts of one injected cell against the clean facts: a misfire (the construct did not abort as designed —
    /// an instrument defect, never allowlistable) or the facts that disagree (empty when every fact agrees). The injected
    /// document is the clean PREFIX through the line (<see cref="InjectedPrefix"/>): error recovery resets the indent
    /// stack and the bracket depth, so on a whole document the lines below would abort again (cascade), and each later
    /// recovery would add its own flags to the cumulative <c>LiteralLoss</c> — the prefix makes every fact the injection's.
    /// </summary>
    internal static (string? Misfire, List<string> Disagreements, bool OpenerLine) Judge(CleanLineFacts facts, Injection cell)
    {
        var text = InjectedPrefix(facts, cell);
        var lexer = new LexerNs.Lexer(text);
        lexer.TokenizeAll();
        var errors = lexer.Diagnostics.GetErrors().ToList();
        if (errors.Count != 1 || errors[0].Code != cell.Code || errors[0].Line != cell.Line + 1
            || lexer.RecoveryResumes.Count != 1 || lexer.RecoveryResumes[0] != text.Length)
        {
            return ($"{cell.Code} expected once on L{cell.Line + 1}, got [{string.Join("; ", errors.Select(d => $"{d.Code}@{d.Line}:{d.Column}"))}] "
                + $"resumes [{string.Join(",", lexer.RecoveryResumes)}] of {text.Length}", new List<string>(), false);
        }

        var disagreements = new List<string>();
        var resume = lexer.RecoveryResumes[0];
        var dropped = (lexer.LiteralLoss & LexerNs.LiteralLoss.DroppedOpener) != 0;
        var crossing = facts.OpenerCrossing(cell.Line, cell.DroppedFrom);
        if (dropped != crossing)
            disagreements.Add($"DroppedOpener lexer={dropped} oracle={crossing}");
        if (facts.EndInsideLiteral(cell.Line))
            return (null, disagreements, true);   // the domain restriction: only DroppedOpener is a clean fact here

        var continued = lexer.RecoveryResumesAfterAContinuedLine.Contains(resume);
        var wantContinued = facts.Continued(cell.Line);
        if (continued != wantContinued)
            disagreements.Add($"Continued lexer={continued} oracle={wantContinued}");
        var header = lexer.RecoveryResumesAfterAPossibleHeader.Contains(resume);
        var wantHeader = facts.PossibleHeader(cell.Line, cell.AbortAt);
        if (header != wantHeader)
            disagreements.Add($"PossibleHeader lexer={header} oracle={wantHeader}");
        var bracket = lexer.BracketLeftOpenAtLine;
        var wantBracket = facts.BracketOpenerLine(cell.Line);
        if (bracket != wantBracket)
            disagreements.Add($"BracketLeftOpenAtLine lexer={bracket?.ToString() ?? "null"} oracle={wantBracket?.ToString() ?? "null"}");
        return (null, disagreements, false);
    }

    /// <summary>The clean-fact oracle's population: every <c>.spy</c> under <see cref="FixtureRoots.All"/>, build output excluded, in ordinal order.</summary>
    private static List<(string Name, string Source)> CorpusDocuments()
    {
        var documents = new List<(string, string)>();
        foreach (var root in FixtureRoots.All)
        {
            var files = Directory.EnumerateFiles(root.Path, "*.spy", SearchOption.AllDirectories)
                .Where(f => !Sharpy.Compiler.Diagnostics.CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(root.Path, f)))
                .OrderBy(f => f, StringComparer.Ordinal);
            foreach (var file in files)
                documents.Add((Path.GetRelativePath(FixtureRoots.RepositoryRoot, file).Replace('\\', '/'), File.ReadAllText(file)));
        }
        return documents;
    }

    /// <summary>
    /// The scanner ratchet (<c>Lexer/literal-loss-scanner-allowlist.txt</c>): rows <c>key # #NNNN reason</c>, the key
    /// <c>file:line:construct[@twin]</c> (injection cells) or <c>file:line:shape:NAME</c> (shape cells). A row with no
    /// <c>#\d+</c> cite fails; a row whose key the generated universe of its kind does not hold fails; a row whose cell
    /// agrees fails as stale (drain on fix). Every row is a findings-ledger line (plan Design Decision 4).
    /// </summary>
    internal sealed class ScannerAllowlist
    {
        public const string RelativePath = "src/Sharpy.Compiler.Tests/Lexer/literal-loss-scanner-allowlist.txt";

        private static readonly Regex Cite = new(@"#\d+", RegexOptions.Compiled);

        public Dictionary<string, string> Rows { get; } = new(StringComparer.Ordinal);

        public List<string> Malformed { get; } = new();

        public static ScannerAllowlist Load()
        {
            var path = Path.Combine(FixtureRoots.RepositoryRoot, RelativePath);
            File.Exists(path).Should().BeTrue("the ratchet's file arms it: {0}", path);
            var allowlist = new ScannerAllowlist();
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;
                var hash = line.IndexOf(" #", StringComparison.Ordinal);
                var key = (hash < 0 ? line : line[..hash]).Trim();
                if (hash < 0 || !Cite.IsMatch(line[hash..]))
                    allowlist.Malformed.Add($"no #NNNN issue cite: {raw}");
                else if (!allowlist.Rows.TryAdd(key, raw))
                    allowlist.Malformed.Add($"duplicate key: {raw}");
            }
            return allowlist;
        }

        public static bool IsShapeKey(string key) => key.Contains(":shape:", StringComparison.Ordinal);

        /// <summary>
        /// The ratchet's verdicts over one kind of key: the disagreeing keys the allowlist does not hold (failures), the
        /// rows of that kind whose key is outside the <paramref name="universe"/> or whose cell agrees (stale).
        /// </summary>
        public (List<string> Unlisted, List<string> Stale, int Allowlisted) Ratchet(
            Func<string, bool> ofKind, IReadOnlySet<string> universe, IReadOnlyDictionary<string, string> disagreements)
        {
            var unlisted = new List<string>();
            var stale = new List<string>();
            var allowlisted = 0;
            foreach (var (key, detail) in disagreements)
            {
                if (Rows.ContainsKey(key))
                    allowlisted++;
                else
                    unlisted.Add($"{key} :: {detail}");
            }
            foreach (var (key, raw) in Rows.Where(r => ofKind(r.Key)))
            {
                if (!universe.Contains(key))
                    stale.Add($"not in the generated universe: {raw}");
                else if (!disagreements.ContainsKey(key))
                    stale.Add($"the cell agrees now (drain it): {raw}");
            }
            return (unlisted, stale, allowlisted);
        }
    }

    /// <summary>
    /// P68 Phase 3 (#2300 row C5): the literal-loss scanner judged on the text the lexer GIVES UP — the only text it is
    /// for, which the clean-corpus differential above never shows it. Over every clean corpus document and its clean
    /// twins (<see cref="Twins"/>), each line × each error construct (<see cref="Injections"/>) is injected into the clean
    /// prefix through that line, and the lexer's recorded facts must equal the clean lexer's facts for that line
    /// (<see cref="CleanLineFacts"/>): <c>DroppedOpener</c> ≡ a literal span opens in the given-up text and crosses the
    /// line's end; and, on a line whose end is not inside a literal (the domain restriction), the resume is a continued
    /// one ≡ <see cref="CleanLineFacts.Continued"/>, a possible header ≡ <see cref="CleanLineFacts.PossibleHeader"/>, and
    /// <c>BracketLeftOpenAtLine</c> ≡ <see cref="CleanLineFacts.BracketOpenerLine"/>. A cell whose construct does not abort
    /// exactly once on its line is a misfire (instrument defect, fails). Disagreements are ratcheted by
    /// <see cref="ScannerAllowlist"/>.
    /// </summary>
    [Fact]
    public void Injected_ScannerAgreesWithTheCleanLexersFacts()
    {
        var clock = Stopwatch.StartNew();
        var allowlist = ScannerAllowlist.Load();
        var corpus = CorpusDocuments();
        var universe = new HashSet<string>(StringComparer.Ordinal);
        var disagreements = new Dictionary<string, string>(StringComparer.Ordinal);
        var misfires = new List<string>();
        var rejectedTwins = new List<string>();
        var perConstruct = InjectionConstructs.ToDictionary(c => c.Name, _ => 0);
        var perTwin = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var clean = 0;
        var documents = 0;
        var sites = 0;
        var cells = 0;
        var openerLines = 0;
        var closeLines = 0;
        var sequence = 0;

        foreach (var (name, source) in corpus)
        {
            var facts = CleanLineFacts.Of(source);
            if (facts == null)
                continue;
            clean++;
            foreach (var (twin, twinSource) in Twins(facts))
            {
                var twinFacts = twin.Length == 0 ? facts : CleanLineFacts.Of(twinSource);
                if (twinFacts == null)
                {
                    rejectedTwins.Add($"{name}{twin}");
                    continue;
                }
                documents++;
                perTwin[twin.Length == 0 ? "as-is" : twin[1..]] = perTwin.GetValueOrDefault(twin.Length == 0 ? "as-is" : twin[1..]) + 1;
                for (var k = 0; k < twinFacts.Lines.Count; k++)
                {
                    if (twinFacts.IsCloseLine(k) && twinFacts.HasCode(k))
                        closeLines++;
                }

                var lastSite = -1;
                foreach (var cell in Injections(twinFacts))
                {
                    if (sequence++ % InjectionStride != 0)
                        continue;
                    var offset = cell.Construct is "escape" or "conversion" ? $"@{cell.EditAt - twinFacts.Lines[cell.Line].Start}" : "";
                    var key = $"{name}:{cell.Line + 1}:{cell.Construct}{offset}{twin}";
                    universe.Add(key);
                    cells++;
                    perConstruct[cell.Construct]++;
                    if (cell.Line != lastSite)
                    {
                        sites++;
                        lastSite = cell.Line;
                    }

                    var (misfire, wrong, openerLine) = Judge(twinFacts, cell);
                    var text = Escape(twinFacts.Source[twinFacts.Lines[cell.Line].Start..twinFacts.Lines[cell.Line].End]);
                    if (misfire != null)
                    {
                        misfires.Add($"{key} :: {misfire} :: {text}");
                        continue;
                    }
                    if (openerLine)
                        openerLines++;
                    if (wrong.Count > 0)
                        disagreements[key] = $"{string.Join("; ", wrong)} :: {text}";
                }
            }
        }

        var (unlisted, stale, allowlisted) = allowlist.Ratchet(k => !ScannerAllowlist.IsShapeKey(k), universe, disagreements);
        clock.Stop();
        _output.WriteLine($"SCANNER-INJECT documents {documents} sites {sites} constructs {InjectionConstructs.Count} cells {cells} "
            + $"disagreements {disagreements.Count} allowlisted {allowlisted} stride {InjectionStride}");
        _output.WriteLine($"SCANNER-INJECT corpus {corpus.Count} clean {clean}; twins {string.Join(", ", perTwin.Select(t => $"{t.Key}={t.Value}"))} "
            + $"(rejected {rejectedTwins.Count}); cells per construct {string.Join(", ", perConstruct.Select(c => $"{c.Key}={c.Value}"))}; "
            + $"opener-crossing cells (DroppedOpener only) {openerLines}; close lines excluded {closeLines}; misfires {misfires.Count}; "
            + $"wall {clock.Elapsed.TotalSeconds:F1}s");
        foreach (var line in misfires.Take(50))
            _output.WriteLine("SCANNER-INJECT MISFIRE " + line);
        foreach (var line in rejectedTwins.Take(50))
            _output.WriteLine("SCANNER-INJECT TWIN-NOT-CLEAN " + line);
        foreach (var line in unlisted.Take(200))
            _output.WriteLine("SCANNER-INJECT DISAGREE " + line);
        foreach (var line in stale.Concat(allowlist.Malformed))
            _output.WriteLine("SCANNER-INJECT ALLOWLIST " + line);

        clean.Should().BeGreaterThan(3000, "the oracle's population is the fixture corpus");
        perConstruct.Where(c => c.Value == 0).Select(c => c.Key).Should().BeEmpty("every construct must reach the corpus");
        perTwin.Keys.Should().Contain(new[] { "as-is", "backtick", "escaped-quote", "unbalanced-brace", "field-split" }, "every twin must be built");
        misfires.Should().BeEmpty("a construct that does not abort exactly once on its line is outside the oracle");
        rejectedTwins.Should().BeEmpty("every twin is a clean document by construction");
        allowlist.Malformed.Should().BeEmpty();
        stale.Should().BeEmpty();
        unlisted.Should().BeEmpty();
    }

    private static string Escape(string text) => text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
}
