using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Tests.E2E;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Registry;
using Xunit;
using Xunit.Abstractions;
// The enclosing `Sharpy` namespace exposes Core's Pythonic collections, which shadow System's; SCG-qualify.
using SCG = System.Collections.Generic;
using CompilerNullLogger = Sharpy.Compiler.Logging.NullLogger;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// P54 (#2250) cells 2–3, found by the P47 code-action sweep (issuecomment-6120931669): <b>an analysis, and every
/// semantic query a handler makes on it, is a function of the document alone</b> — whatever the
/// <see cref="CompilerApi"/> analysed before (ORDER) or alongside (CONCURRENCY). One LSP server process holds one
/// <see cref="CompilerApi"/> for every document it serves, so a warm instance must answer as a cold process does.
///
/// <para><b>Matrix.</b> The three P47 stems at the twin and selection the sweep measured them on (<see cref="Targets"/>)
/// × warmth {<c>fresh</c> — a new <see cref="CompilerApi"/> per target in this (warm) test process, the positive
/// control that separates instance state from process-wide state; <c>warm-sequential</c> — ONE shared instance after
/// the sweep's earlier documents (<see cref="EarlierDocuments"/>); <c>warm-concurrent</c> — one shared instance
/// serving the same documents at DOP <see cref="Dop"/>, <see cref="Repetitions"/> times} × operation {<c>analyze</c> —
/// the published diagnostics of the source; <c>extract</c> — Extract method through the REAL
/// <c>SharpyCodeActionHandler</c> on the selection, applied as a client must; <c>applied-analyze</c> — the published
/// diagnostics of the COLD applied text (the same text for every warmth, so the analysis is judged apart from the
/// extract)}. The oracle (<see cref="ColdOracle"/>) is the P47 sweep's own: a child <c>Sharpy.Lsp</c> process per
/// document via <see cref="LspTestClient"/> (this project does not reference <c>Sharpy.Cli</c>).</para>
///
/// <para><b>Fingerprint.</b> Diagnostics: <c>success</c> (no error) + the sorted <c>code@line:col</c> (1-based).
/// Extract: the applied text's first parse error position (the P47 reading — <c>(12,59)</c> cold, <c>(12,49)</c> warm
/// @ 43aab2658) + a digest of the applied text.</para>
///
/// <para><b>Seam measurement</b> (Design Decision 4). Between every step of the sequential arm, and around the
/// concurrent arm, the masters' dictionary key sets are snapshotted (<see cref="MasterRegistryKeySets"/>) and the first
/// diverging key is printed to the test output and to <c>.claude/tmp/warm-instance-parity/</c> (with the cold applied texts).</para>
///
/// <para><b>KnownRed rows</b> (<see cref="KnownRedCells"/>, P54 Phase 1 convention): a deterministic row asserts the
/// divergence REPRODUCES (a cure turns it red as stale: drain the row); a probabilistic row reports
/// <c>k/R</c> and asserts nothing; every other cell asserts parity. No row is live since P54 Phase 2 task 3. <c>SHARPY_WARM_PARITY_FULL=1</c> replays the P47
/// FULL ordering (every stem's identity and crlf twin, then the cr twin in corpus order up to each target — about 20
/// minutes) instead of the stride sample.</para>
/// </summary>
public sealed class WarmInstanceParityTests : IClassFixture<WarmInstanceParityTests.ColdOracle>
{
    private readonly ITestOutputHelper _output;
    private readonly ColdOracle _cold;

    public WarmInstanceParityTests(ColdOracle cold, ITestOutputHelper output)
    {
        _cold = cold;
        _output = output;
    }

    // ================================================================
    // The matrix
    // ================================================================

    /// <summary>One P47 cell: a corpus stem, the twin the sweep measured it on, and the Extract-method selection.</summary>
    internal sealed record Target(string Stem, string Twin, LspRange Range)
    {
        public string Id => $"{Stem[(Stem.LastIndexOf('/') + 1)..]}#{Twin}";

        public string Text => CodeActionShapeSweepTests.TwinOf(SweepCorpus.Corpus.Value.Corpus[Stem].Source, Twin);
    }

    private static LspRange R(int l0, int c0, int l1, int c1) => new(new Position(l0, c0), new Position(l1, c1));

    /// <summary>The three P47 stems at the cr twin and the selections of issuecomment-6120931669 (0-based LSP ranges).</summary>
    internal static readonly Target[] Targets =
    {
        new("fstrings/format_iformattable_class_1988", CodeActionShapeSweepTests.Cr, R(12, 0, 19, 24)),
        new("fstrings/format_no_dunder_format_refused_1988", CodeActionShapeSweepTests.Cr, R(8, 0, 16, 23)),
        new("deferred_overload_annotated_pin_1589", CodeActionShapeSweepTests.Cr, R(4, 0, 9, 21)),
    };

    internal const string Fresh = "fresh";
    internal const string WarmSequential = "warm-sequential";
    internal const string WarmConcurrent = "warm-concurrent";
    internal static readonly string[] Warmths = { Fresh, WarmSequential, WarmConcurrent };

    internal const string Analyze = "analyze";
    internal const string Extract = "extract";
    internal const string AppliedAnalyze = "applied-analyze";
    internal static readonly string[] Operations = { Analyze, Extract, AppliedAnalyze };

    /// <summary>The concurrent arm's degree of parallelism (the P47 sweep's default) and repetitions.</summary>
    internal const int Dop = 4;

    internal const int Repetitions = 4;

    /// <summary>Regular mode's prefix: the identity and crlf twins of the stems whose stable bucket under this stride is 0.</summary>
    internal const int PrefixStride = 1024;

    /// <summary>
    /// The earlier documents measured to flip the targets on ONE instance (<see cref="WarmInstanceTriggers"/>): two
    /// Wrap-in-try applied texts the P47 sweep analysed @ 43aab2658, found by replaying the sweep on one instance and
    /// re-reading the targets after every document; the sweep no longer produces them, so they are replayed by text
    /// right before each target (and shuffled into the concurrent arm). With them, the TYPE-definition twins
    /// (<see cref="WarmInstanceTriggers.TypeDefinitionTexts"/>: a <c>class str</c> and its struct/interface/union/delegate
    /// and dataclass siblings inside a block, which wrote the master <c>str</c> until the type-definition seam).
    /// </summary>
    internal static readonly (string Name, string Text)[] TriggerTexts = WarmInstanceTriggers.AllTexts;

    /// <summary>The concurrent arm's marker for a trigger-text item.</summary>
    private const string TriggerTwin = "trigger";

    internal static string CellId(Target target, string warmth, string operation) => $"{target.Id} {warmth} {operation}";

    internal static bool FullMode => System.Environment.GetEnvironmentVariable("SHARPY_WARM_PARITY_FULL") == "1";

    /// <summary>One reading of the three operations on one target.</summary>
    internal sealed record Reading(string Analyze, string Extract, string AppliedAnalyze)
    {
        public string Of(string operation) => operation switch
        {
            WarmInstanceParityTests.Analyze => Analyze,
            WarmInstanceParityTests.Extract => Extract,
            WarmInstanceParityTests.AppliedAnalyze => AppliedAnalyze,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "unknown operation"),
        };
    }

    // ================================================================
    // KnownRed rows
    // ================================================================

    /// <summary>A KnownRed row: whether the cell is a probabilistic race (reported, never asserted) and its citation.</summary>
    internal sealed record KnownRed(bool Probabilistic, string Row);

    /// <summary>
    /// The cells measured red at the implementation sha, keyed by <see cref="CellId"/>. Each row cites #2250 and its
    /// measured reproduction <c>k/R @ sha</c>; "drains in Phase 2 task 3".
    /// </summary>
    internal static readonly SCG.IReadOnlyDictionary<string, KnownRed> KnownRedCells = new SCG.Dictionary<string, KnownRed>(StringComparer.Ordinal)
    {
        // Empty since P54 Phase 2 task 3. Cells 2 (ORDER, warm-sequential, deterministic) and 3 (CONCURRENCY,
        // warm-concurrent, probabilistic) were one write: a trigger text's builtin-named def inside a `try:` body
        // resolved by name to the master `len`/`format` and wrote its signature onto it, so the IFormattable sources
        // gained SPY0220 and Extract method parsed at (12,55)/(8,55)/(5,148) instead of the cold (12,59)/(8,59)/(5,149).
        // The def now declares its own symbol and the master's symbols are frozen.
    };

    [Fact]
    public void KnownRedRows_NameCellsOfTheMatrix()
    {
        var cells = Targets.SelectMany(t => Warmths.SelectMany(w => Operations.Select(o => CellId(t, w, o)))).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(Targets.Length * Warmths.Length * Operations.Length, cells.Count);
        foreach (var (cell, row) in KnownRedCells)
        {
            Assert.True(cells.Contains(cell), $"KnownRed row '{cell}' names no cell of the matrix");
            Assert.False(cell.Contains($" {Fresh} ", StringComparison.Ordinal), $"'{cell}': the fresh arm is the positive control; it carries no KnownRed row");
            Assert.Matches(@"(^|\s)#\d+\b", row.Row);
            Assert.Matches(@"\d+/\d+[^@]* @ [0-9a-f]{7,}", row.Row);
        }
    }

    // ================================================================
    // The arms
    // ================================================================

    /// <summary>
    /// Positive control: a FRESH <see cref="CompilerApi"/> per target, in this test process, answers as the cold child
    /// process on every operation — the oracle is not "always red", and a divergence in the warm arms is instance state.
    /// Also pins that the oracle saw what the cells are about: Extract method is offered on every selection and its
    /// applied text does not parse (the P47 shape), and the refused fixture's SPY0609s are published.
    /// </summary>
    [Fact]
    public void Fresh_ANewInstancePerTarget_AnswersAsTheColdProcess()
    {
        var readings = new SCG.Dictionary<string, SCG.List<Reading>>(StringComparer.Ordinal);
        foreach (var target in Targets)
        {
            var api = NewApi();
            using var driver = new LspCodeActionDriver(api);
            readings[target.Id] = new SCG.List<Reading> { Measure(driver, target, _cold.Of(target).AppliedText) };
        }

        foreach (var target in Targets)
        {
            var cold = _cold.Of(target);
            Assert.True(cold.AppliedText is not null, $"{target.Id}: the cold process offered no Extract method on {target.Range}");
            Assert.StartsWith("parse@", cold.Reading.Extract, StringComparison.Ordinal);
        }

        Assert.Contains("SPY0609", _cold.Of(Targets[1]).Reading.Analyze, StringComparison.Ordinal);
        Judge(Fresh, readings, new SCG.List<string>());
    }

    /// <summary>
    /// Cell 2 (ORDER): ONE shared <see cref="CompilerApi"/> and driver serve the sweep's earlier documents
    /// (<see cref="EarlierDocuments"/>), then each target in the P47 order — its identity twin's offers, then the
    /// analysis, the Extract-method request and the applied text's analysis of its cr twin — with the masters' key sets
    /// snapshotted between every step.
    /// </summary>
    [Fact]
    public void WarmSequential_OneSharedInstance_AnswersAsTheColdProcess()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var api = NewApi();
        using var driver = new LspCodeActionDriver(api);
        api.PrimeAnalysisContextForTests();
        var log = new SCG.List<string> { RunEnvironment() };
        var snapshot = MasterRegistryKeySets.Take(api);
        var readings = new SCG.Dictionary<string, SCG.List<Reading>>(StringComparer.Ordinal);
        var served = 0;
        foreach (var step in EarlierDocuments(FullMode))
        {
            if (step.Target is { } target)
            {
                snapshot = Snapshot(api, snapshot, $"prefix ({served} documents served)", log);
                foreach (var (name, text) in TriggerTexts)
                {
                    api.Analyze(text, CancellationToken.None);
                    snapshot = Snapshot(api, snapshot, $"trigger text {name}", log);
                }

                Serve(driver, api, target.Stem, CodeActionShapeSweepTests.Identity, applyAndAnalyze: false);
                snapshot = Snapshot(api, snapshot, $"{target.Id} identity offers", log);
                using var session = driver.Open(target.Text);
                var analyze = DiagFingerprint(session.Diagnostics);
                snapshot = Snapshot(api, snapshot, $"{target.Id} {Analyze}", log);
                var extract = ExtractFingerprint(ExtractIn(session, target.Range));
                snapshot = Snapshot(api, snapshot, $"{target.Id} {Extract}", log);
                var appliedAnalyze = AnalyzeIn(driver, _cold.Of(target).AppliedText);
                snapshot = Snapshot(api, snapshot, $"{target.Id} {AppliedAnalyze}", log);
                readings[target.Id] = new SCG.List<Reading> { new(analyze, extract, appliedAnalyze) };
                served = 0;
            }
            else
            {
                Serve(driver, api, step.Stem, step.Twin, applyAndAnalyze: true);
                served++;
            }
        }

        log.Add($"elapsed {clock.Elapsed.TotalSeconds:F1}s (full={FullMode})");
        Judge(WarmSequential, readings, log);
    }

    /// <summary>
    /// Cell 3 (CONCURRENCY): one shared <see cref="CompilerApi"/> and driver serve the regular prefix's documents and
    /// the targets <see cref="Repetitions"/> times in a seeded shuffle at DOP <see cref="Dop"/>; each target reading is
    /// judged against the cold process (probabilistic: <c>k/R</c>).
    /// </summary>
    [Fact]
    public void WarmConcurrent_OneSharedInstanceAtDop4_AnswersAsTheColdProcess()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var api = NewApi();
        using var driver = new LspCodeActionDriver(api);
        api.PrimeAnalysisContextForTests();
        var log = new SCG.List<string> { RunEnvironment() };
        var before = MasterRegistryKeySets.Take(api);
        var documents = EarlierDocuments(full: false).Where(s => s.Target is null && s.Twin == CodeActionShapeSweepTests.Identity).ToArray();
        var random = new Random(2250);
        var items = new SCG.List<(int Repetition, Target? Target, string Stem, string Twin)>();
        for (var repetition = 0; repetition < Repetitions; repetition++)
        {
            var round = documents.Select(d => (repetition, (Target?)null, d.Stem, d.Twin))
                .Concat(Targets.Select(t => (repetition, (Target?)t, t.Stem, t.Twin)))
                .OrderBy(_ => random.Next())
                .ToArray();
            items.AddRange(round);
        }

        var readings = new ConcurrentDictionary<string, ConcurrentBag<Reading>>(StringComparer.Ordinal);
        foreach (var (name, _) in TriggerTexts)
            items.Insert(random.Next(items.Count), (-1, null, name, TriggerTwin));
        Parallel.ForEach(items, new ParallelOptions { MaxDegreeOfParallelism = Dop }, item =>
        {
            if (item.Twin == TriggerTwin)
                api.Analyze(TriggerTexts.Single(t => t.Name == item.Stem).Text, CancellationToken.None);
            else if (item.Target is { } target)
                readings.GetOrAdd(target.Id, _ => new ConcurrentBag<Reading>()).Add(Measure(driver, target, _cold.Of(target).AppliedText));
            else
                Serve(driver, api, item.Stem, item.Twin, applyAndAnalyze: true);
        });

        var after = MasterRegistryKeySets.Take(api);
        log.Add($"key sets across the concurrent arm ({items.Count} items, DOP {Dop}): first divergence {before.FirstDivergence(after)}");
        log.Add(before.Describe(after, 6));
        log.Add($"elapsed {clock.Elapsed.TotalSeconds:F1}s");
        Judge(WarmConcurrent, readings.ToDictionary(p => p.Key, p => p.Value.ToList(), StringComparer.Ordinal), log);
    }

    /// <summary>
    /// The verifier's F1 cell (P54 #2250 @ effba7776), as one Sharpy.Lsp served it: a later document that reads a member
    /// the master <c>str</c> does not have (<see cref="WarmInstanceTriggers.LaterDocumentProbes"/>) must publish what a
    /// cold process publishes after EACH trigger text — opened through the server's document route and analysed by
    /// text — was served on the same instance. Before the type-definition seam the <c>class str</c> text added a
    /// <c>_size</c> backing field to the master <c>str</c> and the probe's SPY0203 vanished on the warm instance.
    /// Positive controls: the cold oracle publishes SPY0203 for the probe (the cell reads what it is about), and each
    /// probe opened first on the same instance — before any trigger — equals the cold reading.
    /// </summary>
    [Fact]
    public async Task WarmSequential_LaterDocumentProbes_AnswerAsTheColdProcessAfterEveryTrigger()
    {
        var cold = new SCG.Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, text) in WarmInstanceTriggers.LaterDocumentProbes)
            cold[name] = (await ColdAsync(text, null)).Diagnostics;
        Assert.Contains("SPY0203", cold["str-has-no-_size"], StringComparison.Ordinal);

        var api = NewApi();
        using var driver = new LspCodeActionDriver(api);
        var problems = new SCG.List<string>();
        void ReadProbes(string after)
        {
            foreach (var (name, text) in WarmInstanceTriggers.LaterDocumentProbes)
            {
                using var session = driver.Open(text);
                var warm = DiagFingerprint(session.Diagnostics);
                _output.WriteLine($"{name} after {after}: {warm}");
                if (!string.Equals(warm, cold[name], StringComparison.Ordinal))
                    problems.Add($"{name} after {after}: warm [{warm}] / cold [{cold[name]}]");
            }
        }

        ReadProbes("nothing (the positive control)");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
        foreach (var (name, text) in TriggerTexts)
        {
            using (driver.Open(text))
            {
            }

            api.Analyze(text, CancellationToken.None);
            ReadProbes($"trigger {name}");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // ================================================================
    // Judging
    // ================================================================

    private void Judge(string warmth, SCG.IReadOnlyDictionary<string, SCG.List<Reading>> readings, SCG.List<string> log)
    {
        var problems = new SCG.List<string>();
        foreach (var target in Targets)
        {
            var cold = _cold.Of(target);
            log.Add($"cold {target.Id}: {cold.Reading} ({cold.Seconds:F1}s)");
            if (!readings.TryGetValue(target.Id, out var observed) || observed.Count == 0)
            {
                problems.Add($"{target.Id}: the {warmth} arm produced no reading");
                continue;
            }

            foreach (var operation in Operations)
            {
                var cell = CellId(target, warmth, operation);
                var expected = cold.Reading.Of(operation);
                var differing = observed.Where(r => !string.Equals(r.Of(operation), expected, StringComparison.Ordinal)).ToArray();
                var line = $"{cell}: {differing.Length}/{observed.Count} differ from cold [{expected}]"
                    + (differing.Length > 0 ? $"; e.g. [{differing[0].Of(operation)}]" : "");
                log.Add(line);
                if (KnownRedCells.TryGetValue(cell, out var row))
                {
                    if (!row.Probabilistic && differing.Length != observed.Count)
                        problems.Add($"{line} — the deterministic KnownRed row no longer reproduces ({row.Row}): stale, drain the row");
                }
                else if (differing.Length > 0)
                {
                    problems.Add($"{line} — a {warmth} instance must answer as the cold process (no KnownRed row)");
                }
            }
        }

        foreach (var entry in log)
            _output.WriteLine(entry);
        var reportDirectory = Path.Combine(FixtureRoots.RepositoryRoot, ".claude", "tmp", "warm-instance-parity");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(Path.Combine(reportDirectory, $"{warmth}.txt"), log);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Takes the masters' key sets and records the first divergence from <paramref name="previous"/> under <paramref name="label"/>.</summary>
    private static MasterRegistryKeySets Snapshot(CompilerApi api, MasterRegistryKeySets previous, string label, SCG.List<string> log)
    {
        var now = MasterRegistryKeySets.Take(api);
        log.Add($"keysets after {label}: {previous.FirstDivergence(now)}");
        return now;
    }

    private static string RunEnvironment()
    {
        var runner = Path.Combine(AppContext.BaseDirectory, "xunit.runner.json");
        var threads = File.Exists(runner)
            ? JsonNode.Parse(File.ReadAllText(runner))?["maxParallelThreads"]?.ToString() ?? "?"
            : "?";
        return $"processors={System.Environment.ProcessorCount} xunit maxParallelThreads={threads} dop={Dop} repetitions={Repetitions}";
    }

    // ================================================================
    // The sweep's earlier documents
    // ================================================================

    /// <summary>One step of the P47 ordering: serve (stem, twin), or measure a target.</summary>
    internal readonly record struct OrderStep(string Stem, string Twin, Target? Target);

    /// <summary>
    /// The documents the P47 sweep's shared instance served before each target, in its order (identity twin of every
    /// stem, crlf twin of every stem, then the cr twin in corpus order — a target is measured where the sweep reached
    /// it). Regular mode keeps the stems whose stable bucket under <see cref="PrefixStride"/> is 0, plus the targets'
    /// own stems; FULL mode keeps every stem.
    /// </summary>
    internal static SCG.IEnumerable<OrderStep> EarlierDocuments(bool full)
    {
        var targets = Targets.ToDictionary(t => t.Stem, StringComparer.Ordinal);
        var stems = SweepCorpus.Corpus.Value.Corpus.Keys
            .Where(s => full || targets.ContainsKey(s) || SweepCorpus.StableBucket(s, PrefixStride) == 0)
            .ToArray();
        foreach (var twin in new[] { CodeActionShapeSweepTests.Identity, CodeActionShapeSweepTests.Crlf })
        {
            foreach (var stem in stems)
                yield return new OrderStep(stem, twin, null);
        }

        // The cr sweep reached the targets in corpus order; nothing after the last one is an earlier document.
        var lastTarget = stems.Last(targets.ContainsKey);
        foreach (var stem in stems)
        {
            yield return targets.TryGetValue(stem, out var target)
                ? new OrderStep(stem, CodeActionShapeSweepTests.Cr, target)
                : new OrderStep(stem, CodeActionShapeSweepTests.Cr, null);
            if (stem == lastTarget)
                yield break;
        }
    }

    // ================================================================
    // In-process operations (the sweep's per-document work)
    // ================================================================

    private static readonly Lazy<string[]> s_references = new(StdlibReferences.Paths);

    /// <summary>A compiler with the server's default reference pair (Core + Stdlib), as <c>Program.cs</c> builds it.</summary>
    private static CompilerApi NewApi() => new(CompilerNullLogger.Instance, s_references.Value);

    /// <summary>The three operations on <paramref name="target"/> through <paramref name="driver"/>.</summary>
    private static Reading Measure(LspCodeActionDriver driver, Target target, string? coldApplied)
    {
        using var session = driver.Open(target.Text);
        var analyze = DiagFingerprint(session.Diagnostics);
        var extract = ExtractFingerprint(ExtractIn(session, target.Range));
        return new Reading(analyze, extract, AnalyzeIn(driver, coldApplied));
    }

    private static string? ExtractIn(LspCodeActionDriver.Session session, LspRange range)
    {
        var action = session.Request(range).FirstOrDefault(a => a.Title.StartsWith("Extract method", StringComparison.Ordinal));
        return action is null ? null : session.Apply(action);
    }

    private static string AnalyzeIn(LspCodeActionDriver driver, string? text)
    {
        if (text is null)
            return "no-extract";
        using var session = driver.Open(text);
        return DiagFingerprint(session.Diagnostics);
    }

    /// <summary>
    /// The P47 sweep's work on one (stem, twin) on the shared instance (<c>RunCells</c>): a non-identity twin first
    /// requests the identity twin's offers; then every request the AST admits, and (when <paramref name="applyAndAnalyze"/>)
    /// every offered action applied and every applied text that parses analysed, with the source's own path-less
    /// analysis (the <c>diagnostics</c> oracle).
    /// </summary>
    internal static void Serve(LspCodeActionDriver driver, CompilerApi api, string stem, string twin, bool applyAndAnalyze)
    {
        if (twin != CodeActionShapeSweepTests.Identity)
            Serve(driver, api, stem, CodeActionShapeSweepTests.Identity, applyAndAnalyze: false);
        var text = CodeActionShapeSweepTests.TwinOf(SweepCorpus.Corpus.Value.Corpus[stem].Source, twin);
        var doc = CodeActionShapeSweepTests.SweepText.Of(text);
        if (!doc.Parses || doc.UniformBreak is null)
            return;
        using var session = driver.Open(text);
        var seen = new SCG.HashSet<string>(StringComparer.Ordinal);
        foreach (var request in CodeActionShapeSweepTests.BuildRequests(doc, session.Diagnostics))
        {
            var context = request.Diagnostic is null ? Array.Empty<Diagnostic>() : new[] { request.Diagnostic };
            var actions = session.Request(request.Range, context);
            if (!applyAndAnalyze)
                continue;
            foreach (var action in actions)
            {
                string applied;
                try
                {
                    applied = LspFormattingDriver.ApplyStrict(text, LspCodeActionDriver.EditsOf(action, session.Uri));
                }
                catch (Exception ex) when (ex is InvalidOperationException or StrictEditApplicationException)
                {
                    continue;
                }

                if (seen.Add(applied) && SweepCorpus.Parses(applied))
                {
                    if (seen.Count == 1)
                        api.Analyze(text, CancellationToken.None);
                    api.Analyze(applied, CancellationToken.None);
                }
            }
        }
    }

    // ================================================================
    // Fingerprints
    // ================================================================

    /// <summary><c>success</c> (no error-severity diagnostic) and the sorted <c>code@line:col</c> (1-based) of published diagnostics.</summary>
    internal static string DiagFingerprint(SCG.IEnumerable<Diagnostic> diagnostics)
    {
        var list = diagnostics.ToArray();
        var rows = list.Select(d => $"{d.Code?.String ?? d.Code?.Long.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}@{d.Range.Start.Line + 1}:{d.Range.Start.Character + 1}")
            .OrderBy(s => s, StringComparer.Ordinal);
        return $"success={!list.Any(d => d.Severity == DiagnosticSeverity.Error)} [{string.Join(" ", rows)}]";
    }

    /// <summary>The applied text's first parse error (1-based line, column) — the P47 reading — and a digest of the text.</summary>
    internal static string ExtractFingerprint(string? applied)
    {
        if (applied is null)
            return "no-extract";
        var doc = CodeActionShapeSweepTests.SweepText.Of(applied);
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(applied)))[..12];
        return (doc.Parses ? "parses" : $"parse@({doc.FirstErrorLine},{doc.FirstErrorColumn})") + $" text={digest}";
    }

    // ================================================================
    // The cold oracle
    // ================================================================

    /// <summary>
    /// The cold reading of every target: a child <c>Sharpy.Lsp</c> process opens the cr twin (its first
    /// <c>publishDiagnostics</c> is <c>analyze</c>), answers the <c>textDocument/codeAction</c> request on the selection
    /// (Extract method's edits, applied strictly, are <c>extract</c>); a SECOND child process opens that applied text
    /// (<c>applied-analyze</c>) — a process that served one document only, each time. Targets run concurrently.
    /// </summary>
    public sealed class ColdOracle : IAsyncLifetime
    {
        private readonly ConcurrentDictionary<string, ColdReading> _readings = new(StringComparer.Ordinal);

        /// <summary>A target's cold reading, the cold applied text, and the seconds the two processes took.</summary>
        internal sealed record ColdReading(Reading Reading, string? AppliedText, double Seconds);

        internal ColdReading Of(Target target) => _readings.TryGetValue(target.Id, out var reading)
            ? reading
            : throw new InvalidOperationException($"no cold reading for {target.Id}");

        public async Task InitializeAsync()
        {
            await Task.WhenAll(Targets.Select(async target =>
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var (analyze, applied) = await ColdAsync(target.Text, target.Range);
                var appliedAnalyze = applied is null ? "no-extract" : (await ColdAsync(applied, null)).Diagnostics;
                _readings[target.Id] = new ColdReading(new Reading(analyze, ExtractFingerprint(applied), appliedAnalyze), applied, clock.Elapsed.TotalSeconds);
                if (applied is not null)
                {
                    var directory = Path.Combine(FixtureRoots.RepositoryRoot, ".claude", "tmp", "warm-instance-parity");
                    Directory.CreateDirectory(directory);
                    await File.WriteAllTextAsync(Path.Combine(directory, $"cold-applied-{target.Id.Replace('#', '.')}.spy"), applied);
                }
            }));
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private const string ColdUri = "file:///lsp-code-action-driver/doc1.spy";

    /// <summary>One cold process: open <paramref name="text"/>, read its first publication, optionally apply Extract method on <paramref name="range"/>.</summary>
    private static async Task<(string Diagnostics, string? Applied)> ColdAsync(string text, LspRange? range)
    {
        await using var client = LspTestClient.Start();
        await client.InitializeAsync();
        await client.DidOpenAsync(ColdUri, text);
        JsonNode? publication = null;
        while (publication is null)
        {
            var notification = await client.WaitForNotificationAsync("textDocument/publishDiagnostics");
            if (notification["uri"]?.GetValue<string>() == ColdUri)
                publication = notification;
        }

        var diagnostics = ColdDiagFingerprint(publication["diagnostics"]!.AsArray());
        string? applied = null;
        if (range is not null)
        {
            var result = await client.SendRequestAsync("textDocument/codeAction", new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = ColdUri },
                ["range"] = RangeJson(range),
                ["context"] = new JsonObject { ["diagnostics"] = new JsonArray() },
            });
            var action = result?.AsArray().FirstOrDefault(a => a?["title"]?.GetValue<string>().StartsWith("Extract method", StringComparison.Ordinal) == true);
            if (action is not null)
            {
                var edits = action["edit"]!["changes"]![ColdUri]!.AsArray().Select(e => new TextEdit
                {
                    Range = new LspRange(PositionOf(e!["range"]!["start"]!), PositionOf(e["range"]!["end"]!)),
                    NewText = e["newText"]!.GetValue<string>(),
                }).ToArray();
                applied = LspFormattingDriver.ApplyStrict(text, edits);
            }
        }

        try
        {
            await client.ShutdownAsync();
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or IOException)
        {
            // The reading is taken; a server that exits before answering shutdown is disposed (killed) below.
        }

        return (diagnostics, applied);
    }

    private static Position PositionOf(JsonNode node) => new(node["line"]!.GetValue<int>(), node["character"]!.GetValue<int>());

    private static JsonObject RangeJson(LspRange range) => new()
    {
        ["start"] = new JsonObject { ["line"] = range.Start.Line, ["character"] = range.Start.Character },
        ["end"] = new JsonObject { ["line"] = range.End.Line, ["character"] = range.End.Character },
    };

    /// <summary><see cref="DiagFingerprint"/> over the wire form of a publication.</summary>
    private static string ColdDiagFingerprint(JsonArray diagnostics)
    {
        var rows = diagnostics.Select(d => $"{d!["code"]?.ToString() ?? "-"}@{d["range"]!["start"]!["line"]!.GetValue<int>() + 1}:{d["range"]!["start"]!["character"]!.GetValue<int>() + 1}")
            .OrderBy(s => s, StringComparer.Ordinal);
        var success = !diagnostics.Any(d => d!["severity"]?.GetValue<int>() == (int)DiagnosticSeverity.Error);
        return $"success={success} [{string.Join(" ", rows)}]";
    }
}
