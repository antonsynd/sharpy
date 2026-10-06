using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Text;
using Sharpy.TestInfrastructure.Formatting;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
// The enclosing `Sharpy` namespace exposes Core's Pythonic collections, which shadow System's; SCG-qualify.
using SCG = System.Collections.Generic;
using CompilerNullLogger = Sharpy.Compiler.Logging.NullLogger;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SModule = Sharpy.Compiler.Parser.Ast.Module;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// The route-parity sweep of P22e (#2168): <b>every formatting route applies a text that was checked
/// AS APPLIED, or applies no edits.</b> It drives the REAL handlers through
/// <see cref="LspFormattingDriver"/> — <c>sharpyc format</c>'s <see cref="FormatterService.Format"/>,
/// Format Document, Format Selection and format-on-type — over the fixture corpus, applies their
/// edits the way a client must (<see cref="LspFormattingDriver.ApplyStrict"/>), and judges the
/// APPLIED text, never the edits.
///
/// <para><b>Corpus.</b> Every single-file fixture under <c>TestFixtures/</c> (<c>.skip</c> and
/// <c>Formatting/</c> included) that lexes and parses the way <see cref="FormatterService.Format"/>
/// reads it — the CLI-route sweep's rule (<c>FormatterMeaningPreservationSweepTests</c>, whose corpus
/// census lives in <c>Sharpy.Compiler.Tests</c>; this one is rebuilt from the same discovery helper).
/// One theory per twin <c>Q</c> (<see cref="FormatterTwins"/>): <c>identity</c>, <c>comment</c>
/// (comments before, inside and after every range), <c>wide</c> (8 spaces per level — the shape in
/// which a partial re-indent changes structure) and <c>crlf</c>. The backtick and line-trailing twins
/// are NOT swept: their subject is the unparser, which the CLI-route sweep owns; this sweep's subject is
/// the mapping from <c>Format(P)</c> to edits.</para>
///
/// <para><b>Documents and states.</b> A twin yields its documents (<see cref="Documents"/>): <c>Q</c>, and
/// <c>F = Format(Q)</c> when <c>Format</c> accepts <c>Q</c> and changes it (a declined <c>Format</c> is
/// counted, and the F-dependent cells skipped). The state is OBSERVED, never assumed: a document that
/// parses and is <c>Format</c>'s fixed point is <c>S1</c>, one that parses otherwise is <c>S2</c> — so an
/// already formatted fixture's <c>Q</c> is S1. Documents are de-duplicated by CONTENT across twins in
/// twin order (<c>Format(Wide(P))</c> is <c>Format(P)</c> on all but two stems, and the crlf twin's
/// <c>F</c> is written with <c>\n</c>): a later twin skips a document an earlier twin already swept —
/// equal text, equal state, equal cells.</para>
///
/// <para><b>Cells</b> (<see cref="Cells"/>; requests are 0-based LSP positions):
/// S2 — <c>cli</c>, <c>full</c>, <c>range-whole</c>, <c>range-statement</c> (each <c>Module.Body</c>
/// entry as whole lines <c>(a,0)–(b+1,0)</c>), <c>range-block</c> (each lexer <c>Indent…Dedent</c> line
/// span, <c>(a,0)–(b,len)</c>), <c>range-line</c> and <c>ontype</c> (each non-blank line);
/// S1 — <c>full</c>, <c>range-whole</c>, <c>range-statement</c>, <c>range-block</c>, <c>ontype</c>.
/// Part B of the task adds the unparseable states (S3 cut after a block opener, S4 cut inside a
/// multi-line literal, S5 an unmatched triple quote inserted) as more <see cref="SweepDocument"/>s and
/// their oracles beside <see cref="ParseableOracles"/>.</para>
///
/// <para><b>Oracles (buckets)</b>, <c>D</c> the document and <c>T</c> the applied text:
/// <list type="bullet">
/// <item><c>edits</c> — the strict applier threw (the edits are not a legal LSP edit list for D);</item>
/// <item><c>net</c> — <c>FormatterService.CheckMeaningPreserved(D, ast(D), T)</c> refuses T, called by
/// THIS test on the applied text whatever the handler did;</item>
/// <item><c>whole</c> — <c>cli</c>: <c>T != Format(D)</c>; <c>full</c>: the same modulo line breaks (the
/// handler documents writing <c>\n</c>, so on a CRLF document it is judged on content); <c>range-whole</c>:
/// <c>T != Format(D)</c>, and on a CRLF document equal modulo line breaks with no bare <c>\n</c> in T;</item>
/// <item><c>local</c> — range kinds: <c>T ∉ {D, Apply(D, selected)}</c>, <c>selected</c> the hunks of
/// <c>LineDiff.Hunks(D, Format(D))</c> the selection rule (<see cref="Selects"/>, plan decision 2,
/// RESTATED here) keeps;</item>
/// <item><c>fixedPoint</c> — S1: <c>T != D</c> on any route;</item>
/// <item><c>ontypeShape</c> — <c>ontype</c>: T differs from D other than in the requested line's leading
/// whitespace.</item>
/// </list>
/// <c>refusedWork</c> — a range cell with a non-empty <c>selected</c> whose T is D — is census, not a
/// failure.</para>
///
/// <para><b>Ratchet.</b> <c>Conformance/formatting-route-parity-allowlist.txt</c> lists
/// <c>stem twin route state bucket # #issue</c> (the bucket meanings are in its header); a row covers every cell of that route and state
/// on that stem and twin. A failing (route, state, bucket) must be listed; a listed one whose
/// (route, state) RAN in this mode and holds is stale (drain on fix), and so is one whose cell no longer
/// exists. <c>cli</c> and <c>full</c> rows on S1/S2 are refused by the loader: those routes apply
/// <c>Format</c>'s checked output whole, and a failure there is outside P22e's cure.</para>
///
/// <para><b>Budget.</b> <c>range-line</c> and <c>ontype</c> — the cells proportional to line count — run
/// for a STEM sample (<see cref="StableBucket"/>, stride <see cref="SampledStride"/>); every other cell
/// always runs. <c>SHARPY_ROUTE_SWEEP_FULL=1</c> runs every stem; the allowlist is populated from a
/// full run, and a row of a sampled-out stem's <c>range-line</c>/<c>ontype</c> is not judged.</para>
/// </summary>
[TestCaseOrderer("Sharpy.Lsp.Tests.Conformance.RouteParitySweepOrderer", "Sharpy.Lsp.Tests")]
public sealed class FormattingRouteParitySweepTests : IDisposable
{
    private const string AllowlistFileName = "formatting-route-parity-allowlist.txt";
    private const string FullModeVariable = "SHARPY_ROUTE_SWEEP_FULL";

    /// <summary>
    /// Stems are drawn for the line-proportional cells when <see cref="StableBucket"/> of the stem is 0.
    /// Chosen so the sampled sweep stays within its 90 s budget on the 8-thread Lsp.Tests runner.
    /// </summary>
    internal const int SampledStride = 8;

    // ---- twins ----
    internal const string Identity = "identity";
    internal const string Comment = "comment";
    internal const string Wide = "wide";
    internal const string Crlf = "crlf";
    internal static readonly string[] Twins = { Identity, Comment, Wide, Crlf };

    // ---- routes ----
    internal const string Cli = "cli";
    internal const string Full = "full";
    internal const string RangeWhole = "range-whole";
    internal const string RangeStatement = "range-statement";
    internal const string RangeBlock = "range-block";
    internal const string RangeLine = "range-line";
    internal const string OnType = "ontype";
    internal static readonly string[] Routes = { Cli, Full, RangeWhole, RangeStatement, RangeBlock, RangeLine, OnType };
    private static readonly SCG.HashSet<string> RangeRoutes = new(StringComparer.Ordinal) { RangeWhole, RangeStatement, RangeBlock, RangeLine };

    /// <summary>The routes whose cell count is proportional to line count — the only sampled ones.</summary>
    private static readonly SCG.HashSet<string> SampledRoutes = new(StringComparer.Ordinal) { RangeLine, OnType };

    // ---- states (part B adds S3, S4, S5) ----
    internal const string S1 = "S1";
    internal const string S2 = "S2";
    internal static readonly string[] States = { S1, S2 };

    /// <summary>The routes swept per state.</summary>
    internal static readonly SCG.IReadOnlyDictionary<string, string[]> RoutesByState = new SCG.Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [S1] = new[] { Full, RangeWhole, RangeStatement, RangeBlock, OnType },
        [S2] = new[] { Cli, Full, RangeWhole, RangeStatement, RangeBlock, RangeLine, OnType },
    };

    // ---- buckets (part B adds content, literal, depth) ----
    internal const string Edits = "edits";
    internal const string Net = "net";
    internal const string Whole = "whole";
    internal const string Local = "local";
    internal const string FixedPoint = "fixedPoint";
    internal const string OnTypeShape = "ontypeShape";
    internal static readonly string[] Buckets = { Edits, Net, Whole, Local, FixedPoint, OnTypeShape };

    private readonly ITestOutputHelper _output;
    private readonly LspFormattingDriver _driver = new();

    public FormattingRouteParitySweepTests(ITestOutputHelper output) => _output = output;

    public void Dispose() => _driver.Dispose();

    // ================================================================
    // Corpus and sampling
    // ================================================================

    internal sealed record Fixture(string Name, string Source);

    internal sealed record CorpusCensus(int SingleFile, IReadOnlyList<string> Unparseable, SCG.IReadOnlyDictionary<string, Fixture> Corpus);

    internal static readonly Lazy<CorpusCensus> Corpus = new(BuildCorpus);

    private static CorpusCensus BuildCorpus()
    {
        var single = FixtureDiscoveryHelper.DiscoverFixtures(FixtureRoots.CompilerTests.Path, includeSkipped: true)
            .Where(f => !f.IsMultiFile)
            .ToArray();
        var corpus = new SortedDictionary<string, Fixture>(StringComparer.Ordinal);
        var unparseable = new SCG.List<string>();
        foreach (var fixture in single)
        {
            var source = File.ReadAllText(fixture.SpyFilePath);
            if (Parse(source).Module == null)
                unparseable.Add(fixture.TestName);
            else
                corpus[fixture.TestName] = new Fixture(fixture.TestName, source);
        }

        return new CorpusCensus(single.Length, unparseable, corpus);
    }

    public static IEnumerable<object[]> CorpusNames() => Corpus.Value.Corpus.Keys.Select(name => new object[] { name });

    internal static bool FullMode => Environment.GetEnvironmentVariable(FullModeVariable) == "1";

    internal static int Stride => FullMode ? 1 : SampledStride;

    /// <summary>Whether <paramref name="stem"/>'s <c>range-line</c> and <c>ontype</c> cells run under <paramref name="stride"/>.</summary>
    internal static bool IsSampledIn(string stem, int stride) => StableBucket(stem, stride) == 0;

    /// <summary>Process-independent bucket in <c>[0, stride)</c>: FNV-1a of the name (the <c>FrontEndParityTests</c> precedent).</summary>
    internal static int StableBucket(string text, int stride)
    {
        if (stride <= 1)
            return 0;
        uint hash = 2166136261;
        foreach (var ch in text)
        {
            hash ^= ch;
            hash *= 16777619;
        }

        return (int)(hash % (uint)stride);
    }

    // ================================================================
    // Documents and cells
    // ================================================================

    /// <summary>
    /// One document a route is driven over: its text, observed state, and what the parseable oracles
    /// read (its AST, <c>Format(D)</c>, the hunks between them). Part B adds unparseable documents,
    /// whose ground truth is the parent twin's clean lex.
    /// </summary>
    internal sealed class SweepDocument
    {
        private readonly Lazy<IReadOnlyList<LineHunk>> _hunks;
        private readonly Lazy<bool> _netAcceptsSelf;
        private readonly Lazy<bool> _netAcceptsFormatted;

        public SweepDocument(string text, string state, SModule? ast, string formatted, bool formatDeclined,
            IReadOnlyList<(int First, int Last)> statementLines, IReadOnlyList<(int First, int Last)> blockLines)
        {
            Text = text;
            State = state;
            Ast = ast;
            Formatted = formatted;
            FormatDeclined = formatDeclined;
            StatementLines = statementLines;
            BlockLines = blockLines;
            Lines = LineDiff.Split(text).Lines;
            IsCrlf = LineDiff.DocumentLineBreak(text) == "\r\n";
            _hunks = new(() => LineDiff.Hunks(Text, Formatted));
            _netAcceptsSelf = new(() => Ast == null || FormatterService.CheckMeaningPreserved(Text, Ast, Text) == null);
            _netAcceptsFormatted = new(() => Ast == null || FormatterService.CheckMeaningPreserved(Text, Ast, Formatted) == null);
        }

        public string Text { get; }
        public string State { get; }
        public SModule? Ast { get; }

        /// <summary><c>Format(D).FormattedText</c> — D itself when <c>Format</c> declines.</summary>
        public string Formatted { get; }
        public bool FormatDeclined { get; }

        /// <summary>0-based first and last line of each <c>Module.Body</c> entry.</summary>
        public IReadOnlyList<(int First, int Last)> StatementLines { get; }

        /// <summary>0-based first and last line of each lexer <c>Indent…Dedent</c> block.</summary>
        public IReadOnlyList<(int First, int Last)> BlockLines { get; }

        /// <summary>Line contents (<see cref="LineDiff.Split"/>), terminators excluded.</summary>
        public IReadOnlyList<string> Lines { get; }

        public bool IsCrlf { get; }

        public IReadOnlyList<LineHunk> Hunks => _hunks.Value;

        /// <summary>
        /// The test's own <c>CheckMeaningPreserved(D, ast(D), T)</c>, memoized for the two applied texts
        /// most cells produce (D and <c>Format(D)</c>) — the verdict is a function of (D, T).
        /// </summary>
        public string? NetRefusal(string applied)
        {
            if (Ast == null)
                return null;
            if (applied == Text)
                return _netAcceptsSelf.Value ? null : "the net refuses D itself";
            if (applied == Formatted)
                return _netAcceptsFormatted.Value ? null : "the net refuses Format(D)";
            return FormatterService.CheckMeaningPreserved(Text, Ast, applied)?.Message;
        }
    }

    /// <summary>One request: a route and its range (range kinds) or line (<c>ontype</c>).</summary>
    internal sealed record CellRequest(string Route, LspRange? Range, int Line)
    {
        public override string ToString() => Range is { } r
            ? $"{Route}[{r.Start.Line}:{r.Start.Character}-{r.End.Line}:{r.End.Character}]"
            : Route == OnType ? $"{Route}@{Line}" : Route;
    }

    /// <summary>
    /// The twin's text. <c>comment</c>'s and <c>wide</c>'s are checked by the twins' own tests and
    /// instrument checks (<c>FormatterTwinsTests</c>, the CLI-route sweep); here only their parse is
    /// observed.
    /// </summary>
    internal static string TwinText(string source, string twin) => twin switch
    {
        Identity => source,
        Comment => FormatterTwins.CommentInjected(source).Text,
        Wide => FormatterTwins.WideIndented(source),
        Crlf => FormatterTwins.Crlf(source),
        _ => throw new ArgumentOutOfRangeException(nameof(twin), twin, null),
    };

    /// <summary>
    /// Lexes (trivia lexer) and parses <paramref name="text"/> the way <see cref="FormatterService.Format"/>
    /// reads it, and formats it; null when it does not parse (part B's states).
    /// </summary>
    internal static SweepDocument? ObserveParseable(string text)
    {
        var (tokens, module) = Parse(text);
        if (module == null)
            return null;
        var format = FormatterService.Format(text);
        var declined = format.Diagnostics.Count > 0;
        var state = !declined && format.FormattedText == text ? S1 : S2;
        return new SweepDocument(text, state, module, format.FormattedText, declined,
            StatementLines(module), BlockLines(tokens));
    }

    private static (IReadOnlyList<Token> Tokens, SModule? Module) Parse(string text)
    {
        var lex = FileCompilationPipeline.Lex(new SourceText(text, "<route-sweep>"), CompilerNullLogger.Instance, preserveTrivia: true);
        if (lex.HasErrors)
            return (lex.Tokens, null);
        var parse = FileCompilationPipeline.Parse(lex.Tokens, CompilerNullLogger.Instance);
        return (lex.Tokens, parse.HasErrors ? null : parse.Module);
    }

    private static IReadOnlyList<(int First, int Last)> StatementLines(SModule module)
        => module.Body.Where(s => s.LineStart > 0 && s.LineEnd >= s.LineStart)
            .Select(s => (s.LineStart - 1, s.LineEnd - 1))
            .ToArray();

    /// <summary>
    /// Each <c>Indent</c> and its matching <c>Dedent</c>: from the first token after the <c>Indent</c> to the
    /// last token before the <c>Dedent</c> (the <c>Newline</c> ending the block's last logical line).
    /// </summary>
    private static IReadOnlyList<(int First, int Last)> BlockLines(IReadOnlyList<Token> tokens)
    {
        var spans = new SCG.List<(int, int)>();
        var open = new Stack<int>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Type == TokenType.Indent)
            {
                open.Push(i);
            }
            else if (tokens[i].Type == TokenType.Dedent && open.Count > 0)
            {
                var start = open.Pop();
                if (start + 1 < i && tokens[start + 1].Line > 0 && tokens[i - 1].Line >= tokens[start + 1].Line)
                    spans.Add((tokens[start + 1].Line - 1, tokens[i - 1].Line - 1));
            }
        }

        spans.Sort();
        return spans;
    }

    /// <summary>A content hash of a document, for de-duplication across twins.</summary>
    private static string ContentKey(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>Per (stem, twin): the content keys of the documents that twin sweeps (filled as twins run, computed when absent).</summary>
    private static readonly ConcurrentDictionary<(string Stem, string Twin), string[]> s_documentKeys = new();

    /// <summary>
    /// The documents of one (stem, twin): <c>Q</c> and, when <c>Format</c> accepts and changes it,
    /// <c>F = Format(Q)</c>; each dropped when an earlier twin (in <see cref="Twins"/> order) already has a
    /// document with the same content. <paramref name="declined"/> reports a declined <c>Format(Q)</c>.
    /// Throws when Q or F does not parse: the corpus parses, and the twins and the net preserve that.
    /// </summary>
    internal static SCG.List<SweepDocument> Documents(string stem, string twin, out bool declined)
    {
        var source = Corpus.Value.Corpus[stem].Source;
        var seen = new SCG.HashSet<string>(StringComparer.Ordinal);
        foreach (var earlier in Twins.TakeWhile(t => t != twin))
            seen.UnionWith(s_documentKeys.GetOrAdd((stem, earlier), key => Keys(source, key.Twin)));

        var all = OwnDocuments(source, twin, out declined);
        s_documentKeys.TryAdd((stem, twin), all.Select(d => ContentKey(d.Text)).ToArray());
        return all.Where(d => !seen.Contains(ContentKey(d.Text))).ToList();

        static string[] Keys(string source, string twin)
            => OwnDocuments(source, twin, out _).Select(d => ContentKey(d.Text)).ToArray();
    }

    private static SCG.List<SweepDocument> OwnDocuments(string source, string twin, out bool declined)
    {
        var q = TwinText(source, twin);
        var qDoc = ObserveParseable(q)
            ?? throw new InvalidOperationException($"instrument: the {twin} twin does not parse");
        declined = qDoc.FormatDeclined;
        var docs = new SCG.List<SweepDocument> { qDoc };
        if (!qDoc.FormatDeclined && qDoc.Formatted != q)
        {
            docs.Add(ObserveParseable(qDoc.Formatted)
                ?? throw new InvalidOperationException($"instrument: Format accepted an output of the {twin} twin that does not parse"));
        }

        return docs;
    }

    /// <summary>The requests swept on <paramref name="d"/>: <see cref="RoutesByState"/>, enumerated per route kind.</summary>
    internal static IEnumerable<CellRequest> Cells(SweepDocument d)
    {
        var last = d.Lines.Count - 1;
        foreach (var route in RoutesByState[d.State])
        {
            switch (route)
            {
                case Cli or Full:
                    yield return new CellRequest(route, null, 0);
                    break;
                case RangeWhole:
                    yield return new CellRequest(route, R(0, 0, last, d.Lines[last].Length), 0);
                    break;
                case RangeStatement:
                    foreach (var (a, b) in d.StatementLines)
                    {
                        yield return new CellRequest(route,
                            b + 1 <= last ? R(a, 0, b + 1, 0) : R(a, 0, b, d.Lines[b].Length), 0);
                    }

                    break;
                case RangeBlock:
                    foreach (var (a, b) in d.BlockLines)
                        yield return new CellRequest(route, R(a, 0, b, d.Lines[b].Length), 0);
                    break;
                case RangeLine:
                    foreach (var l in NonBlankLines(d))
                        yield return new CellRequest(route, R(l, 0, l, d.Lines[l].Length), l);
                    break;
                case OnType:
                    foreach (var l in NonBlankLines(d))
                        yield return new CellRequest(route, null, l);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(route), route, null);
            }
        }
    }

    private static IEnumerable<int> NonBlankLines(SweepDocument d)
        => Enumerable.Range(0, d.Lines.Count).Where(l => !string.IsNullOrWhiteSpace(d.Lines[l]));

    private static LspRange R(int startLine, int startChar, int endLine, int endChar)
        => new(new Position(startLine, startChar), new Position(endLine, endChar));

    // ================================================================
    // Running a cell and the oracles
    // ================================================================

    /// <summary>The text a route applies to <paramref name="text"/> for <paramref name="request"/>.</summary>
    internal static string Apply(LspFormattingDriver driver, string text, CellRequest request) => request.Route switch
    {
        Cli => FormatterService.Format(text).FormattedText,
        Full => driver.Full(text).Applied,
        OnType => driver.OnType(text, request.Line).Applied,
        _ => driver.Range(text, request.Range!).Applied,
    };

    /// <summary>One cell's verdict: its failing buckets and whether it is a <c>refusedWork</c> census cell.</summary>
    internal sealed record CellVerdict(SCG.List<(string Bucket, string Detail)> Failures, bool RefusedWork);

    /// <summary>Runs <paramref name="apply"/>; a strict-applier refusal is <c>edits</c> and nothing else is judged.</summary>
    internal static CellVerdict Judge(SweepDocument d, CellRequest request, Func<string> apply)
    {
        string applied;
        try
        {
            applied = apply();
        }
        catch (StrictEditApplicationException e)
        {
            return new CellVerdict(new() { (Edits, $"{request}: {e.Message}") }, false);
        }

        return ParseableOracles(d, request, applied);
    }

    /// <summary>The oracles of a document that parses (S1, S2) over the applied text <paramref name="applied"/>.</summary>
    internal static CellVerdict ParseableOracles(SweepDocument d, CellRequest request, string applied)
    {
        var failures = new SCG.List<(string, string)>();
        var refusedWork = false;
        var route = request.Route;

        if (d.NetRefusal(applied) is { } refusal)
            failures.Add((Net, $"{request}: {refusal}"));

        var wholeFails = route switch
        {
            Cli => applied != d.Formatted,
            Full => NormalizeLineBreaks(applied) != NormalizeLineBreaks(d.Formatted),
            RangeWhole => d.IsCrlf
                ? NormalizeLineBreaks(applied) != NormalizeLineBreaks(d.Formatted) || HasBareLineFeed(applied)
                : applied != d.Formatted,
            _ => false,
        };
        if (wholeFails)
            failures.Add((Whole, $"{request}: {FirstDifference(d.Formatted, applied, "Format(D)", "T")}"));

        if (RangeRoutes.Contains(route))
        {
            var (s, e) = SelectedLines(request.Range!, d.Lines);
            var selected = d.Hunks.Where(h => Selects(h, s, e, d.Lines.Count)).ToList();
            if (applied != d.Text)
            {
                var expected = LineDiff.Apply(d.Text, selected);
                if (applied != expected)
                    failures.Add((Local, $"{request} selects lines {s}..{e}: {FirstDifference(expected, applied, "Apply(D, selected)", "T")}"));
            }
            else if (selected.Count > 0 && LineDiff.Apply(d.Text, selected) != d.Text)
            {
                refusedWork = true;
            }
        }

        if (d.State == S1 && applied != d.Text)
            failures.Add((FixedPoint, $"{request}: {FirstDifference(d.Text, applied, "F", "T")}"));

        if (route == OnType && WithoutLeadingWhitespace(applied, request.Line) != WithoutLeadingWhitespace(d.Text, request.Line))
            failures.Add((OnTypeShape, $"{request}: {FirstDifference(d.Text, applied, "D", "T")}"));

        return new CellVerdict(failures, refusedWork);
    }

    /// <summary>
    /// Plan P22e decision 2's selected lines <c>[s, e]</c>, RESTATED (never called from production):
    /// <c>s = Start.Line</c>; <c>e = End.Line</c>, minus one when <c>End.Character == 0</c> and
    /// <c>End.Line &gt; Start.Line</c> (an editor's whole-line selection ends at column 0 of the next line);
    /// and when <c>e</c> reaches the last line before a final line break, the empty final line is selected
    /// too.
    /// </summary>
    internal static (int Start, int End) SelectedLines(LspRange range, IReadOnlyList<string> lines)
    {
        var n = lines.Count;
        var s = range.Start.Line;
        var e = range.End.Line;
        if (range.End.Character == 0 && range.End.Line > range.Start.Line)
            e--;
        var endsWithLineBreak = n >= 2 && lines[n - 1].Length == 0;
        if (endsWithLineBreak && e >= n - 2)
            e = n - 1;
        return (s, Math.Min(e, n - 1));
    }

    /// <summary>
    /// Decision 2's hunk predicate over source lines <c>[s, e]</c> of an <paramref name="n"/>-line document,
    /// RESTATED: a replacement or deletion of <c>[a, b)</c> is selected iff it touches the selection
    /// (<c>a ≤ e ∧ b − 1 ≥ s</c>); a pure insertion before line <c>a</c> iff both neighbours that exist
    /// are selected. The plan writes the insertion arm as <c>(a == 0 ∨ a − 1 ≥ s) ∧ (a == n ∨ a ≤ e)</c>,
    /// which agrees on interior insertions but at the edges would select an insertion at line 0 for any
    /// selection; the prose ("both neighbours") is what is restated here.
    /// </summary>
    internal static bool Selects(LineHunk hunk, int s, int e, int n)
    {
        int a = hunk.Start, b = hunk.End;
        if (b > a)
            return a <= e && b - 1 >= s;
        return (a == 0 || (a - 1 >= s && a - 1 <= e)) && (a == n || (a >= s && a <= e));
    }

    internal static string NormalizeLineBreaks(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private static bool HasBareLineFeed(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n' && (i == 0 || text[i - 1] != '\r'))
                return true;
        }

        return false;
    }

    /// <summary><paramref name="text"/> with line <paramref name="line"/>'s leading spaces and tabs removed, terminators kept.</summary>
    internal static string WithoutLeadingWhitespace(string text, int line)
    {
        var (lines, breaks) = LineDiff.Split(text);
        if (line >= 0 && line < lines.Count)
            lines[line] = lines[line].TrimStart(' ', '\t');
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < lines.Count; i++)
        {
            sb.Append(lines[i]);
            if (i < breaks.Count)
                sb.Append(breaks[i]);
        }

        return sb.ToString();
    }

    private static string FirstDifference(string expected, string actual, string expectedName, string actualName)
    {
        var x = LineDiff.Split(expected).Lines;
        var y = LineDiff.Split(actual).Lines;
        for (var i = 0; i < Math.Max(x.Count, y.Count); i++)
        {
            var l = i < x.Count ? x[i] : "<end>";
            var r = i < y.Count ? y[i] : "<end>";
            if (l != r)
                return $"line {i}: {expectedName} '{Clip(l)}' vs {actualName} '{Clip(r)}' ({x.Count} vs {y.Count} lines)";
        }

        return $"{expectedName} and {actualName} differ only in line breaks";
    }

    private static string Clip(string text) => text.Length > 120 ? text[..120] + "…" : text;

    // ================================================================
    // The theories
    // ================================================================

    [Theory]
    [MemberData(nameof(CorpusNames))]
    public void Identity_EveryRouteAppliesACheckedText(string stem) => Sweep(stem, Identity);

    [Theory]
    [MemberData(nameof(CorpusNames))]
    public void CommentInjected_EveryRouteAppliesACheckedText(string stem) => Sweep(stem, Comment);

    [Theory]
    [MemberData(nameof(CorpusNames))]
    public void WideIndented_EveryRouteAppliesACheckedText(string stem) => Sweep(stem, Wide);

    [Theory]
    [MemberData(nameof(CorpusNames))]
    public void Crlf_EveryRouteAppliesACheckedText(string stem) => Sweep(stem, Crlf);

    /// <summary>The cells of one (route, state) group of a (stem, twin): counts and the first detail per failing bucket.</summary>
    internal sealed class Group
    {
        public int Cells;
        public int RefusedWork;
        public readonly SortedDictionary<string, (int Count, string First)> Failures = new(StringComparer.Ordinal);
    }

    /// <summary>Everything the census reads from the theories that ran in this process.</summary>
    private static readonly ConcurrentDictionary<(string Route, string State, string Twin), int> s_cellsRun = new();
    private static readonly ConcurrentDictionary<(string Route, string Twin), int> s_refusedWork = new();
    private static readonly ConcurrentDictionary<(string Stem, string Twin), bool> s_declined = new();
    private static long s_sweepTicks;

    private void Sweep(string stem, string twin)
    {
        var clock = Stopwatch.StartNew();
        var stride = Stride;
        var sampledIn = IsSampledIn(stem, stride);
        var groups = RunCells(_driver, stem, twin, sampledIn, out var declined);
        Interlocked.Add(ref s_sweepTicks, clock.Elapsed.Ticks);
        if (declined)
            s_declined[(stem, twin)] = true;
        foreach (var ((route, state), group) in groups)
        {
            s_cellsRun.AddOrUpdate((route, state, twin), group.Cells, (_, n) => n + group.Cells);
            if (group.RefusedWork > 0)
                s_refusedWork.AddOrUpdate((route, twin), group.RefusedWork, (_, n) => n + group.RefusedWork);
        }

        var problems = Ratchet(stem, twin, groups, sampledIn, AllowlistByCell.Value[(stem, twin)]);

        var failing = groups.SelectMany(g => g.Value.Failures.Keys.Select(b => (g.Key.Route, g.Key.State, Bucket: b))).ToList();
        _output.WriteLine($"FMTROUTE {stem} {twin} cells={groups.Values.Sum(g => g.Cells)} sampled={(sampledIn ? "in" : "out")} "
            + (failing.Count == 0 ? "ok" : $"rows={failing.Count}"));
        foreach (var (route, state, bucket) in failing)
            _output.WriteLine($"FMTROUTE-ROW {stem} {twin} {route} {state} {bucket}");

        if (problems.Count > 0)
        {
            Assert.Fail($"{stem} ({twin} twin): every formatting route must apply a text checked as applied.\n  "
                + string.Join("\n  ", problems));
        }
    }

    /// <summary>Runs every cell of (stem, twin) — the sampled routes only when <paramref name="sampledIn"/> — grouped by (route, state).</summary>
    internal static SortedDictionary<(string Route, string State), Group> RunCells(
        LspFormattingDriver driver, string stem, string twin, bool sampledIn, out bool declined)
    {
        var docs = Documents(stem, twin, out declined);
        var groups = new SortedDictionary<(string Route, string State), Group>();
        var work = new SCG.List<(SweepDocument Doc, CellRequest Request, Group Group)>();
        foreach (var doc in docs)
        {
            foreach (var request in Cells(doc))
            {
                if (SampledRoutes.Contains(request.Route) && !sampledIn)
                    continue;
                var key = (request.Route, doc.State);
                if (!groups.TryGetValue(key, out var group))
                    groups[key] = group = new Group();
                work.Add((doc, request, group));
            }
        }

        Parallel.ForEach(work, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, item =>
        {
            var verdict = Judge(item.Doc, item.Request, () => Apply(driver, item.Doc.Text, item.Request));
            lock (item.Group)
            {
                item.Group.Cells++;
                if (verdict.RefusedWork)
                    item.Group.RefusedWork++;
                foreach (var (bucket, detail) in verdict.Failures)
                {
                    item.Group.Failures[bucket] = item.Group.Failures.TryGetValue(bucket, out var seen)
                        ? (seen.Count + 1, seen.First)
                        : (1, detail);
                }
            }
        });
        return groups;
    }

    /// <summary>
    /// The allowlist ratchet for one (stem, twin): every failing (route, state, bucket) is listed; a listed
    /// one whose (route, state) ran and holds is stale, and so is one whose (route, state) has no cell — unless
    /// it is a sampled route of a sampled-out stem, which this mode did not run.
    /// </summary>
    internal static SCG.List<string> Ratchet(string stem, string twin,
        SCG.IReadOnlyDictionary<(string Route, string State), Group> groups, bool sampledIn, IEnumerable<Row> rows)
    {
        var problems = new SCG.List<string>();
        var listed = rows.ToDictionary(r => (r.Route, r.State, r.Bucket), r => r.Cite);
        foreach (var ((route, state), group) in groups)
        {
            foreach (var (bucket, (count, first)) in group.Failures)
            {
                if (!listed.ContainsKey((route, state, bucket)))
                    problems.Add($"{route} {state} {bucket} ({count}/{group.Cells} cells): {first}");
            }
        }

        foreach (var ((route, state, bucket), cite) in listed)
        {
            var drain = $"drain: delete the row `{stem} {twin} {route} {state} {bucket}` from Conformance/{AllowlistFileName}";
            if (groups.TryGetValue((route, state), out var group))
            {
                if (!group.Failures.ContainsKey(bucket))
                    problems.Add($"{route} {state} {bucket} holds now on all {group.Cells} cells — {cite} is fixed for this cell group: {drain}");
            }
            else if (!(SampledRoutes.Contains(route) && !sampledIn))
            {
                problems.Add($"{route} {state} has no cell on this stem and twin — {drain}");
            }
        }

        return problems;
    }

    // ================================================================
    // Allowlist
    // ================================================================

    internal sealed record Row(string Stem, string Twin, string Route, string State, string Bucket, string Cite);

    private static readonly Lazy<IReadOnlyList<Row>> Allowlist = new(ReadAllowlist);

    private static readonly Lazy<ILookup<(string Stem, string Twin), Row>> AllowlistByCell =
        new(() => Allowlist.Value.ToLookup(row => (row.Stem, row.Twin)));

    /// <summary><c>stem twin route state bucket # #issue</c> (a reason after the cite is allowed); <c>#</c> starts the cite only after whitespace.</summary>
    internal static IReadOnlyList<Row> ParseAllowlist(IEnumerable<string> lines)
    {
        var rows = new SCG.List<Row>();
        var keys = new SCG.HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var split = line.IndexOf(" #", StringComparison.Ordinal);
            var fields = (split < 0 ? line : line[..split]).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cite = split < 0 ? "" : line[(split + 2)..].Trim();
            if (fields.Length != 5 || !Twins.Contains(fields[1]) || !Routes.Contains(fields[2])
                || !States.Contains(fields[3]) || !Buckets.Contains(fields[4]))
            {
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' must read `stem twin route state bucket # #issue` "
                    + $"with twin in {{{string.Join(", ", Twins)}}}, route in {{{string.Join(", ", Routes)}}}, state in {{{string.Join(", ", States)}}} "
                    + $"and bucket in {{{string.Join(", ", Buckets)}}}.");
            }

            if (fields[2] is Cli or Full && fields[3] is S1 or S2)
            {
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' — `cli` and `full` on a parseable document apply "
                    + "Format's checked output whole; a failure there is a finding outside P22e's cure, never a row.");
            }

            if (!Regex.IsMatch(cite, @"^#\d+\b"))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' must cite an issue first (# #N reason).");
            if (!keys.Add(string.Join(' ', fields)))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' is listed twice.");
            rows.Add(new Row(fields[0], fields[1], fields[2], fields[3], fields[4], cite));
        }

        return rows;
    }

    private static IReadOnlyList<Row> ReadAllowlist()
    {
        var path = FindAllowlistPath()
            ?? throw new InvalidOperationException($"Conformance/{AllowlistFileName} is missing. Its presence is what arms the ratchet.");
        return ParseAllowlist(File.ReadAllLines(path));
    }

    private static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var dir = Path.Combine(current, "src", "Sharpy.Lsp.Tests", "Conformance");
            if (Directory.Exists(dir))
            {
                var path = Path.Combine(dir, AllowlistFileName);
                return File.Exists(path) ? path : null;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }

    // ================================================================
    // Positive controls — each bucket fires on a damaged applied text and not on the clean one
    // ================================================================

    private const string D1 = "def helper() -> int:\n    return 2\ndef main():\n    x   =   helper()\n    print(x)\n";
    private const string D1Formatted = "def helper() -> int:\n    return 2\n\n\ndef main():\n    x = helper()\n    print(x)\n";
    private const string D2 = "def main():\n    xs = [1,\n          2]\n    # keep me\n    y = 3\n    print(xs, y)\n";
    private const string D13 = "def main():\n    xs = [1,  # one\n          2]\n    print(xs)\n";

    private static SweepDocument Doc(string text) => ObserveParseable(text) ?? throw new InvalidOperationException("control document does not parse");

    private static CellRequest RangeLines(SweepDocument d, int first, int last)
        => new(RangeLine, R(first, 0, last, d.Lines[last].Length), first);

    private static string[] BucketsOf(SweepDocument d, CellRequest request, string applied)
        => ParseableOracles(d, request, applied).Failures.Select(f => f.Bucket).ToArray();

    /// <summary>
    /// <c>net</c>: cell 2 (D2, lines 1..4 — <c>print(xs, y)</c> written twice, the program prints twice)
    /// and cell 1 (D1, main's body — <c>def main():</c> twice, its body gone). The texts are the ones the
    /// range handler applied @ 7a034b81d. Clean: <c>Format(D)</c> and D itself.
    /// </summary>
    [Fact]
    public void PositiveControl_Net_SeesTheProgramChangeOfCells1And2()
    {
        var d2 = Doc(D2);
        var cell2 = "def main():\n    xs = [1, 2]\n    # keep me\n    y = 3\n    print(xs, y)\n    print(xs, y)\n";
        BucketsOf(d2, RangeLines(d2, 1, 4), cell2).Should().Contain(Net);
        BucketsOf(d2, RangeLines(d2, 1, 4), d2.Formatted).Should().NotContain(Net);
        BucketsOf(d2, RangeLines(d2, 1, 4), D2).Should().NotContain(Net);

        var d1 = Doc(D1);
        var cell1 = "def helper() -> int:\n    return 2\ndef main():\n\ndef main():\n";
        BucketsOf(d1, RangeLines(d1, 3, 4), cell1).Should().Contain(Net);
        BucketsOf(d1, RangeLines(d1, 3, 4), D1.Replace("x   =   helper()", "x = helper()")).Should().NotContain(Net);
    }

    /// <summary><c>whole</c>: cell 4 (D1, whole-document range — the last line and the final line break lost). Clean: <c>Format(D1)</c>.</summary>
    [Fact]
    public void PositiveControl_Whole_SeesCell4sLostLastLine()
    {
        var d1 = Doc(D1);
        d1.Formatted.Should().Be(D1Formatted);
        var whole = new CellRequest(RangeWhole, R(0, 0, d1.Lines.Count - 1, 0), 0);
        var cell4 = "def helper() -> int:\n    return 2\n\n\ndef main():\n    x = helper()";

        BucketsOf(d1, whole, cell4).Should().Contain(Whole);
        BucketsOf(d1, whole, D1Formatted).Should().NotContain(Whole).And.NotContain(Local);
        BucketsOf(d1, new CellRequest(Full, null, 0), cell4).Should().Contain(Whole);
        BucketsOf(d1, new CellRequest(Full, null, 0), D1Formatted).Should().BeEmpty();
    }

    /// <summary>
    /// <c>whole</c> on a CRLF document, <c>range-whole</c>: <c>Format(D)</c> written with bare <c>\n</c> fails;
    /// the same text with CRLF passes. <c>full</c> is judged modulo line breaks.
    /// </summary>
    [Fact]
    public void PositiveControl_Whole_OnACrlfDocument_RangeWholeKeepsCrlf()
    {
        var d = Doc(FormatterTwins.Crlf(D1));
        d.IsCrlf.Should().BeTrue();
        var whole = new CellRequest(RangeWhole, R(0, 0, d.Lines.Count - 1, 0), 0);

        BucketsOf(d, whole, D1Formatted).Should().Contain(Whole);
        BucketsOf(d, whole, D1Formatted.Replace("\n", "\r\n")).Should().NotContain(Whole).And.NotContain(Local);
        BucketsOf(d, new CellRequest(Full, null, 0), D1Formatted).Should().NotContain(Whole);
    }

    /// <summary>
    /// <c>local</c>: a D1 selection of line 4 (<c>print(x)</c>, already formatted) whose applied text also
    /// re-spaces line 3, outside it. Clean: no edits, and the line-3 selection's own re-spacing.
    /// </summary>
    [Fact]
    public void PositiveControl_Local_SeesAnEditOutsideTheSelection()
    {
        var d1 = Doc(D1);
        var respaced = D1.Replace("x   =   helper()", "x = helper()");

        BucketsOf(d1, RangeLines(d1, 4, 4), respaced).Should().Contain(Local);
        BucketsOf(d1, RangeLines(d1, 4, 4), D1).Should().NotContain(Local);
        BucketsOf(d1, RangeLines(d1, 3, 3), respaced).Should().NotContain(Local);
    }

    /// <summary>
    /// The selection rule: on D1 the blank-line insertion before <c>def main():</c> is not selected by
    /// main's body (cell 1's expectation) and is by a selection spanning both its neighbours; the
    /// whole-document selection selects every hunk, so its expected text is <c>Format(D1)</c>.
    /// </summary>
    [Fact]
    public void SelectionRule_D1_MainsBodyDoesNotTakeTheBlankLinesAbove()
    {
        var d1 = Doc(D1);
        d1.Hunks.Should().HaveCount(2);
        var n = d1.Lines.Count;

        d1.Hunks.Where(h => Selects(h, 3, 4, n)).Should().ContainSingle().Which.Start.Should().Be(3);
        d1.Hunks.Where(h => Selects(h, 1, 2, n)).Should().ContainSingle().Which.Start.Should().Be(2);
        d1.Hunks.Where(h => Selects(h, 5, 5, n)).Should().BeEmpty("an insertion at line 0 or n is not selected by a selection that excludes its neighbour");
        var (s, e) = SelectedLines(R(0, 0, n - 1, 0), d1.Lines);
        LineDiff.Apply(D1, d1.Hunks.Where(h => Selects(h, s, e, n)).ToList()).Should().Be(D1Formatted);
        SelectedLines(R(3, 0, 5, 0), d1.Lines).Should().Be((3, 5), "a whole-line selection ending at the last line before the final break takes the empty final line too");
        SelectedLines(R(3, 0, 4, 0), d1.Lines).Should().Be((3, 3));
    }

    /// <summary>
    /// The insertion arm at the document's edges, where the plan's formula and its prose part: a missing
    /// final line break is an insertion at <c>n</c>, selected with the last line and not by a selection
    /// that ends above it; an insertion at line 0 is not selected by a selection below it.
    /// </summary>
    [Fact]
    public void SelectionRule_EdgeInsertions_NeedTheirExistingNeighbourSelected()
    {
        var d = Doc("x   =  1\ny = 2");
        d.Hunks.Select(h => (h.Start, h.End)).Should().Equal((0, 1), (2, 2));
        var n = d.Lines.Count;

        d.Hunks.Where(h => Selects(h, 0, 0, n)).Select(h => h.Start).Should().Equal(0);
        d.Hunks.Where(h => Selects(h, 1, 1, n)).Select(h => h.Start).Should().Equal(2);
        Selects(new LineHunk(0, 0, new[] { "" }), 3, 3, 10).Should().BeFalse();
        Selects(new LineHunk(0, 0, new[] { "" }), 0, 3, 10).Should().BeTrue();
    }

    /// <summary><c>fixedPoint</c>: cell 17 (on-type line 2 of D13, a Format fixed point — <c>2]</c> snapped from column 10 to 4). Clean: D13.</summary>
    [Fact]
    public void PositiveControl_FixedPoint_SeesCell17sSnappedContinuation()
    {
        var d13 = Doc(D13);
        d13.State.Should().Be(S1);
        var onType = new CellRequest(OnType, null, 2);
        var cell17 = "def main():\n    xs = [1,  # one\n    2]\n    print(xs)\n";

        BucketsOf(d13, onType, cell17).Should().Contain(FixedPoint).And.NotContain(OnTypeShape);
        BucketsOf(d13, onType, D13).Should().BeEmpty();
    }

    /// <summary><c>ontypeShape</c>: an on-type result that re-indents its line AND strips a trailing space. Clean: the re-indent alone.</summary>
    [Fact]
    public void PositiveControl_OnTypeShape_SeesAStrippedTrailingSpace()
    {
        var d = Doc("def main():\n        x = 1 \n");
        var onType = new CellRequest(OnType, null, 1);

        BucketsOf(d, onType, "def main():\n    x = 1\n").Should().Contain(OnTypeShape);
        BucketsOf(d, onType, "def main():\n    x = 1 \n").Should().NotContain(OnTypeShape);
        BucketsOf(d, onType, "def main():\n        x = 1 \n").Should().NotContain(OnTypeShape);
    }

    /// <summary><c>edits</c>: an edit list the strict applier refuses is the bucket, and nothing else is judged. Clean: a legal list.</summary>
    [Fact]
    public void PositiveControl_Edits_SeesAnIllegalEditList()
    {
        var d = Doc(FormatterTwins.Crlf("def main():\n        x = 1\n"));
        var request = new CellRequest(RangeLine, R(1, 0, 1, 13), 1);
        // The range handler's shape on a CRLF document: the line's length counted with its '\r'.
        var illegal = new[] { new TextEdit { Range = R(1, 0, 1, 14), NewText = "    x = 1" } };
        var legal = new[] { new TextEdit { Range = R(1, 0, 1, 13), NewText = "    x = 1" } };

        Judge(d, request, () => LspFormattingDriver.ApplyStrict(d.Text, illegal)).Failures.Select(f => f.Bucket).Should().Equal(Edits);
        Judge(d, request, () => LspFormattingDriver.ApplyStrict(d.Text, legal)).Failures.Should().BeEmpty();
    }

    /// <summary>
    /// <c>refusedWork</c> is census: a range cell whose selection has hunks and whose applied text is D
    /// counts, and fails no bucket. Clean: the selected hunks applied.
    /// </summary>
    [Fact]
    public void PositiveControl_RefusedWork_IsCountedNotFailed()
    {
        var d1 = Doc(D1);
        var line3 = RangeLines(d1, 3, 3);

        var refused = ParseableOracles(d1, line3, D1);
        refused.RefusedWork.Should().BeTrue();
        refused.Failures.Should().BeEmpty();
        ParseableOracles(d1, line3, D1.Replace("x   =   helper()", "x = helper()")).RefusedWork.Should().BeFalse();
    }

    // ================================================================
    // Ratchet and sampling mechanics
    // ================================================================

    /// <summary>
    /// The drain-on-fix rule under sampling: a row for a sampled route of a stem OUTSIDE the sample is not
    /// judged; the same row for a stem INSIDE the sample whose cells pass is stale; a missing row for a
    /// failing group is reported; a row for a group with no cell is stale.
    /// </summary>
    [Fact]
    public void Ratchet_SampledOutStemsAreNotJudged_SampledInPassingRowsAreStale()
    {
        var rows = ParseAllowlist(new[] { "a/b identity ontype S2 ontypeShape # #2168 reason" });
        var passing = new SortedDictionary<(string, string), Group> { [(OnType, S2)] = new Group { Cells = 3 } };
        var noOnType = new SortedDictionary<(string, string), Group>();

        Ratchet("a/b", Identity, noOnType, sampledIn: false, rows).Should().BeEmpty();
        Ratchet("a/b", Identity, passing, sampledIn: true, rows).Should().ContainSingle().Which.Should().Contain("holds now");
        Ratchet("a/b", Identity, noOnType, sampledIn: true, rows).Should().ContainSingle().Which.Should().Contain("no cell");

        var failing = new Group { Cells = 3 };
        failing.Failures[OnTypeShape] = (1, "detail");
        var failingGroups = new SortedDictionary<(string, string), Group> { [(OnType, S2)] = failing };
        Ratchet("a/b", Identity, failingGroups, sampledIn: true, rows).Should().BeEmpty();
        Ratchet("a/b", Identity, failingGroups, sampledIn: true, Array.Empty<Row>()).Should().ContainSingle().Which.Should().Contain("ontypeShape");
    }

    /// <summary>The loader refuses a <c>cli</c>/<c>full</c> row on a parseable state, an unknown field, a missing cite and a duplicate.</summary>
    [Fact]
    public void Allowlist_RefusesCliAndFullRowsOnParseableStates_AndMalformedRows()
    {
        foreach (var bad in new[]
        {
            "a/b identity full S2 whole # #2168 r",
            "a/b identity cli S1 net # #2168 r",
            "a/b nope ontype S2 net # #2168 r",
            "a/b identity ontype S9 net # #2168 r",
            "a/b identity ontype S2 net # no cite",
        })
        {
            FluentActions.Invoking(() => ParseAllowlist(new[] { bad })).Should().Throw<InvalidOperationException>(bad);
        }

        FluentActions.Invoking(() => ParseAllowlist(new[] { "a/b identity ontype S2 net # #2168 r", "a/b identity ontype S2 net # #2168 r" }))
            .Should().Throw<InvalidOperationException>();
        ParseAllowlist(new[] { "a/b identity range-line S2 local # #2168 r" }).Should().ContainSingle();
    }

    // ================================================================
    // Census
    // ================================================================

    /// <summary>
    /// Ordered after the theories (<see cref="RouteParitySweepOrderer"/>): prints the cells that ran per
    /// route × state × twin, <c>refusedWork</c> per range kind and twin, the F-declined stems, the wall time
    /// and the stride. Floor: every route of every state has ≥ 1 cell. When the theories did not run in this
    /// process (a filter selected the census alone), the cells are ENUMERATED instead — documents and
    /// requests, no handler call. Every allowlist row names a corpus stem.
    /// </summary>
    [Fact]
    public void Census_EveryRouteAndStateHasCells_CountsAreStated()
    {
        var corpus = Corpus.Value;
        var stride = Stride;
        _output.WriteLine($"FMTROUTE-CENSUS corpus={corpus.Corpus.Count} single-file={corpus.SingleFile} excluded-unparseable={corpus.Unparseable.Count} "
            + $"stride={stride} mode={(FullMode ? "full" : "sampled")} sampled-in-stems={corpus.Corpus.Keys.Count(k => IsSampledIn(k, stride))}");

        var measured = !s_cellsRun.IsEmpty;
        var cells = measured ? new SCG.Dictionary<(string, string, string), int>(s_cellsRun) : Enumerate(stride);
        _output.WriteLine($"FMTROUTE-CENSUS source={(measured ? "run" : "enumerated (the theories did not run in this process)")} "
            + $"sweep-time={TimeSpan.FromTicks(Interlocked.Read(ref s_sweepTicks)).TotalSeconds:F1}s declined={s_declined.Count}");
        foreach (var (stem, twin) in s_declined.Keys.OrderBy(k => k.Stem, StringComparer.Ordinal).ThenBy(k => k.Twin, StringComparer.Ordinal))
            _output.WriteLine($"FMTROUTE-CENSUS declined {stem} {twin}");

        foreach (var state in States)
        {
            foreach (var route in RoutesByState[state])
            {
                _output.WriteLine($"FMTROUTE-CENSUS cells {route} {state} "
                    + string.Join(" ", Twins.Select(t => $"{t}={cells.GetValueOrDefault((route, state, t))}")));
            }
        }

        foreach (var route in Routes.Where(RangeRoutes.Contains))
        {
            _output.WriteLine($"FMTROUTE-CENSUS refusedWork {route} "
                + string.Join(" ", Twins.Select(t => $"{t}={s_refusedWork.GetValueOrDefault((route, t))}")));
        }

        var rows = Allowlist.Value;
        foreach (var bucket in Buckets)
            _output.WriteLine($"FMTROUTE-CENSUS allowlist {bucket}={rows.Count(r => r.Bucket == bucket)}");

        foreach (var state in States)
        {
            foreach (var route in RoutesByState[state])
            {
                Twins.Sum(t => cells.GetValueOrDefault((route, state, t))).Should()
                    .BeGreaterThanOrEqualTo(1, $"route {route} must have a cell in state {state}");
            }
        }

        rows.Should().OnlyContain(r => corpus.Corpus.ContainsKey(r.Stem), "every allowlist row names a corpus fixture");
    }

    private static SCG.Dictionary<(string, string, string), int> Enumerate(int stride)
    {
        var counts = new ConcurrentDictionary<(string, string, string), int>();
        Parallel.ForEach(Corpus.Value.Corpus.Keys, stem =>
        {
            var sampledIn = IsSampledIn(stem, stride);
            foreach (var twin in Twins)
            {
                foreach (var doc in Documents(stem, twin, out _))
                {
                    foreach (var request in Cells(doc))
                    {
                        if (SampledRoutes.Contains(request.Route) && !sampledIn)
                            continue;
                        counts.AddOrUpdate((request.Route, doc.State, twin), 1, (_, n) => n + 1);
                    }
                }
            }
        });
        return new SCG.Dictionary<(string, string, string), int>(counts);
    }
}

/// <summary>
/// Orders <see cref="FormattingRouteParitySweepTests"/> (one collection, so its cases run in this order):
/// the twins' theories in <see cref="FormattingRouteParitySweepTests.Twins"/> order — so a later twin's
/// de-duplication reads the earlier twins' document keys instead of recomputing them — then the controls,
/// then the census, which reads what the theories ran.
/// </summary>
public sealed class RouteParitySweepOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
        => testCases.OrderBy(t => Rank(t.TestMethod.Method.Name)).ThenBy(t => t.DisplayName, StringComparer.Ordinal);

    private static int Rank(string method) => method switch
    {
        _ when method.StartsWith("Identity_", StringComparison.Ordinal) => 0,
        _ when method.StartsWith("CommentInjected_", StringComparison.Ordinal) => 1,
        _ when method.StartsWith("WideIndented_", StringComparison.Ordinal) => 2,
        _ when method.StartsWith("Crlf_", StringComparison.Ordinal) => 3,
        _ when method.StartsWith("Census_", StringComparison.Ordinal) => 5,
        _ => 4,
    };
}
