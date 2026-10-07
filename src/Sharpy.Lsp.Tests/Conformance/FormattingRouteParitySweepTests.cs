using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Execution;
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
/// which a partial re-indent changes structure), <c>crlf</c> and <c>cr</c> (every line break a lone <c>\r</c> — a
/// line break to the lexer, <c>LineDiff</c> and the LSP spec, missed by a <c>\n</c>-only split). The backtick and line-trailing twins
/// are NOT swept: their subject is the unparser, which the CLI-route sweep owns; this sweep's subject is
/// the mapping from <c>Format(P)</c> to edits.</para>
///
/// <para><b>Documents and states.</b> A twin yields its documents (<see cref="Documents"/>): <c>Q</c>, and
/// <c>F = Format(Q)</c> when <c>Format</c> accepts <c>Q</c> and changes it (a declined <c>Format</c> is
/// counted, and the F-dependent cells skipped). The state is OBSERVED, never assumed: a document that
/// parses and is <c>Format</c>'s fixed point is <c>S1</c>, one that parses otherwise is <c>S2</c> — so an
/// already formatted fixture's <c>Q</c> is S1. Documents are de-duplicated by CONTENT across twins in
/// twin order (<c>Format(Wide(P))</c> is <c>Format(P)</c> on all but two stems, and the crlf and cr twins'
/// <c>F</c> is written with <c>\n</c>): a later twin skips a document an earlier twin already swept —
/// equal text, equal state, equal cells.</para>
///
/// <para><b>Cells</b> (<see cref="Cells"/>; requests are 0-based LSP positions):
/// S2 — <c>cli</c>, <c>full</c>, <c>range-whole</c>, <c>range-statement</c> (each <c>Module.Body</c>
/// entry as whole lines <c>(a,0)–(b+1,0)</c>), <c>range-block</c> (each lexer <c>Indent…Dedent</c> line
/// span, <c>(a,0)–(b,len)</c>), <c>range-line</c> and <c>ontype</c> (each non-blank line);
/// S1 — <c>full</c>, <c>range-whole</c>, <c>range-statement</c>, <c>range-block</c>, <c>ontype</c>.
/// The documents that do not parse (<see cref="UnparseableDocuments"/>, from each twin Q): S3 — Q cut after
/// a block opener; S4 — Q cut after the first interior line of a multi-line literal (the literal is open);
/// S5 — an unmatched triple quote inserted as line 0 (every later literal of that quote character flips);
/// S6 — a literal lost WITHOUT a lexer abort inside its read (P22f, #2271): the error budget spent above Q,
/// a stray triple re-paired into a short string (or a comment) below, a delimiter line dropped whole at an
/// indentation error, a mid-line unexpected character or a short string aborting mid-line before each
/// delimiter (<see cref="FormatterTwins.LiteralLossShapes"/>).
/// Each gets <c>full</c>, <c>range-whole</c>, and <c>range-line</c>/<c>ontype</c> on its last line (S3,
/// S4) or on every line that starts inside a literal in Q (S5, S6); none of them is sampled.</para>
///
/// <para><b>Oracles (buckets)</b>, <c>D</c> the document and <c>T</c> the applied text:
/// <list type="bullet">
/// <item><c>edits</c> — the strict applier threw (the edits are not a legal LSP edit list for D);</item>
/// <item><c>net</c> — <c>FormatterService.CheckMeaningPreserved(D, ast(D), T)</c> refuses T, called by
/// THIS test on the applied text whatever the handler did;</item>
/// <item><c>whole</c> — <c>cli</c>: <c>T != Format(D)</c>; <c>full</c>: the same modulo line breaks (the
/// handler documents writing <c>\n</c>, so on a CRLF document it is judged on content); <c>range-whole</c>:
/// <c>T != Format(D)</c>, and on a document whose line break is not <c>\n</c> (crlf, cr) equal modulo line breaks
/// with no other line break in T;</item>
/// <item><c>local</c> — range kinds: <c>T ∉ {D, Apply(D, selected)}</c>, <c>selected</c> the hunks of
/// <c>LineDiff.Hunks(D, Format(D))</c> the selection rule (<see cref="Selects"/>, plan decision 2,
/// RESTATED here) keeps;</item>
/// <item><c>fixedPoint</c> — S1: <c>T != D</c> on any route;</item>
/// <item><c>ontypeShape</c> — <c>ontype</c>: T differs from D other than in the requested line's leading
/// whitespace;</item>
/// <item>an unparseable D (<see cref="UnparseableOracles"/>, ground truth from the parent Q's clean lex):
/// <c>content</c> — a line lost, gained, or changed beyond its leading whitespace, or its line break changed (an
/// indent-only pass keeps each line's own break, byte-exact); <c>literal</c> — a line
/// that starts inside a literal in Q is not identical; <c>depth</c> — a logical line's block depth in T,
/// by the lexer's width rule, is not its depth in Q.</item>
/// </list>
/// <c>refusedWork</c> — a range cell with a non-empty <c>selected</c> whose T is D — is census, not a
/// failure of its cell; its RATE per range kind (over the cells whose <c>selected</c> changes D) is pinned
/// on the <c>identity</c> and <c>comment</c> twins as a ceiling (<see cref="RefusalCeilingPercent"/>), and
/// the census prints beside it how far applied selections reach outside their lines.</para>
///
/// <para><b>Ratchet.</b> <c>Conformance/formatting-route-parity-allowlist.txt</c> lists
/// <c>stem twin route state bucket # #issue</c> (the bucket meanings are in its header); a row covers every cell of that route and state
/// on that stem and twin. A failing (route, state, bucket) must be listed; a listed one whose
/// (route, state) RAN in this mode and holds is stale (drain on fix), and so is one whose cell no longer
/// exists. <c>cli</c> and <c>full</c> rows on S1/S2 are refused by the loader: those routes apply
/// <c>Format</c>'s checked output whole, and a failure there is outside P22e's cure.</para>
///
/// <para><b>Budget.</b> <c>range-line</c> and <c>ontype</c> on S1/S2 — the cells proportional to line count — run
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
    internal const string Cr = "cr";
    internal static readonly string[] Twins = { Identity, Comment, Wide, Crlf, Cr };

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

    // ---- states ----
    internal const string S1 = "S1";
    internal const string S2 = "S2";
    internal const string S3 = "S3";
    internal const string S4 = "S4";
    internal const string S5 = "S5";
    internal const string S6 = "S6";
    internal const string S6n = "S6n";
    internal const string S6k = "S6k";
    internal static readonly string[] States = { S1, S2, S3, S4, S5, S6, S6n, S6k };

    /// <summary>The routes swept per state.</summary>
    internal static readonly SCG.IReadOnlyDictionary<string, string[]> RoutesByState = new SCG.Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [S1] = new[] { Full, RangeWhole, RangeStatement, RangeBlock, OnType },
        [S2] = new[] { Cli, Full, RangeWhole, RangeStatement, RangeBlock, RangeLine, OnType },
        [S3] = new[] { Full, RangeWhole, RangeLine, OnType },
        [S4] = new[] { Full, RangeWhole, RangeLine, OnType },
        [S5] = new[] { Full, RangeWhole, RangeLine, OnType },
        [S6] = new[] { Full, RangeWhole, RangeLine, OnType },
        [S6n] = new[] { Full, RangeWhole, RangeLine, OnType },
        [S6k] = new[] { Full, RangeWhole, RangeLine, OnType },
    };

    /// <summary>Whether a cell of <paramref name="route"/> in <paramref name="state"/> is sampled: the line-proportional routes on a parseable document. S3–S6 cells always run.</summary>
    internal static bool IsSampledCell(string route, string state) => SampledRoutes.Contains(route) && state is S1 or S2;

    /// <summary>
    /// The state label of an S6-family document — chosen HERE only, so each label is one class with one
    /// allowlist cite (<see cref="StateIssue"/>):
    /// <list type="bullet">
    /// <item><see cref="S6"/> — a literal lost without a lexer abort (#2271): the budget shape when Q holds a
    /// literal spanning lines, the short-string re-pair and every dropped-delimiter shape. Every S6 row
    /// drains when the lexer's fact covers the mechanism.</item>
    /// <item><see cref="S6n"/> — the budget shape when Q holds NO literal spanning lines: nothing can be lost,
    /// so its fact buckets are the direction control of the cure (a <c>factSpurious</c> row here is a finding,
    /// never a row) and its route rows are the indent map's reading of an unread remainder (#2273).</item>
    /// <item><see cref="S6k"/> — the comment re-pair, a known limit of any lexer-level fact (#2274): route
    /// buckets only.</item>
    /// </list>
    /// </summary>
    internal static string LossState(LiteralLossShape shape, LexFacts facts) => shape.Shape switch
    {
        FormatterTwins.RepairCommentShape => S6k,
        FormatterTwins.BudgetShape when facts.MultiLineLiterals.Count == 0 => S6n,
        _ => S6,
    };

    /// <summary>The issue an allowlist row of an S6-family state must cite first (<see cref="LossState"/>).</summary>
    internal static readonly SCG.IReadOnlyDictionary<string, string> StateIssue = new SCG.Dictionary<string, string>(StringComparer.Ordinal)
    {
        [S6] = "#2271",
        [S6n] = "#2273",
        [S6k] = "#2274",
    };

    /// <summary>
    /// The lexer's read paths for a literal that can span lines — the S4/S5 census floors: plain and
    /// <c>d"""</c> (<c>ReadTripleQuotedString</c>), <c>r"""</c>, <c>dr"""</c>, <c>b"""</c>, <c>f"""</c>,
    /// <c>t"""</c>, <c>df"""</c>. A kind is spelled by <see cref="LiteralKind"/>.
    /// </summary>
    internal static readonly string[] TripleQuotedKinds =
        { "\"\"\"", "d\"\"\"", "r\"\"\"", "dr\"\"\"", "b\"\"\"", "f\"\"\"", "t\"\"\"", "df\"\"\"" };

    // ---- buckets ----
    internal const string Edits = "edits";
    internal const string Net = "net";
    internal const string Whole = "whole";
    internal const string Local = "local";
    internal const string FixedPoint = "fixedPoint";
    internal const string OnTypeShape = "ontypeShape";
    internal const string Content = "content";
    internal const string Literal = "literal";
    internal const string Depth = "depth";
    internal const string FactMissing = "factMissing";
    internal const string FactSpurious = "factSpurious";
    internal static readonly string[] Buckets = { Edits, Net, Whole, Local, FixedPoint, OnTypeShape, Content, Literal, Depth, FactMissing, FactSpurious };

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
    /// One document a route is driven over: its text, observed state, and what the oracles read — for a
    /// parseable document its AST, <c>Format(D)</c>, the hunks between them and its own lex
    /// (<see cref="Facts"/>); for an unparseable one (S3–S5) the <see cref="Truth"/> taken from the
    /// parent twin <c>Q</c>'s clean lex.
    /// </summary>
    internal sealed class SweepDocument
    {
        private readonly Lazy<IReadOnlyList<LineHunk>> _hunks;
        private readonly Lazy<bool> _netAcceptsSelf;
        private readonly Lazy<bool> _netAcceptsFormatted;
        private readonly Lazy<bool> _unmatchedEqualLine;
        private Lazy<(bool FactSet, SCG.IReadOnlySet<int> LostLiteralLines)> _literalState;
        private readonly Lazy<LiteralLoss> _lexerLiteralLoss;

        /// <summary>An unparseable document: no AST, no <c>Format(D)</c>; its oracles read <paramref name="truth"/>.</summary>
        public SweepDocument(string text, string state, GroundTruth truth)
            : this(text, state, null, text, false, Array.Empty<(int, int)>(), Array.Empty<(int, int)>())
            => Truth = truth;

        public SweepDocument(string text, string state, SModule? ast, string formatted, bool formatDeclined,
            IReadOnlyList<(int First, int Last)> statementLines, IReadOnlyList<(int First, int Last)> blockLines,
            LexFacts? facts = null)
        {
            Facts = facts;
            Text = text;
            State = state;
            Ast = ast;
            Formatted = formatted;
            FormatDeclined = formatDeclined;
            StatementLines = statementLines;
            BlockLines = blockLines;
            Lines = LineDiff.Split(text).Lines;
            LineBreak = LineDiff.DocumentLineBreak(text);
            _hunks = new(() => LineDiff.Hunks(Text, Formatted));
            _netAcceptsSelf = new(() => Ast == null || FormatterService.CheckMeaningPreserved(Text, Ast, Text) == null);
            _netAcceptsFormatted = new(() => Ast == null || FormatterService.CheckMeaningPreserved(Text, Ast, Formatted) == null);
            _unmatchedEqualLine = new(ComputeUnmatchedEqualLine);
            _literalState = new(ComputeLiteralState);
            _lexerLiteralLoss = new(() =>
            {
                var lexer = new Sharpy.Compiler.Lexer.Lexer(Text);
                lexer.TokenizeAll();
                return lexer.LiteralLoss;
            });
        }

        public string Text { get; }
        public string State { get; }
        public SModule? Ast { get; }

        /// <summary>A parseable document's own clean lex (the parent facts of its cut documents).</summary>
        public LexFacts? Facts { get; }

        /// <summary>An unparseable document's ground truth, from its parent <c>Q</c>; null when D parses.</summary>
        public GroundTruth? Truth { get; }

        /// <summary><c>Format(D).FormattedText</c> — D itself when <c>Format</c> declines.</summary>
        public string Formatted { get; }
        public bool FormatDeclined { get; }

        /// <summary>0-based first and last line of each <c>Module.Body</c> entry.</summary>
        public IReadOnlyList<(int First, int Last)> StatementLines { get; }

        /// <summary>0-based first and last line of each lexer <c>Indent…Dedent</c> block.</summary>
        public IReadOnlyList<(int First, int Last)> BlockLines { get; }

        /// <summary>Line contents (<see cref="LineDiff.Split"/>), terminators excluded.</summary>
        public IReadOnlyList<string> Lines { get; }

        /// <summary>The document's first line break (<see cref="LineDiff.DocumentLineBreak(string)"/>; <c>\n</c> when it has none).</summary>
        public string LineBreak { get; }

        public IReadOnlyList<LineHunk> Hunks => _hunks.Value;

        /// <summary>
        /// An unparseable document's literal-loss fact as every indent-only route reads it — the lexer's
        /// <c>LiteralStateUnknown</c> through <see cref="IndentationService.BuildIndentMap"/> — and the 0-based
        /// lines that start inside a literal in Q (<see cref="GroundTruth.LiteralLines"/>) but not in D's own lex
        /// (the same call's literal lines): the literal lines D lost. Both empty/false for a parseable document.
        /// </summary>
        public (bool FactSet, SCG.IReadOnlySet<int> LostLiteralLines) LiteralState => _literalState.Value;

        private (bool, SCG.IReadOnlySet<int>) ComputeLiteralState()
        {
            if (Truth == null)
                return (false, new SCG.HashSet<int>());
            var map = IndentationService.BuildIndentMap(Text);
            return (map.LiteralStateUnknown, Truth.LiteralLines.Where(l => !map.LiteralLines.Contains(l + 1)).ToHashSet());
        }

        /// <summary>
        /// Which mechanisms set the fact (<see cref="Sharpy.Compiler.Lexer.Lexer.LiteralLoss"/>), read from a lexer
        /// constructed as <see cref="IndentationService.BuildIndentMap"/> constructs its own (source only, no
        /// trivia) — the map exposes the boolean alone, and the routes read nothing else.
        /// </summary>
        public LiteralLoss LexerLiteralLoss => _lexerLiteralLoss.Value;

        /// <summary>The test seam of the fact buckets' positive controls: this unparseable document with its <see cref="LiteralState"/> forced.</summary>
        public SweepDocument WithLexFacts(bool factSet, IEnumerable<int> lostLiteralLines)
        {
            var forced = (factSet, (SCG.IReadOnlySet<int>)lostLiteralLines.ToHashSet());
            return new SweepDocument(Text, State, Truth ?? throw new InvalidOperationException("instrument: the fact seam is for an unparseable document"))
            {
                _literalState = new(() => forced),
            };
        }

        /// <summary>
        /// Whether some non-blank line is both deleted (a source line inside a hunk) and inserted (a hunk's
        /// new line) VERBATIM: the diff left an equal pair unmatched — the D17 shape ("delete line 1,
        /// re-insert <c>x: int = 1</c> below"). An upper bound on LCS ties resolved against a non-blank line:
        /// it also counts a pair no LCS could match (one that crosses a matched line).
        /// </summary>
        public bool HasUnmatchedEqualLine => _unmatchedEqualLine.Value;

        private bool ComputeUnmatchedEqualLine()
        {
            var deleted = new SCG.HashSet<string>(StringComparer.Ordinal);
            foreach (var h in Hunks)
            {
                for (var i = h.Start; i < h.End; i++)
                {
                    if (!string.IsNullOrWhiteSpace(Lines[i]))
                        deleted.Add(Lines[i]);
                }
            }

            return Hunks.Any(h => h.NewLines.Any(deleted.Contains));
        }

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
        Cr => FormatterTwins.Cr(source),
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
            StatementLines(module), BlockLines(tokens), Analyze(text, tokens));
    }

    /// <summary>
    /// What the unparseable states read from a document's CLEAN lex — the ground truth of every document
    /// cut from or inserted into it. Lines are 0-based.
    /// </summary>
    internal sealed record LexFacts(
        int[] LineStarts,
        SCG.IReadOnlySet<int> InsideLines,
        IReadOnlyList<(int Line, int Depth)> LogicalStarts,
        IReadOnlyList<int> OpenerLines,
        IReadOnlyList<(int Start, int End)> MultiLineLiterals,
        SCG.IReadOnlyDictionary<int, string> Continuations)
    {
        /// <summary>The 0-based line holding source offset <paramref name="offset"/>.</summary>
        public int LineOf(int offset)
        {
            var i = Array.BinarySearch(LineStarts, offset);
            return i >= 0 ? i : ~i - 1;
        }
    }

    /// <summary>
    /// An unparseable document's ground truth, from its parent <c>Q</c>'s clean lex mapped to D's lines:
    /// the lines that start inside a literal, each logical line's first line with its block depth
    /// (<c>Indent</c> minus <c>Dedent</c> tokens before it), the lines <c>range-line</c>/<c>ontype</c> are
    /// requested on, the literal kind whose state the document loses (S4–S6), and the S6 shape that built it
    /// (<see cref="FormatterTwins.LiteralLossShapeNames"/>).
    /// </summary>
    internal sealed record GroundTruth(
        SCG.IReadOnlySet<int> LiteralLines,
        IReadOnlyList<(int Line, int Depth)> LogicalStarts,
        IReadOnlyList<int> TargetLines,
        string? Kind,
        string? Shape = null);

    internal const string BracketContinuation = "bracket";
    internal const string BackslashContinuation = "backslash";

    /// <summary>
    /// Reads <see cref="LexFacts"/> off a clean token stream: a logical line starts at the first token
    /// after a <c>Newline</c> (or the first token); a block opener is a <c>Colon</c> directly followed by
    /// <c>Newline</c> (the lexer emits no <c>Newline</c> inside brackets, so the colon is at bracket depth
    /// 0); a continuation line is a later line of a logical line that does not start inside a literal —
    /// a bracket continuation when a bracket is open before its first token, a backslash one otherwise.
    /// </summary>
    internal static LexFacts Analyze(string text, IReadOnlyList<Token> tokens)
    {
        var (lines, breaks) = LineDiff.Split(text);
        var starts = new int[lines.Count];
        for (var i = 1; i < lines.Count; i++)
            starts[i] = starts[i - 1] + lines[i - 1].Length + breaks[i - 1].Length;

        var spans = LiteralSpans.Of(tokens);
        var inside = LiteralSpans.LinesStartingInside(text, spans).Select(l => l - 1).ToHashSet();
        var logical = new SCG.List<(int, int)>();
        var openers = new SCG.List<int>();
        var continuations = new SCG.Dictionary<int, string>();
        int depth = 0, brackets = 0, maxLine = -1;
        var atStart = true;
        for (var i = 0; i < tokens.Count && tokens[i].Type != TokenType.Eof; i++)
        {
            var token = tokens[i];
            switch (token.Type)
            {
                case TokenType.Indent:
                    depth++;
                    continue;
                case TokenType.Dedent:
                    depth--;
                    continue;
                case TokenType.Newline:
                    if (i > 0 && tokens[i - 1].Type == TokenType.Colon)
                        openers.Add(tokens[i - 1].Line - 1);
                    atStart = true;
                    continue;
            }

            var line = token.Line - 1;
            if (atStart)
                logical.Add((line, depth));
            else if (line > maxLine && !inside.Contains(line))
                continuations[line] = brackets > 0 ? BracketContinuation : BackslashContinuation;
            atStart = false;
            maxLine = Math.Max(maxLine, line);
            if (token.Type is TokenType.LeftParen or TokenType.LeftBracket or TokenType.LeftBrace)
                brackets++;
            else if (token.Type is TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace)
                brackets--;
        }

        var facts = new LexFacts(starts, inside, logical, openers, Array.Empty<(int, int)>(), continuations);
        return facts with { MultiLineLiterals = spans.Where(s => facts.LineOf(s.Start) < facts.LineOf(s.End - 1)).ToArray() };
    }

    /// <summary>A literal's kind (<see cref="FormatterTwins.LiteralKind"/>, shared with the S6 recipe).</summary>
    internal static string LiteralKind(string text, int start) => FormatterTwins.LiteralKind(text, start);

    private static char QuoteCharacter(string text, int start) => FormatterTwins.QuoteCharacter(text, start);

    /// <summary>
    /// The unparseable documents of a parseable twin <paramref name="q"/> (plan Task 3):
    /// <list type="bullet">
    /// <item>S3 — Q cut after each block-opener line (the lines up to it and its line break);</item>
    /// <item>S4 — Q cut after the first interior line of each literal spanning ≥ 2 lines, when the literal
    /// is still open after the cut (a two-line literal's second line holds its closer: no interior line);</item>
    /// <item>S5 — when Q has a literal spanning ≥ 2 lines, Q with a line holding only the triple quote of its
    /// FIRST such literal's quote character inserted as line 0;</item>
    /// <item>S6 — the literal-loss shapes WITHOUT a lexer abort (P22f, #2271), built by the one recipe the
    /// lexer's tests share (<see cref="FormatterTwins.LiteralLossShapes"/>: the error budget spent above Q, a
    /// re-paired triple closed by a short string or a comment, a delimiter line dropped at SPY0011–SPY0015 or SPY0004;
    /// SPY0014 only on the <c>wide</c> twin, <paramref name="includeMismatch"/>) and wrapped with Q's
    /// ground truth by <see cref="Shaped"/>.</item>
    /// </list>
    /// The state is observed: a document that parses is <see cref="ObserveParseable"/>'s S1/S2 document.
    /// </summary>
    internal static IEnumerable<SweepDocument> UnparseableDocuments(string q, LexFacts facts, bool includeMismatch = false)
    {
        foreach (var line in facts.OpenerLines.Distinct())
        {
            if (line + 1 < facts.LineStarts.Length)
                yield return Cut(q, facts, line, S3, null);
        }

        foreach (var (start, end) in facts.MultiLineLiterals)
        {
            var interior = facts.LineOf(start) + 1;
            if (interior + 1 < facts.LineStarts.Length && facts.LineStarts[interior + 1] < end)
                yield return Cut(q, facts, interior, S4, LiteralKind(q, start));
        }

        if (facts.MultiLineLiterals.Count > 0)
        {
            var first = facts.MultiLineLiterals[0].Start;
            yield return Inserted(q, facts, 0, new string(QuoteCharacter(q, first), 3), S5, LiteralKind(q, first));
        }

        foreach (var shape in FormatterTwins.LiteralLossShapes(q, includeMismatch))
            yield return Shaped(q, facts, shape);
    }

    /// <summary>
    /// An S6-family document (<see cref="LossState"/>): <paramref name="shape"/>'s text, ground truth Q's lex
    /// shifted by its <see cref="LiteralLossShape.LineShift"/> (the lines it inserts above Q; a line it appends
    /// is no line of Q), the lines it re-indents removed from the logical starts (their depth may legitimately
    /// change — the repair) and the literal lines kept as they are. <c>range-line</c>/<c>ontype</c> are
    /// requested on every line that starts inside a literal in Q. An <see cref="S6n"/> document has none, and
    /// its line routes are requested on ONE line: Q's first logical line inside a block (depth ≥ 1). Its route
    /// rows are one class (#2273, the indent map reading an unread remainder), which <c>full</c> and
    /// <c>range-whole</c> already show on every stem it reaches; one line per document gives each route a cell
    /// without a row per logical line of the corpus.
    /// </summary>
    internal static SweepDocument Shaped(string q, LexFacts facts, LiteralLossShape shape)
    {
        int Shift(int l) => l + shape.LineShift;
        var reindented = shape.ReindentedLines.ToHashSet();
        var inside = facts.InsideLines.Select(Shift).Order().ToArray();
        var logical = facts.LogicalStarts.Where(s => !reindented.Contains(s.Line)).Select(s => (Shift(s.Line), s.Depth)).ToArray();
        var state = LossState(shape, facts);
        return ObserveParseable(shape.Text) ?? new SweepDocument(shape.Text, state, new GroundTruth(
            inside.ToHashSet(),
            logical,
            state == S6n ? logical.Where(s => s.Item2 >= 1).Select(s => s.Item1).Take(1).ToArray() : inside,
            shape.Kind,
            shape.Shape));
    }

    /// <summary>
    /// <paramref name="q"/> with the leading whitespace of the 0-based <paramref name="lines"/> replaced by
    /// <paramref name="whitespace"/> (<see cref="FormatterTwins.Reindent"/>) as an S6 document of
    /// <paramref name="shape"/> — the hand-built twin of a <c>dropped-</c> shape.
    /// </summary>
    internal static SweepDocument Reindented(string q, LexFacts facts, int[] lines, string whitespace, string shape)
        => Shaped(q, facts, new LiteralLossShape(shape, FormatterTwins.Reindent(q, lines, whitespace),
            facts.MultiLineLiterals.Select(m => LiteralKind(q, m.Start)).FirstOrDefault(), 0, lines));

    /// <summary><paramref name="q"/>'s lines up to <paramref name="line"/> and its line break; ground truth is Q's lex of those lines.</summary>
    internal static SweepDocument Cut(string q, LexFacts facts, int line, string state, string? kind)
    {
        var text = q[..facts.LineStarts[line + 1]];
        return ObserveParseable(text) ?? new SweepDocument(text, state, new GroundTruth(
            facts.InsideLines.Where(l => l <= line).ToHashSet(),
            facts.LogicalStarts.Where(s => s.Line <= line).ToArray(),
            new[] { line },
            kind));
    }

    /// <summary>
    /// <paramref name="q"/> with <paramref name="lineText"/> inserted as line <paramref name="at"/> (Q's own
    /// line break); ground truth is Q's lex, shifted past the insertion. <c>range-line</c>/<c>ontype</c> are
    /// requested on every line that starts inside a literal in Q.
    /// </summary>
    internal static SweepDocument Inserted(string q, LexFacts facts, int at, string lineText, string state, string? kind)
    {
        var text = q[..facts.LineStarts[at]] + lineText + LineDiff.DocumentLineBreak(q) + q[facts.LineStarts[at]..];
        int Shift(int l) => l >= at ? l + 1 : l;
        var inside = facts.InsideLines.Select(Shift).Order().ToArray();
        return ObserveParseable(text) ?? new SweepDocument(text, state, new GroundTruth(
            inside.ToHashSet(),
            facts.LogicalStarts.Select(s => (Shift(s.Line), s.Depth)).ToArray(),
            inside,
            kind));
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
    /// <c>F = Format(Q)</c>, and Q's <see cref="UnparseableDocuments"/>; each dropped when an earlier twin (in <see cref="Twins"/> order) already has a
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

        docs.AddRange(UnparseableDocuments(q, qDoc.Facts!, includeMismatch: twin == Wide));
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
                    foreach (var l in LineTargets(d))
                        yield return new CellRequest(route, R(l, 0, l, d.Lines[l].Length), l);
                    break;
                case OnType:
                    foreach (var l in LineTargets(d))
                        yield return new CellRequest(route, null, l);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(route), route, null);
            }
        }
    }

    /// <summary>The lines <c>range-line</c> and <c>ontype</c> are requested on: an unparseable document's targets, else each non-blank line.</summary>
    private static IEnumerable<int> LineTargets(SweepDocument d)
        => d.Truth?.TargetLines ?? Enumerable.Range(0, d.Lines.Count).Where(l => !string.IsNullOrWhiteSpace(d.Lines[l]));

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

    /// <summary>One cell's verdict: its failing buckets, whether it is a <c>refusedWork</c> census cell, and whether T differs from D.</summary>
    internal sealed record CellVerdict(SCG.List<(string Bucket, string Detail)> Failures, bool RefusedWork)
    {
        public bool Changed { get; init; }

        /// <summary>A range cell of a parseable document whose <c>selected</c> hunks change D — the refusal rate's denominator.</summary>
        public bool WithWork { get; init; }

        /// <summary>For a range cell with work: the lines <c>[s, e]</c> it selects.</summary>
        public int SelectedLineCount { get; init; }

        /// <summary>For a range cell with work: the source lines its <c>selected</c> hunks replace or delete that lie outside <c>[s, e]</c>.</summary>
        public int OutsideLineCount { get; init; }

        /// <summary>A <c>refusedWork</c> cell whose document <see cref="SweepDocument.HasUnmatchedEqualLine"/> (census input for a later LCS tie-break).</summary>
        public bool RefusedOnUnmatchedEqualLine { get; init; }
    }

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
            var failures = new SCG.List<(string, string)> { (Edits, $"{request}: {e.Message}") };
            if (d.Ast == null)
                failures.AddRange(FactOracles(d, request));
            return new CellVerdict(failures, false);
        }

        var verdict = d.Ast == null ? UnparseableOracles(d, request, applied) : ParseableOracles(d, request, applied);
        return verdict with { Changed = applied != d.Text };
    }

    /// <summary>
    /// The oracles of a document that does not parse (S3–S5) over the applied text <paramref name="applied"/>,
    /// ground truth from the parent Q (<see cref="SweepDocument.Truth"/>). Every route on such a document is an
    /// indent-only pass, so it changes leading whitespace and nothing else — line breaks included.
    /// <list type="bullet">
    /// <item><c>content</c> — the line count differs, or a line differs after its leading whitespace (a
    /// whitespace-only line may become empty), or a line's break differs (a CRLF or lone-<c>\r</c> document
    /// keeps its breaks);</item>
    /// <item><c>literal</c> — a line that starts inside a literal in Q is not identical;</item>
    /// <item><c>depth</c> — a line that starts a logical line in Q has another block depth in T
    /// (<see cref="FirstDepthDifference"/>) than in Q.</item>
    /// </list>
    /// <c>literal</c> and <c>depth</c> map lines by index, so they are judged only when the line count holds.
    /// The lexer's literal-loss fact is judged on every cell too (<see cref="FactOracles"/>).
    /// </summary>
    internal static CellVerdict UnparseableOracles(SweepDocument d, CellRequest request, string applied)
    {
        var failures = new SCG.List<(string, string)>();
        var truth = d.Truth ?? throw new InvalidOperationException("instrument: an unparseable document without ground truth");
        failures.AddRange(FactOracles(d, request));
        var x = d.Lines;
        var (t, tBreaks) = LineDiff.Split(applied);
        if (t.Count != x.Count)
        {
            failures.Add((Content, $"{request}: {FirstDifference(d.Text, applied, "D", "T")}"));
            return new CellVerdict(failures, false);
        }

        var content = Enumerable.Range(0, x.Count).FirstOrDefault(i => x[i].TrimStart(' ', '\t') != t[i].TrimStart(' ', '\t'), -1);
        var xBreaks = LineDiff.Split(d.Text).Breaks;
        var lineBreak = Enumerable.Range(0, xBreaks.Count).FirstOrDefault(i => xBreaks[i] != tBreaks[i], -1);
        if (content >= 0)
            failures.Add((Content, $"{request}: line {content}: D '{Clip(x[content])}' vs T '{Clip(t[content])}'"));
        else if (lineBreak >= 0)
            failures.Add((Content, $"{request}: line {lineBreak}'s line break: D {EscapeBreak(xBreaks[lineBreak])} vs T {EscapeBreak(tBreaks[lineBreak])}"));

        var literal = truth.LiteralLines.Order().FirstOrDefault(l => x[l] != t[l], -1);
        if (literal >= 0)
            failures.Add((Literal, $"{request}: line {literal} starts inside a literal: D '{Clip(x[literal])}' vs T '{Clip(t[literal])}'"));

        if (FirstDepthDifference(t, truth.LogicalStarts) is { } depth)
        {
            failures.Add((Depth, $"{request}: line {depth.Line} '{Clip(t[depth.Line])}' is at depth "
                + (depth.Actual < 0 ? "<a width on no enclosing level>" : depth.Actual.ToString(System.Globalization.CultureInfo.InvariantCulture))
                + $" in T, {depth.Expected} in Q"));
        }

        return new CellVerdict(failures, false);
    }

    /// <summary>
    /// The lexer's literal-loss fact itself (P22f, #2271), judged per DOCUMENT and reported on each of its
    /// cells so a row keeps its <c>stem twin route state bucket</c> shape: <c>factMissing</c> — the fact is
    /// false while some line that starts inside a literal in Q does not start inside one in D's lex (the
    /// routes would treat string content as code); <c>factSpurious</c> — the fact is true while no such line
    /// is lost (a route is switched off for nothing). The route buckets see only what a route did; these see
    /// the INPUT every indent-only route reads, so a fact that is wrong where every route happens to refuse
    /// for another reason (N4m) is still a row.
    /// </summary>
    internal static IEnumerable<(string Bucket, string Detail)> FactOracles(SweepDocument d, CellRequest request)
    {
        // The comment re-pair (S6k) is exempt from the FACT buckets only: its lexer signature is
        // DStringRepaired's (a closer re-paired into a comment), which loses nothing, so no lexer fact can
        // tell them apart (#2271's known limit, tracked by #2274; owner ruling at /verify-plan 2026-10-07).
        // Its route buckets are judged.
        if (d.State == S6k)
            yield break;
        var (factSet, lost) = d.LiteralState;
        if (!factSet && lost.Count > 0)
            yield return (FactMissing, $"{request}: the lexer's literal state is known, but line {lost.Min()} no longer starts inside a literal");
        else if (factSet && lost.Count == 0)
            yield return (FactSpurious, $"{request}: the lexer's literal state is unknown, but every line that starts inside a literal in Q still does");
    }

    /// <summary>
    /// The first logical line of <paramref name="lines"/> whose block depth — by a width stack over the
    /// lines' leading whitespace, in the LEXER's rule — differs from its depth in Q: a wider line pushes; a
    /// narrower line pops to an EQUAL width; a width on no enclosing level is a difference
    /// (<c>Actual = -1</c>), never a level of its own. <c>IndentationService</c>'s indent map opens a level
    /// for a dedent between widths and is deliberately not mirrored (cell 21).
    /// </summary>
    internal static (int Line, int Actual, int Expected)? FirstDepthDifference(IReadOnlyList<string> lines, IReadOnlyList<(int Line, int Depth)> logicalStarts)
    {
        var stack = new Stack<int>();
        stack.Push(0);
        foreach (var (line, expected) in logicalStarts)
        {
            var text = lines[line];
            var width = text.Length - text.TrimStart(' ', '\t').Length;
            if (width > stack.Peek())
            {
                stack.Push(width);
            }
            else
            {
                while (stack.Count > 1 && stack.Peek() > width)
                    stack.Pop();
                if (stack.Peek() != width)
                    return (line, -1, expected);
            }

            if (stack.Count - 1 != expected)
                return (line, stack.Count - 1, expected);
        }

        return null;
    }

    /// <summary>The oracles of a document that parses (S1, S2) over the applied text <paramref name="applied"/>.</summary>
    internal static CellVerdict ParseableOracles(SweepDocument d, CellRequest request, string applied)
    {
        var failures = new SCG.List<(string, string)>();
        var refusedWork = false;
        var withWork = false;
        int selectedLineCount = 0, outsideLineCount = 0;
        var route = request.Route;

        if (d.NetRefusal(applied) is { } refusal)
            failures.Add((Net, $"{request}: {refusal}"));

        var wholeFails = route switch
        {
            Cli => applied != d.Formatted,
            Full => NormalizeLineBreaks(applied) != NormalizeLineBreaks(d.Formatted),
            RangeWhole => d.LineBreak != "\n"
                ? NormalizeLineBreaks(applied) != NormalizeLineBreaks(d.Formatted) || HasOtherLineBreak(applied, d.LineBreak)
                : applied != d.Formatted,
            _ => false,
        };
        if (wholeFails)
            failures.Add((Whole, $"{request}: {FirstDifference(d.Formatted, applied, "Format(D)", "T")}"));

        if (RangeRoutes.Contains(route))
        {
            var (s, e) = SelectedLines(request.Range!, d.Lines);
            var selected = d.Hunks.Where(h => Selects(h, s, e, d.Lines.Count)).ToList();
            withWork = selected.Count > 0 && LineDiff.Apply(d.Text, selected) != d.Text;
            if (withWork)
            {
                selectedLineCount = e - s + 1;
                outsideLineCount = selected.Sum(h => OutsideLines(h, s, e));
            }

            if (applied != d.Text)
            {
                var expected = LineDiff.Apply(d.Text, selected);
                if (applied != expected)
                    failures.Add((Local, $"{request} selects lines {s}..{e}: {FirstDifference(expected, applied, "Apply(D, selected)", "T")}"));
            }
            else if (withWork)
            {
                refusedWork = true;
            }
        }

        if (d.State == S1 && applied != d.Text)
            failures.Add((FixedPoint, $"{request}: {FirstDifference(d.Text, applied, "F", "T")}"));

        if (route == OnType && WithoutLeadingWhitespace(applied, request.Line) != WithoutLeadingWhitespace(d.Text, request.Line))
            failures.Add((OnTypeShape, $"{request}: {FirstDifference(d.Text, applied, "D", "T")}"));

        return new CellVerdict(failures, refusedWork)
        {
            RefusedOnUnmatchedEqualLine = refusedWork && d.HasUnmatchedEqualLine,
            WithWork = withWork,
            SelectedLineCount = selectedLineCount,
            OutsideLineCount = outsideLineCount,
        };
    }

    /// <summary>The source lines <c>[a, b)</c> a hunk replaces or deletes that lie outside <c>[s, e]</c> (an insertion has none).</summary>
    internal static int OutsideLines(LineHunk hunk, int s, int e)
        => Math.Max(0, Math.Min(hunk.End, s) - hunk.Start) + Math.Max(0, hunk.End - Math.Max(hunk.Start, e + 1));

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

    /// <summary>Whether <paramref name="text"/> has a line break other than <paramref name="lineBreak"/>.</summary>
    private static bool HasOtherLineBreak(string text, string lineBreak)
        => LineDiff.Split(text).Breaks.Any(b => b != lineBreak);

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

    private static string EscapeBreak(string lineBreak) => lineBreak.Replace("\r", "\\r").Replace("\n", "\\n");

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

    [Theory]
    [MemberData(nameof(CorpusNames))]
    public void Cr_EveryRouteAppliesACheckedText(string stem) => Sweep(stem, Cr);

    /// <summary>The cells of one (route, state) group of a (stem, twin): counts and the first detail per failing bucket.</summary>
    internal sealed class Group
    {
        public int Cells;
        public int RefusedWork;
        public int Changed;

        /// <summary>Range cells whose <c>selected</c> hunks change D (<see cref="CellVerdict.WithWork"/>).</summary>
        public int WithWork;

        /// <summary><c>refusedWork</c> cells whose document has an unmatched equal line (<see cref="SweepDocument.HasUnmatchedEqualLine"/>).</summary>
        public int RefusedOnUnmatchedEqualLine;

        /// <summary>Over the range cells with work that APPLIED their hunks: lines selected, hunk lines outside them, and the cells with any.</summary>
        public long AppliedSelectedLines;
        public long AppliedOutsideLines;
        public int AppliedStraddling;
        public readonly SortedDictionary<string, (int Count, string First)> Failures = new(StringComparer.Ordinal);

        /// <summary>Per failing bucket, the S6 shapes of the documents whose cells failed it (the reason an allowlist row records).</summary>
        public readonly SortedDictionary<string, SortedSet<string>> FailureShapes = new(StringComparer.Ordinal);
    }

    /// <summary>Everything the census reads from the theories that ran in this process.</summary>
    private static readonly ConcurrentDictionary<(string Route, string State, string Twin), int> s_cellsRun = new();
    private static readonly ConcurrentDictionary<(string Route, string Twin, string Measure), long> s_rangeCensus = new();
    private static readonly ConcurrentDictionary<string, int> s_stemsRun = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(string Stem, string Twin), bool> s_declined = new();

    /// <summary>Census tallies (<see cref="Tallies"/>): cells per (unparseable state, literal kind) and <c>ontype</c> cells per continuation shape.</summary>
    private static readonly ConcurrentDictionary<string, int> s_tallies = new(StringComparer.Ordinal);
    private static long s_sweepTicks;

    private void Sweep(string stem, string twin)
    {
        var clock = Stopwatch.StartNew();
        var stride = Stride;
        var sampledIn = IsSampledIn(stem, stride);
        var tallies = new SCG.Dictionary<string, int>(StringComparer.Ordinal);
        var groups = RunCells(_driver, stem, twin, sampledIn, tallies, out var declined);
        Interlocked.Add(ref s_sweepTicks, clock.Elapsed.Ticks);
        foreach (var (key, n) in tallies)
            s_tallies.AddOrUpdate(key, n, (_, m) => m + n);
        if (declined)
            s_declined[(stem, twin)] = true;
        foreach (var ((route, state), group) in groups)
        {
            s_cellsRun.AddOrUpdate((route, state, twin), group.Cells, (_, n) => n + group.Cells);
            if (group.Changed > 0)
                s_tallies.AddOrUpdate(ChangedTally(route, state, twin), group.Changed, (_, n) => n + group.Changed);
            foreach (var (measure, n) in RangeMeasures(group))
            {
                if (n > 0)
                    s_rangeCensus.AddOrUpdate((route, twin, measure), n, (_, m) => m + n);
            }
        }

        s_stemsRun.AddOrUpdate(twin, 1, (_, n) => n + 1);

        var problems = Ratchet(stem, twin, groups, sampledIn, AllowlistByCell.Value[(stem, twin)]);

        var failing = groups.SelectMany(g => g.Value.Failures.Keys.Select(b => (g.Key.Route, g.Key.State, Bucket: b,
            Shapes: g.Value.FailureShapes.TryGetValue(b, out var shapes) ? " " + string.Join(' ', shapes) : ""))).ToList();
        _output.WriteLine($"FMTROUTE {stem} {twin} cells={groups.Values.Sum(g => g.Cells)} sampled={(sampledIn ? "in" : "out")} "
            + (failing.Count == 0 ? "ok" : $"rows={failing.Count}"));
        foreach (var (route, state, bucket, shapes) in failing)
            _output.WriteLine($"FMTROUTE-ROW {stem} {twin} {route} {state} {bucket}{shapes}");

        if (problems.Count > 0)
        {
            Assert.Fail($"{stem} ({twin} twin): every formatting route must apply a text checked as applied.\n  "
                + string.Join("\n  ", problems));
        }
    }

    /// <summary>
    /// Runs every cell of (stem, twin) — the sampled cells only when <paramref name="sampledIn"/> — grouped by
    /// (route, state); adds each cell's <see cref="Tallies"/> to <paramref name="tallies"/>.
    /// </summary>
    internal static SortedDictionary<(string Route, string State), Group> RunCells(
        LspFormattingDriver driver, string stem, string twin, bool sampledIn, SCG.Dictionary<string, int> tallies, out bool declined)
    {
        var docs = Documents(stem, twin, out declined);
        var groups = new SortedDictionary<(string Route, string State), Group>();
        var work = new SCG.List<(SweepDocument Doc, CellRequest Request, Group Group)>();
        foreach (var doc in docs)
        {
            foreach (var tally in DocumentTallies(doc))
                tallies[tally] = tallies.GetValueOrDefault(tally) + 1;
            foreach (var request in Cells(doc))
            {
                foreach (var tally in Tallies(doc, request))
                    tallies[tally] = tallies.GetValueOrDefault(tally) + 1;
                if (IsSampledCell(request.Route, doc.State) && !sampledIn)
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
                if (verdict.Changed)
                    item.Group.Changed++;
                if (verdict.RefusedWork)
                    item.Group.RefusedWork++;
                if (verdict.RefusedOnUnmatchedEqualLine)
                    item.Group.RefusedOnUnmatchedEqualLine++;
                if (verdict.WithWork)
                {
                    item.Group.WithWork++;
                    if (!verdict.RefusedWork)
                    {
                        item.Group.AppliedSelectedLines += verdict.SelectedLineCount;
                        item.Group.AppliedOutsideLines += verdict.OutsideLineCount;
                        if (verdict.OutsideLineCount > 0)
                            item.Group.AppliedStraddling++;
                    }
                }
                foreach (var (bucket, detail) in verdict.Failures)
                {
                    item.Group.Failures[bucket] = item.Group.Failures.TryGetValue(bucket, out var seen)
                        ? (seen.Count + 1, seen.First)
                        : (1, detail);
                    if (item.Doc.Truth?.Shape is { } lossShape)
                    {
                        if (!item.Group.FailureShapes.TryGetValue(bucket, out var shapes))
                            item.Group.FailureShapes[bucket] = shapes = new SortedSet<string>(StringComparer.Ordinal);
                        shapes.Add(lossShape);
                    }
                }
            }
        });
        return groups;
    }

    /// <summary>
    /// The census keys of one cell, counted whether or not a sampled cell runs (they count the full-mode
    /// cells): <c>kind S4 r"""</c> for a cell of an unparseable document that loses a literal of that kind,
    /// <c>shape S6 dropped-SPY0013 ontype</c> for a cell of an S6 document of that shape and route (and
    /// <c>kind S6c r"""</c> when the shape is a <c>dropped-</c> one), and <c>ontype-bracket</c>/<c>ontype-backslash</c>
    /// for an <c>ontype</c> cell on a continuation line of a parseable document.
    /// </summary>
    internal static IEnumerable<string> Tallies(SweepDocument doc, CellRequest request)
    {
        if (doc.Truth?.Kind is { } kind)
            yield return KindTally(doc.State, kind);
        if (doc.Truth?.Shape is { } lossShape)
        {
            yield return ShapeTally(doc.State, lossShape, request.Route);
            if (lossShape.StartsWith(FormatterTwins.DroppedShapePrefix, StringComparison.Ordinal) && doc.Truth.Kind is { } droppedKind)
                yield return KindTally(S6Dropped, droppedKind);
        }

        if (request.Route == OnType && doc.Facts?.Continuations.TryGetValue(request.Line, out var shape) == true)
            yield return $"ontype-{shape}";
    }

    private static string KindTally(string state, string kind) => $"kind {state} {kind}";

    /// <summary>The kind tallies' key for S6's <c>dropped-</c> shapes alone (the per-kind floor reads it).</summary>
    private const string S6Dropped = "S6c";

    private static string ShapeTally(string state, string shape, string route) => $"shape {state} {shape} {route}";

    /// <summary>
    /// Whether the lexer read <paramref name="d"/> without aborting inside a literal — what makes an S6
    /// document a literal loss the abort fact cannot see: the abort's own flag, since the fact now has four
    /// producers (#2271).
    /// </summary>
    internal static bool IsAbortFree(SweepDocument d) => (d.LexerLiteralLoss & LiteralLoss.AbortInsideLiteral) == 0;

    /// <summary>
    /// The census keys of one S6-family document (not of its cells): <c>documents S6 budget</c> and, when
    /// <see cref="IsAbortFree"/>, <c>abort-free S6 budget</c>. An <see cref="S6"/> document counts only when Q
    /// has a literal line for it to lose; an <see cref="S6n"/> one (nothing to lose) and an
    /// <see cref="S6k"/> one always.
    /// </summary>
    internal static IEnumerable<string> DocumentTallies(SweepDocument d)
    {
        if (d.Truth is not { Shape: { } shape } truth || (d.State == S6 && truth.LiteralLines.Count == 0))
            yield break;
        yield return LossDocumentTally(d.State, shape);
        if (IsAbortFree(d))
            yield return AbortFreeTally(d.State, shape);
    }

    private static string LossDocumentTally(string state, string shape) => $"documents {state} {shape}";

    private static string AbortFreeTally(string state, string shape) => $"abort-free {state} {shape}";

    /// <summary>The (state, shape) pairs <see cref="LossState"/> can produce, in census order.</summary>
    private static IEnumerable<(string State, string Shape)> LossStateShapes()
        => FormatterTwins.LiteralLossShapeNames
            .Select(shape => (State: shape == FormatterTwins.RepairCommentShape ? S6k : S6, Shape: shape))
            .Append((S6n, FormatterTwins.BudgetShape));

    // ---- the range census's measures (s_rangeCensus) ----
    private const string WithWorkMeasure = "withWork";
    private const string RefusedMeasure = "refused";
    private const string RefusedTieMeasure = "refusedOnUnmatchedEqualLine";
    private const string AppliedSelectedMeasure = "appliedSelectedLines";
    private const string AppliedOutsideMeasure = "appliedOutsideLines";
    private const string StraddlingMeasure = "appliedStraddling";

    private static IEnumerable<(string Measure, long N)> RangeMeasures(Group group)
    {
        yield return (WithWorkMeasure, group.WithWork);
        yield return (RefusedMeasure, group.RefusedWork);
        yield return (RefusedTieMeasure, group.RefusedOnUnmatchedEqualLine);
        yield return (AppliedSelectedMeasure, group.AppliedSelectedLines);
        yield return (AppliedOutsideMeasure, group.AppliedOutsideLines);
        yield return (StraddlingMeasure, group.AppliedStraddling);
    }

    private static string ChangedTally(string route, string state, string twin) => $"changed {route} {state} {twin}";

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
            else if (!(IsSampledCell(route, state) && !sampledIn))
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
            if (StateIssue.TryGetValue(fields[3], out var issue) && !Regex.IsMatch(cite, $@"^{issue}\b"))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' — a {fields[3]} row cites {issue}, the one class that state holds (LossState).");
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
        d.LineBreak.Should().Be("\r\n");
        var whole = new CellRequest(RangeWhole, R(0, 0, d.Lines.Count - 1, 0), 0);

        BucketsOf(d, whole, D1Formatted).Should().Contain(Whole);
        BucketsOf(d, whole, D1Formatted.Replace("\n", "\r\n")).Should().NotContain(Whole).And.NotContain(Local);
        BucketsOf(d, new CellRequest(Full, null, 0), D1Formatted).Should().NotContain(Whole);
    }

    /// <summary>
    /// <c>whole</c> on a lone-<c>\r</c> document, <c>range-whole</c>: <c>Format(D)</c> written with <c>\n</c> or with
    /// <c>\r\n</c> fails; the same text with lone <c>\r</c> passes. <c>full</c> is judged modulo line breaks.
    /// </summary>
    [Fact]
    public void PositiveControl_Whole_OnACrDocument_RangeWholeKeepsCr()
    {
        var d = Doc(FormatterTwins.Cr(D1));
        d.LineBreak.Should().Be("\r");
        d.Lines.Should().HaveCount(6);
        var whole = new CellRequest(RangeWhole, R(0, 0, d.Lines.Count - 1, 0), 0);

        BucketsOf(d, whole, D1Formatted).Should().Contain(Whole);
        BucketsOf(d, whole, FormatterTwins.Crlf(D1Formatted)).Should().Contain(Whole);
        BucketsOf(d, whole, FormatterTwins.Cr(D1Formatted)).Should().NotContain(Whole).And.NotContain(Local);
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

    // ---- the unparseable states: documents built by the sweep's own generators from a parseable parent ----

    /// <summary>D5u's parent: D5 inside <c>main</c>. Its S4 cut after the string's first interior line is D5u.</summary>
    private const string Q5 = "def main():\n    s = \"\"\"\n        key: value\n\"\"\"\n    print(s)\n";
    private const string D5u = "def main():\n    s = \"\"\"\n        key: value\n";

    /// <summary>D19's parent: D19 without the docstring opener <c>f</c>'s line 1 holds.</summary>
    private const string Q19 = "def f() -> int:\n    return 1\ndef g() -> str:\n    s = \"\"\"\n        key: value\n    \"\"\"\n    return s\n";
    private const string D19 = "def f() -> int:\n    \"\"\"\n    return 1\ndef g() -> str:\n    s = \"\"\"\n        key: value\n    \"\"\"\n    return s\n";

    /// <summary>D12 = D11 + <c>x = (</c>; its parent closes the bracket on the next line.</summary>
    private const string D11 = "def main():\n        if False:\n                print(1)\n                print(2)\n";
    private const string Q12 = D11 + "        x = (\n            1)\n";
    private const string D12 = D11 + "        x = (\n";

    /// <summary>D21's parent: D21 with the block's body typed. Its S3 cut after <c>if a == 1:</c> is D21.</summary>
    private const string Q21 = "def main():\n        a = 1\n        if a == 1:\n                print(a)\n";
    private const string D21 = "def main():\n        a = 1\n        if a == 1:\n";

    private static string[] UnparseableBucketsOf(SweepDocument d, CellRequest request, string applied)
        => UnparseableOracles(d, request, applied).Failures.Select(f => f.Bucket).ToArray();

    private static SweepDocument Generated(string q, string text, string state)
    {
        var d = UnparseableDocuments(q, Doc(q).Facts!).Should().ContainSingle(g => g.Text == text).Subject;
        d.State.Should().Be(state);
        d.Ast.Should().BeNull("the document must not parse");
        return d;
    }

    /// <summary>
    /// <c>literal</c>: cell 13 (on-type line 2 of D5u — the open string's line 8 → 4 spaces) and cell 19
    /// (Format Document's fallback on D19 — <c>g</c>'s string line 8 → 4 spaces). Clean: no edits.
    /// </summary>
    [Fact]
    public void PositiveControl_Literal_SeesCells13And19ReindentStringContent()
    {
        var d5u = Generated(Q5, D5u, S4);
        d5u.Truth!.Kind.Should().Be("\"\"\"");
        var onType = new CellRequest(OnType, null, 2);
        var cell13 = "def main():\n    s = \"\"\"\n    key: value\n";
        UnparseableBucketsOf(d5u, onType, cell13).Should().Equal(Literal);
        UnparseableBucketsOf(d5u, onType, D5u).Should().BeEmpty();

        var d19 = Inserted(Q19, Doc(Q19).Facts!, 1, "    \"\"\"", S5, null);
        d19.Text.Should().Be(D19);
        d19.Ast.Should().BeNull();
        var cell19 = D19.Replace("        key: value", "    key: value");
        UnparseableBucketsOf(d19, new CellRequest(Full, null, 0), cell19).Should().Equal(Literal);
        UnparseableBucketsOf(d19, new CellRequest(Full, null, 0), D19).Should().BeEmpty();
    }

    /// <summary>
    /// <c>depth</c>: cell 16 (on-type line 3 of D12 — <c>print(2)</c> moves out of <c>if False:</c>) and cell 21
    /// (on-type line 2 of D21 — <c>if a == 1:</c> moved 8 → 4 while <c>a = 1</c> stays at 8: a width between
    /// the enclosing levels, which an indent map that opens a level there would accept). Clean: no edits.
    /// </summary>
    [Fact]
    public void PositiveControl_Depth_SeesCells16And21MoveALineAcrossABlock()
    {
        var d12 = Cut(Q12, Doc(Q12).Facts!, 4, S3, null);
        d12.Text.Should().Be(D12);
        d12.Ast.Should().BeNull();
        var cell16 = D12.Replace("                print(2)", "        print(2)");
        UnparseableBucketsOf(d12, new CellRequest(OnType, null, 3), cell16).Should().Equal(Depth);
        UnparseableBucketsOf(d12, new CellRequest(OnType, null, 3), D12).Should().BeEmpty();

        var d21 = Generated(Q21, D21, S3);
        d21.Truth!.TargetLines.Should().Equal(2);
        var cell21 = "def main():\n        a = 1\n    if a == 1:\n";
        UnparseableBucketsOf(d21, new CellRequest(OnType, null, 2), cell21).Should().Equal(Depth);
        UnparseableBucketsOf(d21, new CellRequest(OnType, null, 2), D21).Should().BeEmpty();
    }

    /// <summary>
    /// <c>content</c>: a fallback result on D21 that drops a character, and one that drops a line. Clean: no
    /// edits, and cell 21's re-indent (a whitespace change is not content).
    /// </summary>
    [Fact]
    public void PositiveControl_Content_SeesADroppedCharacterAndLine()
    {
        var d21 = Generated(Q21, D21, S3);
        var full = new CellRequest(Full, null, 0);

        UnparseableBucketsOf(d21, full, "def main():\n        a = \n        if a == 1:\n").Should().Equal(Content);
        UnparseableBucketsOf(d21, full, "def main():\n        if a == 1:\n").Should().Equal(Content);
        UnparseableBucketsOf(d21, full, D21).Should().BeEmpty();
        UnparseableBucketsOf(d21, full, "def main():\n        a = 1\n    if a == 1:\n").Should().NotContain(Content);
    }

    /// <summary>
    /// <c>content</c> sees a changed line break: the CRLF and CR twins of D21 with their breaks rewritten to
    /// <c>\n</c> (the full fallback's CRLF → LF, A6), and one break of the CR twin changed. Clean: no edits, and
    /// a re-indent that keeps every break.
    /// </summary>
    [Fact]
    public void PositiveControl_Content_SeesAChangedLineBreak()
    {
        var full = new CellRequest(Full, null, 0);
        foreach (var twin in new Func<string, string>[] { FormatterTwins.Crlf, FormatterTwins.Cr })
        {
            var d21 = Generated(twin(Q21), twin(D21), S3);
            UnparseableBucketsOf(d21, full, D21).Should().Equal(Content);
            UnparseableBucketsOf(d21, full, d21.Text).Should().BeEmpty();
            UnparseableBucketsOf(d21, full, twin("def main():\n        a = 1\n    if a == 1:\n")).Should().NotContain(Content);
        }

        var cr = Generated(FormatterTwins.Cr(Q21), FormatterTwins.Cr(D21), S3);
        UnparseableBucketsOf(cr, full, "def main():\r        a = 1\r\n        if a == 1:\r").Should().Equal(Content);
    }

    /// <summary>#2271's N4: <see cref="Q4"/> with the string's opener and closer line and the body's last line at 2 spaces — abort-free @ 3c70ea492.</summary>
    private const string Q4 = "def main():\n    s = \"\"\"\n        key: value\n    \"\"\"\n    print(s)\n";
    private const string N4 = "def main():\n  s = \"\"\"\n        key: value\n  \"\"\"\n  print(s)\n";

    /// <summary>
    /// <c>factMissing</c>: the issue's N4, built from its 4-space parent by the S6 builder — D's own lex
    /// loses the content line 2 (no token starts it inside a literal), and the bucket fires whenever the fact
    /// is false for such a document. Clean: the same lost lines with the fact set. By direction: on the
    /// handler's real on-type result for line 2, <c>factMissing</c> and <c>literal</c> fire together or not at
    /// all — a missing fact is exactly what lets a route rewrite the string's content.
    /// </summary>
    [Fact]
    public void PositiveControl_FactMissing_FiresOnTheIssuesN4Document()
    {
        var n4 = Reindented(Q4, Doc(Q4).Facts!, new[] { 1, 3, 4 }, "  ", FormatterTwins.DroppedShapePrefix + "SPY0013");
        n4.Text.Should().Be(N4);
        n4.State.Should().Be(S6);
        n4.Ast.Should().BeNull("the document must not parse");
        n4.Truth!.LiteralLines.Should().BeEquivalentTo(new[] { 2, 3 });
        n4.LiteralState.LostLiteralLines.Should().Contain(2, "D's lex reads the content line as code");

        var onType = new CellRequest(OnType, null, 2);
        var lost = n4.LiteralState.LostLiteralLines;
        UnparseableBucketsOf(n4.WithLexFacts(false, lost), onType, N4).Should().Equal(FactMissing);
        UnparseableBucketsOf(n4.WithLexFacts(true, lost), onType, N4).Should().BeEmpty();

        var applied = _driver.OnType(N4, 2).Applied;
        var buckets = UnparseableBucketsOf(n4, onType, applied);
        buckets.Contains(FactMissing).Should().Be(buckets.Contains(Literal),
            $"a missing fact must be what lets on-type rewrite line 2 — applied:\n{applied}");
    }

    /// <summary>
    /// <c>factSpurious</c>: D21 (an S3 cut, no literal) with its fact forced true through the test seam — the
    /// fact switches the routes off while no literal line is lost. Clean: D21's real fact (false) and D19's
    /// (true, and lines are lost).
    /// </summary>
    [Fact]
    public void PositiveControl_FactSpurious_FiresOnAStubbedFact()
    {
        var d21 = Generated(Q21, D21, S3);
        d21.Truth!.LiteralLines.Should().BeEmpty();
        var full = new CellRequest(Full, null, 0);

        UnparseableBucketsOf(d21.WithLexFacts(true, Array.Empty<int>()), full, D21).Should().Equal(FactSpurious);
        UnparseableBucketsOf(d21.WithLexFacts(false, Array.Empty<int>()), full, D21).Should().BeEmpty();
        d21.LiteralState.FactSet.Should().BeFalse();
        UnparseableBucketsOf(d21, full, D21).Should().BeEmpty();

        var d19 = Inserted(Q19, Doc(Q19).Facts!, 1, "    \"\"\"", S5, null);
        d19.LiteralState.FactSet.Should().BeTrue();
        d19.LiteralState.LostLiteralLines.Should().NotBeEmpty();
        UnparseableBucketsOf(d19, full, D19).Should().BeEmpty();
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

    /// <summary>The loader refuses a <c>cli</c>/<c>full</c> row on a parseable state, an unknown field, a missing cite, an S6-family row citing another state's issue, and a duplicate.</summary>
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
            "a/b identity ontype S6 literal # #2273 the S6n issue on an S6 row",
            "a/b identity full S6k literal # #2271 the S6 issue on an S6k row",
        })
        {
            FluentActions.Invoking(() => ParseAllowlist(new[] { bad })).Should().Throw<InvalidOperationException>(bad);
        }

        FluentActions.Invoking(() => ParseAllowlist(new[] { "a/b identity ontype S2 net # #2168 r", "a/b identity ontype S2 net # #2168 r" }))
            .Should().Throw<InvalidOperationException>();
        ParseAllowlist(new[] { "a/b identity range-line S2 local # #2168 r" }).Should().ContainSingle();
        ParseAllowlist(new[] { "a/b identity full S6 literal # #2271 r", "a/b identity full S6n depth # #2273 r", "a/b identity full S6k literal # #2274 r" })
            .Should().HaveCount(3);
    }

    // ================================================================
    // Census
    // ================================================================

    /// <summary>
    /// Ordered after the theories (<see cref="RouteParitySweepOrderer"/>): prints the cells that ran per
    /// route × state × twin, <c>refusedWork</c> per range kind and twin, the F-declined stems, the wall time
    /// and the stride, the S4–S6 cells per literal kind, the S6 cells per shape and route and its abort-free
    /// documents per shape (<see cref="DocumentTallies"/>), the <c>ontype</c> cells on continuation lines, and per
    /// route and twin the S3 cells whose applied text differs from the document. Floors: every route of every state
    /// has ≥ 1 cell; S4 and S5 have ≥ 1 cell per <see cref="TripleQuotedKinds"/> kind; every S6 shape but the
    /// comment re-pair has ≥ 1 abort-free document with a literal line to lose (the comment re-pair: ≥ 1 cell per
    /// route), and S6's dropped-delimiter shapes ≥ 1 cell per <see cref="TripleQuotedKinds"/> kind; the backslash and
    /// bracket continuation shapes have ≥ 1 <c>ontype</c> cell; each S3 route edits ≥ 1 document of each twin
    /// whose theory ran over every stem ("no edits" passes every unparseable oracle, and a route that counts
    /// lines differently from the client declines a whole twin's documents silently — the lone-<c>\r</c> twin's
    /// on-type and range fallback @ 88345108f, #2168). When the theories did not run in this
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
        var (cells, tallies) = measured
            ? (new SCG.Dictionary<(string, string, string), int>(s_cellsRun), new SCG.Dictionary<string, int>(s_tallies, StringComparer.Ordinal))
            : Enumerate(stride);
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
                + string.Join(" ", Twins.Select(t => $"{t}={Measure(route, t, RefusedMeasure)}/{Measure(route, t, WithWorkMeasure)}"
                    + $" ({Percent(Measure(route, t, RefusedMeasure), Measure(route, t, WithWorkMeasure))})")));
        }

        foreach (var route in Routes.Where(RangeRoutes.Contains))
        {
            _output.WriteLine($"FMTROUTE-CENSUS overReach {route} "
                + string.Join(" ", Twins.Select(t => $"{t}={Measure(route, t, AppliedOutsideMeasure)}/{Measure(route, t, AppliedSelectedMeasure)}"
                    + $" ({Percent(Measure(route, t, AppliedOutsideMeasure), Measure(route, t, AppliedSelectedMeasure))})"
                    + $" straddling={Measure(route, t, StraddlingMeasure)}/{Measure(route, t, WithWorkMeasure) - Measure(route, t, RefusedMeasure)}")));
        }

        foreach (var route in Routes.Where(RangeRoutes.Contains))
        {
            _output.WriteLine($"FMTROUTE-CENSUS refusedOnUnmatchedEqualLine {route} "
                + string.Join(" ", Twins.Select(t => $"{t}={Measure(route, t, RefusedTieMeasure)}/{Measure(route, t, RefusedMeasure)}")));
        }

        var kinds = TripleQuotedKinds.Concat(tallies.Keys.Where(k => k.StartsWith("kind ", StringComparison.Ordinal)).Select(k => k.Split(' ', 3)[2]))
            .Distinct(StringComparer.Ordinal).ToArray();
        foreach (var state in new[] { S4, S5, S6, S6Dropped })
        {
            _output.WriteLine($"FMTROUTE-CENSUS literal-kind {state} "
                + string.Join(" ", kinds.Select(k => $"{k}={tallies.GetValueOrDefault(KindTally(state, k))}")));
        }

        foreach (var (state, shape) in LossStateShapes())
        {
            _output.WriteLine($"FMTROUTE-CENSUS shape {state} {shape}={RoutesByState[state].Sum(r => tallies.GetValueOrDefault(ShapeTally(state, shape, r)))} "
                + string.Join(" ", RoutesByState[state].Select(r => $"{r}={tallies.GetValueOrDefault(ShapeTally(state, shape, r))}"))
                + $" abort-free documents={tallies.GetValueOrDefault(AbortFreeTally(state, shape))}/{tallies.GetValueOrDefault(LossDocumentTally(state, shape))}");
        }

        _output.WriteLine($"FMTROUTE-CENSUS continuation ontype (full-mode cells) {BackslashContinuation}={tallies.GetValueOrDefault($"ontype-{BackslashContinuation}")} "
            + $"{BracketContinuation}={tallies.GetValueOrDefault($"ontype-{BracketContinuation}")}");
        if (measured)
        {
            foreach (var route in RoutesByState[S3])
            {
                _output.WriteLine($"FMTROUTE-CENSUS S3 applied-differs {route} "
                    + string.Join(" ", Twins.Select(t => $"{t}={tallies.GetValueOrDefault(ChangedTally(route, S3, t))}")));
            }
        }

        var rows = Allowlist.Value;
        foreach (var bucket in Buckets)
            _output.WriteLine($"FMTROUTE-CENSUS allowlist {bucket}={rows.Count(r => r.Bucket == bucket)}");

        // Every floor is reported, not the first one that fails.
        using var scope = new AssertionScope();
        foreach (var state in States)
        {
            foreach (var route in RoutesByState[state])
            {
                Twins.Sum(t => cells.GetValueOrDefault((route, state, t))).Should()
                    .BeGreaterThanOrEqualTo(1, $"route {route} must have a cell in state {state}");
            }
        }

        foreach (var state in new[] { S4, S5 })
        {
            foreach (var kind in TripleQuotedKinds)
            {
                tallies.GetValueOrDefault(KindTally(state, kind)).Should()
                    .BeGreaterThanOrEqualTo(1, $"state {state} must have a cell that loses a {kind} literal (the lexer's read path for it)");
            }
        }

        foreach (var shape in new[] { BackslashContinuation, BracketContinuation })
        {
            tallies.GetValueOrDefault($"ontype-{shape}").Should()
                .BeGreaterThanOrEqualTo(1, $"an ontype cell must sit on a {shape} continuation line");
        }

        // S6 (P22f, #2271): every shape builds a document that loses a literal WITHOUT an abort inside its
        // read — else it is S5 again and proves nothing about the other mechanisms. S6n (#2273) is the cure's
        // direction control: it must have an abort-free document too. S6k (#2274, the comment re-pair) is a
        // route-only guard: the per-route cell floor above is its floor.
        foreach (var (state, shape) in LossStateShapes().Where(p => p.State != S6k))
        {
            tallies.GetValueOrDefault(AbortFreeTally(state, shape)).Should()
                .BeGreaterThanOrEqualTo(1, $"{state} shape {shape} must build an abort-free document"
                    + (state == S6 ? " that has a literal line to lose" : ""));
        }

        // The short-string re-pair's appended `_ = '"""'` line is what keeps the triple count even; a Q that
        // already holds a triple inside a short string (N2's own shape) is abort-free without it, so "≥ 1"
        // cannot see the line go missing. More than half its documents are abort-free: 65/75 in FULL mode
        // @ 3c70ea492 (the corpus with strings/triple_quoted_delimiter_lines), 10/75 without the line.
        (2 * tallies.GetValueOrDefault(AbortFreeTally(S6, FormatterTwins.RepairShape))).Should()
            .BeGreaterThan(tallies.GetValueOrDefault(LossDocumentTally(S6, FormatterTwins.RepairShape)),
                "most S6 repair documents must be abort-free — the appended short string closes the last re-paired run");

        foreach (var kind in TripleQuotedKinds)
        {
            tallies.GetValueOrDefault(KindTally(S6Dropped, kind)).Should()
                .BeGreaterThanOrEqualTo(1, $"S6's dropped-delimiter shapes must have a cell that loses a {kind} literal");
        }

        // "No edits" passes every S3-S5 oracle; that some cell of each route DOES edit an S3 document is
        // what keeps a route that always declines (a lexer flag that is always set) from passing unseen.
        if (measured)
        {
            foreach (var twin in Twins.Where(t => s_stemsRun.GetValueOrDefault(t) == corpus.Corpus.Count))
            {
                foreach (var route in RoutesByState[S3])
                {
                    tallies.GetValueOrDefault(ChangedTally(route, S3, twin)).Should()
                        .BeGreaterThanOrEqualTo(1, $"some {route} cell must apply a text that differs from its S3 document on the {twin} twin");
                }
            }
        }

        rows.Should().OnlyContain(r => corpus.Corpus.ContainsKey(r.Stem), "every allowlist row names a corpus fixture");

        // P22e drained the allowlist to EMPTY at Phase 3 Task 3 (the last ontype rows). P22f Phase 1 Task 4
        // re-populated it from a FULL-mode run with the S5/S6 rows its Phase 2 drains (#2271: 454) and the
        // S6n (#2273: 334) and S6k (#2274: 20) rows of their own trackers; Phase 2 Task 5 drained the 454
        // (the lexer's fact covers every abort-free mechanism). The literal anchors it: changing
        // the allowlist is a visible decision — change this count in the same commit and say why.
        rows.Count.Should().Be(354, "P22f Phase 2 Task 5 drained the 454 #2271 rows; S6n 334 (#2273) and S6k 20 (#2274) remain");

        AssertRefusalCeilings(corpus.Corpus.Count);
    }

    /// <summary>
    /// The refusal ceilings of plan P22e Phase 2 Task 4 (owner ruling R-DK, option (a) kept): per range kind,
    /// on the <c>identity</c> and <c>comment</c> twins, the percentage of cells with work
    /// (<see cref="CellVerdict.WithWork"/>) that Format Selection declines (<c>refusedWork</c>), measured in
    /// full mode @ ec673074f and rounded UP to the next whole percent above it (a measured 0.00% pins 1%:
    /// a <c>range-whole</c> refusal is also a <c>whole</c> failure, judged cell by cell). A change that makes
    /// selections refuse more often is red. The <c>wide</c> twin refuses by construction (a blank or comment line splits a
    /// block's re-indent into hunks, and a partial re-indent fails the net) and the <c>crlf</c> and <c>cr</c> twins
    /// are <c>identity</c>'s line breaks: all three are printed, not judged.
    /// </summary>
    internal static readonly SCG.IReadOnlyDictionary<(string Route, string Twin), int> RefusalCeilingPercent =
        new SCG.Dictionary<(string Route, string Twin), int>
        {
            [(RangeWhole, Identity)] = 1,      // 0/2389 (0.00%)
            [(RangeStatement, Identity)] = 1,  // 1/1443 (0.07%)
            [(RangeBlock, Identity)] = 1,      // 6/2721 (0.22%)
            [(RangeLine, Identity)] = 4,       // 33/1078 (3.06%) @ 843abc9c1 — was 3 (28/1070 @ ec673074f); +5 refusals: the delimiter-line fixture's multi-line f"""/t""" collapse under Format, so Format Selection declines the cross-line hunk
            [(RangeWhole, Comment)] = 1,       // 0/3284 (0.00%)
            [(RangeStatement, Comment)] = 1,   // 0/1184 (0.00%)
            [(RangeBlock, Comment)] = 1,       // 4/2854 (0.14%)
            [(RangeLine, Comment)] = 12,       // 46/402 (11.44%)
        };

    /// <summary>
    /// <c>range-line</c> on S1/S2 is a SAMPLED route, so the default (sampled) mode measures it over the
    /// stem sample (<see cref="SampledStride"/>, a fixed FNV bucket) and is judged against its own ceiling,
    /// measured the same way @ ec673074f (rounded up as above); the other range kinds run every cell in both modes and share
    /// <see cref="RefusalCeilingPercent"/>.
    /// </summary>
    internal static readonly SCG.IReadOnlyDictionary<string, int> SampledRangeLineCeilingPercent =
        new SCG.Dictionary<string, int>(StringComparer.Ordinal)
        {
            [Identity] = 9,  // 12/138 (8.70%)
            [Comment] = 6,   // 2/34 (5.88%)
        };

    /// <summary>
    /// Asserts <see cref="RefusalCeilingPercent"/> for each judged twin whose theory ran over EVERY corpus stem
    /// in this process — a filter that ran a subset (or the census alone) measures another population, and
    /// that twin's ceilings are skipped and printed as such.
    /// </summary>
    private void AssertRefusalCeilings(int corpusCount)
    {
        foreach (var twin in new[] { Identity, Comment })
        {
            var stems = s_stemsRun.GetValueOrDefault(twin);
            if (stems != corpusCount)
            {
                _output.WriteLine($"FMTROUTE-CENSUS refusal-ceiling {twin} skipped: its theory ran {stems} of {corpusCount} stems in this process");
                continue;
            }

            foreach (var route in Routes.Where(RangeRoutes.Contains))
            {
                var ceiling = route == RangeLine && !FullMode
                    ? SampledRangeLineCeilingPercent[twin]
                    : RefusalCeilingPercent[(route, twin)];
                var refused = Measure(route, twin, RefusedMeasure);
                var withWork = Measure(route, twin, WithWorkMeasure);
                _output.WriteLine($"FMTROUTE-CENSUS refusal-ceiling {route} {twin} {refused}/{withWork} ({Percent(refused, withWork)}) ceiling={ceiling}%"
                    + (route == RangeLine && !FullMode ? " (sampled)" : ""));
                (refused * 100).Should().BeLessThanOrEqualTo(ceiling * withWork,
                    $"Format Selection ({route}, {twin} twin) declined {refused} of {withWork} selections with work — more than the {ceiling}% ceiling measured @ ec673074f");
            }
        }
    }

    private static long Measure(string route, string twin, string measure) => s_rangeCensus.GetValueOrDefault((route, twin, measure));

    private static string Percent(long n, long d)
        => d == 0 ? "-" : (100.0 * n / d).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "%";

    private static (SCG.Dictionary<(string, string, string), int> Cells, SCG.Dictionary<string, int> Tallies) Enumerate(int stride)
    {
        var counts = new ConcurrentDictionary<(string, string, string), int>();
        var tallies = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        Parallel.ForEach(Corpus.Value.Corpus.Keys, stem =>
        {
            var sampledIn = IsSampledIn(stem, stride);
            foreach (var twin in Twins)
            {
                foreach (var doc in Documents(stem, twin, out _))
                {
                    foreach (var key in DocumentTallies(doc))
                        tallies.AddOrUpdate(key, 1, (_, n) => n + 1);
                    foreach (var request in Cells(doc))
                    {
                        foreach (var key in Tallies(doc, request))
                            tallies.AddOrUpdate(key, 1, (_, n) => n + 1);
                        if (IsSampledCell(request.Route, doc.State) && !sampledIn)
                            continue;
                        counts.AddOrUpdate((request.Route, doc.State, twin), 1, (_, n) => n + 1);
                    }
                }
            }
        });
        return (new SCG.Dictionary<(string, string, string), int>(counts), new SCG.Dictionary<string, int>(tallies, StringComparer.Ordinal));
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
        _ when method.StartsWith("Cr_", StringComparison.Ordinal) => 4,
        _ when method.StartsWith("Census_", StringComparison.Ordinal) => 6,
        _ => 5,
    };
}
