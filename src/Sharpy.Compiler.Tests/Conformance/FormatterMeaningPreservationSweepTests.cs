using FluentAssertions;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Pretty;
using Sharpy.Compiler.Text;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using SModule = Sharpy.Compiler.Parser.Ast.Module;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The instrument of P22b (#2062 #2068 #2077 #2157): <b><c>sharpyc format</c> never rewrites bytes
/// inside a string token and never drops a comment or an escape — <c>Format(P)</c> re-parses to an
/// AST structurally equal to <c>P</c>'s, and the comment sequence and the escaped-name multiset of
/// <c>P</c> equal those of <c>Format(P)</c>.</b>
///
/// <para>Corpus: every single-file fixture under <c>TestFixtures/</c> — every sidecar, <c>.skip</c>
/// and <c>Formatting/</c> included, since all oracles are parse-level — that lexes and parses clean
/// the way <see cref="FormatterService.Format"/> reads it (trivia lexer, no feature flags).
/// Multi-file fixtures and sources that do not parse are excluded and counted by
/// <see cref="Census_CorpusExclusionsAndInjectionKindsAreCountedAndStated"/>.</para>
///
/// <para>Each fixture P yields four twins Q (<see cref="FormatterTwins"/>): <c>identity</c> (T0,
/// Q = P), <c>comment</c> (T1, a numbered <c># cN</c> at every comment anchor kind),
/// <c>backtick</c> (T2, every unescaped identifier escaped) and <c>trailing</c> (T3, a <c>  # cN</c>
/// after the last code token of every line ending at bracket depth 0 and nothing else — T1's
/// bracket comments write every bracketed statement verbatim, so only T3 meets a trailing comment
/// with every spelling the unparser rewrites). Instrument checks come first and fail as bucket
/// <c>instrument</c>, never as a skip: T1, T2 and T3 parse clean, T1 and T3 parse to P's AST, and the
/// injected count is reached (<c>comments(T1|T3) == comments(P) + injected</c>, T1 at least the
/// end-of-file comment; <c>escapes(T2) == escapes(P) + wrapped</c>). Then, with <c>F = Format(Q)</c>:
/// <list type="bullet">
/// <item>O1 <c>refused</c> — <c>Format(Q)</c> reports a diagnostic;</item>
/// <item>O2 <c>unparseable</c> — F does not re-lex/re-parse clean;</item>
/// <item>O3 <c>astChanged</c> — <see cref="AstNormalizer"/> + <see cref="StructuralEqualityComparer"/> inequality of Q and F;</item>
/// <item>O4 <c>commentDropped</c> — the comment SEQUENCE (source order, text) differs: dropped, added or reordered;</item>
/// <item>O4b <c>commentMoved</c> — same program, same comment sequence, but a comment's attachment
/// changed (<c>CommentAnchors</c>: inline or own-line, neighbouring code tokens, block and bracket depth — the
/// same rule the SPY0912 net applies, after its structural check);</item>
/// <item>O5 <c>escapeDropped</c> — the multiset of backtick-escaped identifier values differs;</item>
/// <item>O6 <c>notIdempotent</c> — <c>Format(F) != F</c>.</item>
/// </list>
/// O4 and O5 read the token stream, so they hold whatever the comparer compares.</para>
///
/// <para><b>The net does not mask the oracles.</b> Since P22b Phase 2, <c>Format</c> refuses (SPY0912)
/// an output that would change meaning and returns Q unchanged — on which O2–O6 hold trivially. So O1
/// reads the real <c>Format</c>, and O2–O6 read the RAW output
/// (<c>FormatterService.FormatUnchecked</c>, the net bypassed): a refused cell lists <c>refused</c>
/// AND the damage the net caught. The two must agree: a refusal of a raw output that passes O2–O5 is
/// <c>netOverRefuses</c>, an accepted raw output that fails one is <c>netMissed</c>.</para>
///
/// <para><b>Ratchet.</b> <c>Conformance/formatter-meaning-preservation-allowlist.txt</c> lists
/// <c>stem twin bucket # #issue reason</c> rows. Every failing bucket of a cell must be listed,
/// and a listed bucket that now holds fails too (drain on fix). Comment buckets cite #2077 and
/// escape buckets #2157, the class trackers; the allowlist is EMPTY at the end of P22b Phase 4.</para>
/// </summary>
public class FormatterMeaningPreservationSweepTests
{
    private const string AllowlistFileName = "formatter-meaning-preservation-allowlist.txt";
    private const string FormatRunsControl = "Formatting/blank_lines";

    internal const string Identity = "identity";
    internal const string Comment = "comment";
    internal const string Backtick = "backtick";
    internal const string Trailing = "trailing";

    internal const string Instrument = "instrument";
    internal const string Refused = "refused";
    internal const string Unparseable = "unparseable";
    internal const string AstChanged = "astChanged";
    internal const string CommentDropped = "commentDropped";
    internal const string CommentMoved = "commentMoved";
    internal const string EscapeDropped = "escapeDropped";
    internal const string NotIdempotent = "notIdempotent";
    internal const string NetOverRefuses = "netOverRefuses";
    internal const string NetMissed = "netMissed";

    /// <summary>The raw-output buckets the net (SPY0912) is meant to catch: any of them ⇔ <c>Format</c> refuses.</summary>
    private static readonly HashSet<string> NetCatches = new(StringComparer.Ordinal) { Unparseable, AstChanged, CommentDropped, CommentMoved, EscapeDropped };

    /// <summary>O7's bucket: its rows share the allowlist file and belong to <see cref="FormatterEmitInvarianceSweepTests"/>.</summary>
    internal const string EmitChanged = "emitChanged";

    private static readonly string[] Twins = { Identity, Comment, Backtick, Trailing };
    private static readonly string[] Buckets = { Instrument, Refused, Unparseable, AstChanged, CommentDropped, CommentMoved, EscapeDropped, NotIdempotent, NetOverRefuses, NetMissed };

    private static readonly string FixturesPathValue = FixtureRoots.CompilerTests.Path;

    private readonly ITestOutputHelper _output;

    public FormatterMeaningPreservationSweepTests(ITestOutputHelper output) => _output = output;

    // ================================================================
    // Corpus
    // ================================================================

    private sealed record Fixture(string Name, string Source, bool IsSkipped);

    private sealed record Census(
        int Discovered, int MultiFile, int SingleFile, int Skipped, IReadOnlyList<string> Unparseable,
        IReadOnlyDictionary<string, Fixture> Corpus);

    private static readonly Lazy<Census> CorpusCensus = new(BuildCensus);

    private static Census BuildCensus()
    {
        var all = FixtureDiscoveryHelper.DiscoverFixtures(FixturesPathValue, includeSkipped: true).ToList();
        var single = all.Where(f => !f.IsMultiFile).ToList();
        var corpus = new SortedDictionary<string, Fixture>(StringComparer.Ordinal);
        var unparseable = new List<string>();
        foreach (var fixture in single)
        {
            var source = File.ReadAllText(fixture.SpyFilePath);
            if (Observe(source).HasErrors)
                unparseable.Add(fixture.TestName);
            else
                corpus[fixture.TestName] = new Fixture(fixture.TestName, source, fixture.IsSkipped);
        }

        return new Census(all.Count, all.Count(f => f.IsMultiFile), single.Count, single.Count(f => f.IsSkipped),
            unparseable, corpus);
    }

    public static IEnumerable<object[]> CorpusNames()
        => CorpusCensus.Value.Corpus.Keys.Select(name => new object[] { name });

    // ================================================================
    // The three theories
    // ================================================================

    [Theory]
    [MemberData(nameof(CorpusNames))]
    public void Identity_PreservesMeaning(string stem) => Sweep(stem, Identity);

    [Theory]
    [MemberData(nameof(CorpusNames))]
    public void CommentInjected_PreservesMeaning(string stem) => Sweep(stem, Comment);

    [Theory]
    [MemberData(nameof(CorpusNames))]
    public void BacktickInjected_PreservesMeaning(string stem) => Sweep(stem, Backtick);

    [Theory]
    [MemberData(nameof(CorpusNames))]
    public void LineTrailingInjected_PreservesMeaning(string stem) => Sweep(stem, Trailing);

    private void Sweep(string stem, string twin)
    {
        var verdict = Evaluate(CorpusCensus.Value.Corpus[stem].Source, twin);
        var listed = AllowlistByCell.Value[(stem, twin)]
            .ToDictionary(row => row.Bucket, row => row.Cite, StringComparer.Ordinal);

        _output.WriteLine($"FMTPRES {stem} {twin} {(verdict.Failures.Count == 0 ? "ok" : string.Join(",", verdict.Failures.Keys))}");
        foreach (var bucket in verdict.Failures.Keys)
            _output.WriteLine($"FMTPRES-ROW {stem} {twin} {bucket}");

        var problems = new List<string>();
        foreach (var (bucket, detail) in verdict.Failures)
        {
            if (!listed.ContainsKey(bucket))
                problems.Add($"{bucket}: {detail}");
        }

        foreach (var (bucket, cite) in listed)
        {
            if (!verdict.Failures.ContainsKey(bucket))
                problems.Add($"{bucket} holds now — {cite} is fixed for this cell: drain: delete the row `{stem} {twin} {bucket}` from Conformance/{AllowlistFileName}");
        }

        if (problems.Count > 0)
        {
            Assert.Fail($"{stem} ({twin} twin): Format must preserve meaning, comments and escapes.\n  "
                + string.Join("\n  ", problems)
                + (verdict.Formatted is { } formatted ? $"\n--- raw Format(Q), net bypassed ---\n{Clip(formatted)}" : ""));
        }
    }

    // ================================================================
    // Oracles
    // ================================================================

    internal sealed record Verdict(SortedDictionary<string, string> Failures, string? Formatted);

    /// <summary>The source observed once through the trivia lexer and the parser.</summary>
    internal sealed record Observation(bool HasErrors, string FirstError, SModule? Module, IReadOnlyList<string> Comments, IReadOnlyList<string> EscapedNames, List<CommentAnchors.Anchor> Anchors);

    internal static Observation Observe(string text)
    {
        var lex = FileCompilationPipeline.Lex(new SourceText(text, "<sweep>"), NullLogger.Instance, preserveTrivia: true);
        var comments = lex.Tokens
            .SelectMany(t => (t.LeadingTrivia ?? Array.Empty<Trivia>()).Concat(t.TrailingTrivia ?? Array.Empty<Trivia>()))
            .Where(t => t.Kind == TriviaKind.Comment)
            .Select(t => FormatterTwins.CommentText(t))
            .ToList();
        var escaped = lex.Tokens
            .Where(t => t.Type == TokenType.Identifier && t.IsBacktickEscaped)
            .Select(t => t.Value)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();
        var anchors = CommentAnchors.Of(lex.Tokens);
        if (lex.HasErrors)
            return new Observation(true, FirstError(lex.Diagnostics.GetAll()), null, comments, escaped, anchors);
        var parse = FileCompilationPipeline.Parse(lex.Tokens, NullLogger.Instance);
        return new Observation(parse.HasErrors || parse.Module == null, FirstError(parse.Diagnostics.GetAll()), parse.Module, comments, escaped, anchors);
    }

    /// <summary>Builds the twin of <paramref name="source"/>, runs the instrument checks, formats it and applies O1–O6.</summary>
    internal static Verdict Evaluate(string source, string twin)
    {
        var failures = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var original = Observe(source);
        string q;
        switch (twin)
        {
            case Identity:
                q = source;
                break;
            case Comment or Trailing:
                {
                    (q, var counts) = twin == Comment ? FormatterTwins.CommentInjected(source) : FormatterTwins.LineTrailingInjected(source);
                    var twinObs = Observe(q);
                    if (twin == Comment && counts[InjectedCommentKind.EndOfFile] != 1)
                        failures[Instrument] = $"the injector did not run (counts: {counts})";
                    else if (twinObs.HasErrors)
                        failures[Instrument] = $"the {twin} twin does not parse: {twinObs.FirstError}";
                    else if (!SameAst(original.Module!, twinObs.Module!))
                        failures[Instrument] = $"the {twin} twin parses to a different AST";
                    else if (twinObs.Comments.Count != original.Comments.Count + counts.Total)
                        failures[Instrument] = $"the {twin} twin has {twinObs.Comments.Count} comments, expected {original.Comments.Count} + {counts.Total} injected";
                    break;
                }

            case Backtick:
                {
                    (q, var wrapped) = FormatterTwins.BacktickInjected(source);
                    var twinObs = Observe(q);
                    if (twinObs.HasErrors)
                        failures[Instrument] = $"the backtick twin does not parse: {twinObs.FirstError}";
                    else if (twinObs.EscapedNames.Count != original.EscapedNames.Count + wrapped)
                        failures[Instrument] = $"the backtick twin has {twinObs.EscapedNames.Count} escaped names, expected {original.EscapedNames.Count} + {wrapped} wrapped";
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(twin), twin, null);
        }

        if (failures.Count > 0)
            return new Verdict(failures, null);

        // O1 on the real formatter; O2–O6 on the RAW output (the net bypassed) — a refusal returns Q
        // unchanged, which would make every damage oracle pass trivially and hide what the net caught.
        var result = Format(q);
        var refused = result.Diagnostics.Count > 0;
        if (refused)
            failures[Refused] = FirstError(result.Diagnostics);
        var raw = FormatRaw(q).FormattedText;
        foreach (var (bucket, detail) in Oracles(Observe(q), raw))
            failures[bucket] = detail;

        // The net's verdict agrees with the damage oracles in both directions.
        var damaged = failures.Keys.Where(NetCatches.Contains).ToList();
        if (refused && damaged.Count == 0)
            failures[NetOverRefuses] = $"Format refused ({failures[Refused]}) an output that passes O2–O5";
        if (!refused && damaged.Count > 0)
            failures[NetMissed] = $"Format accepted an output that fails {string.Join(", ", damaged)}";
        return new Verdict(failures, raw);
    }

    /// <summary>O2–O6 of a twin's observation against a formatted (raw) text; idempotence is the raw formatter's.</summary>
    internal static IEnumerable<(string Bucket, string Detail)> Oracles(Observation q, string formatted)
    {
        var f = Observe(formatted);
        var sameProgram = false;
        if (f.HasErrors)
        {
            yield return (Unparseable, f.FirstError);
        }
        else
        {
            sameProgram = SameAst(q.Module!, f.Module!);
            if (!sameProgram)
                yield return (AstChanged, "the formatted text parses to a structurally different AST");
            var again = FormatRaw(formatted).FormattedText;
            if (again != formatted)
                yield return (NotIdempotent, FirstDifference(formatted, again));
        }

        if (!q.Comments.SequenceEqual(f.Comments, StringComparer.Ordinal))
            yield return (CommentDropped, SequenceDifference(q.Comments, f.Comments));
        else if (sameProgram && CommentAnchors.FirstMoved(q.Anchors, f.Anchors) is { } moved)
            yield return (CommentMoved, $"'{moved.Before.Text}' (line {moved.Before.Line}) moved {CommentAnchors.Describe(moved.Before, moved.After)}");
        if (!q.EscapedNames.SequenceEqual(f.EscapedNames, StringComparer.Ordinal))
            yield return (EscapeDropped, SequenceDifference(q.EscapedNames, f.EscapedNames));
    }

    /// <summary>The formatter under test — <c>sharpyc format</c>'s and the LSP's one entry point.</summary>
    private static FormatterResult Format(string text) => FormatterService.Format(text);

    /// <summary>The same formatter with the SPY0912 net bypassed — the text <see cref="Format"/> writes when the net accepts it.</summary>
    private static FormatterResult FormatRaw(string text) => FormatterService.FormatUnchecked(text);

    private static bool SameAst(SModule a, SModule b)
        => StructuralEqualityComparer.Instance.Equals(
            AstNormalizer.Instance.NormalizeModule(a),
            AstNormalizer.Instance.NormalizeModule(b));

    // ================================================================
    // Controls and census
    // ================================================================

    /// <summary>
    /// <c>Format</c> runs: the <c>blank_lines</c> formatting fixture has a known layout change, so an
    /// identity formatter (which would make O3 and O6 trivially green) is visible.
    /// </summary>
    [Fact]
    public void PositiveControl_FormatRuns_TheBlankLinesFixtureChanges()
    {
        var source = CorpusCensus.Value.Corpus[FormatRunsControl].Source;
        var result = Format(source);

        result.Diagnostics.Should().BeEmpty();
        result.FormattedText.Should().NotBe(source);
        result.HasChanges.Should().BeTrue();
    }

    /// <summary>
    /// The bypass is real: on a program the net refuses, <c>Format</c> returns the source with
    /// SPY0912 while the raw output carries the damage — and the sweep's verdict carries both
    /// <c>refused</c> and the damage bucket, never <c>netMissed</c>/<c>netOverRefuses</c>. The damaged
    /// shape is an escaped contextual keyword the parser reads as the keyword (<c>case `_`:</c> is
    /// written <c>case _:</c>, #2166, outside P22b); until P22b Phase 4 it was a dropped bracket
    /// comment, which the trivia cursor now keeps.
    /// </summary>
    [Fact]
    public void PositiveControl_TheBypassSeesTheDamageTheNetRefuses()
    {
        const string source = "def main():\n    x = 1\n    match x:\n        case `_`:\n            print(x)\n";

        var netted = Format(source);
        netted.Diagnostics.Select(d => d.Code).Should().Equal(global::Sharpy.Compiler.Diagnostics.DiagnosticCodes.Infrastructure.FormatterDeclined);
        netted.FormattedText.Should().Be(source);
        var raw = FormatRaw(source);
        raw.Diagnostics.Should().BeEmpty();
        raw.FormattedText.Should().NotContain("`_`");

        Evaluate(source, Identity).Failures.Keys.Should().Equal(EscapeDropped, Refused);
    }

    /// <summary>
    /// The injector runs and O4 sees a dropped comment: a T1 twin carries injected comments; the
    /// oracles applied to the twin's own text report no comment change, and applied to that text
    /// with one injected comment line removed report <c>commentDropped</c>.
    /// </summary>
    [Fact]
    public void PositiveControl_InjectorRuns_AndO4SeesARemovedComment()
    {
        const string source = "def main():\n    if True:\n        print(1)\n";
        var (twin, counts) = FormatterTwins.CommentInjected(source);
        counts.Total.Should().Be(13);
        var q = Observe(twin);
        Oracles(q, twin).Select(o => o.Bucket).Should().NotContain(CommentDropped);

        var lines = twin.Split('\n').ToList();
        lines.RemoveAt(lines.FindIndex(l => l.Trim() == "# c5"));
        Oracles(q, string.Join("\n", lines)).Select(o => o.Bucket).Should().Contain(CommentDropped);
    }

    /// <summary>
    /// O4b sees a comment that MOVED with the sequence intact (the else-header comment written on the
    /// <c>if</c> line), and does not see a pure re-indent of the same program (the exemption's positive
    /// control: layout is not attachment).
    /// </summary>
    [Fact]
    public void PositiveControl_O4bSeesAMovedComment_NotAReindent()
    {
        var q = Observe("def main():\n        x = 0\n        if x > 0:\n                print(1)\n        else:  # c\n                print(2)\n");

        Oracles(q, "def main():\n    x = 0\n    if x > 0:  # c\n        print(1)\n    else:\n        print(2)\n")
            .Select(o => o.Bucket).Should().Contain(CommentMoved).And.NotContain(CommentDropped);
        Oracles(q, "def main():\n    x = 0\n    if x > 0:\n        print(1)\n    else:  # c\n        print(2)\n")
            .Select(o => o.Bucket).Should().NotContain(CommentMoved);
    }

    /// <summary>O5 sees a dropped escape, O2 an unparseable output and O3 a changed program, each on a hand-damaged formatted text.</summary>
    [Fact]
    public void PositiveControl_OraclesSeeADroppedEscapeAChangedProgramAndAnUnparseableOutput()
    {
        var q = Observe("def f(`x`: int) -> int:\n    return `x`\n");

        Oracles(q, "def f(`x`: int) -> int:\n    return `x`\n").Should().BeEmpty();
        Oracles(q, "def f(`x`: int) -> int:\n    return x\n").Select(o => o.Bucket).Should().Contain(EscapeDropped);
        Oracles(q, "def f(`x`: int) -> int:\n    return `x` + 1\n").Select(o => o.Bucket).Should().Equal(AstChanged);
        Oracles(q, "def f(`x`: int) -> int:\n    return (`x`\n").Select(o => o.Bucket).Should().Contain(Unparseable);
    }

    /// <summary>
    /// Every discovered fixture is in the corpus or excluded for a stated reason; every injection
    /// kind fires somewhere in the corpus and T2 escapes identifiers; the allowlist names corpus
    /// stems, known twins and known buckets only. The counts are printed for the record.
    /// </summary>
    [Fact]
    public void Census_CorpusExclusionsAndInjectionKindsAreCountedAndStated()
    {
        var census = CorpusCensus.Value;
        var memberFiles = CountMultiFileMemberFiles();
        _output.WriteLine($"FMTPRES-CENSUS discovered={census.Discovered} single-file={census.SingleFile} "
            + $"corpus={census.Corpus.Count} skipped-included={census.Corpus.Values.Count(f => f.IsSkipped)} "
            + $"formatting-included={census.Corpus.Keys.Count(k => k.StartsWith("Formatting/", StringComparison.Ordinal))} "
            + $"excluded-multi-file={census.MultiFile} (member .spy files={memberFiles}) "
            + $"excluded-unparseable={census.Unparseable.Count}");
        foreach (var name in census.Unparseable)
            _output.WriteLine($"FMTPRES-CENSUS unparseable {name}");

        var totals = new int[InjectedCommentCounts.Kinds.Count];
        var identifiers = 0;
        var trailing = 0;
        var trailingNone = 0;
        var skipped = FormatterTwins.ContextualKeywordsReadAsKeywords.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
        foreach (var fixture in census.Corpus.Values)
        {
            var (_, counts) = FormatterTwins.CommentInjected(fixture.Source);
            foreach (var kind in InjectedCommentCounts.Kinds)
                totals[(int)kind] += counts[kind];
            identifiers += FormatterTwins.BacktickInjected(fixture.Source).Count;
            var (_, trailingCounts) = FormatterTwins.LineTrailingInjected(fixture.Source);
            trailing += trailingCounts.Total;
            trailingNone += trailingCounts.Total == 0 ? 1 : 0;
            foreach (var (value, n) in FormatterTwins.ContextualKeywordSkips(fixture.Source))
                skipped[value] += n;
        }

        _output.WriteLine("FMTPRES-CENSUS T1 " + string.Join(" ", InjectedCommentCounts.Kinds.Select(k => $"{k}={totals[(int)k]}")));
        _output.WriteLine($"FMTPRES-CENSUS T2 identifiers={identifiers}");
        _output.WriteLine($"FMTPRES-CENSUS T3 LineTrailing={trailing} (fixtures with none={trailingNone})");
        _output.WriteLine("FMTPRES-CENSUS T2 skipped-contextual-keywords(#2166) "
            + string.Join(" ", skipped.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}")));

        var rows = LoadAllowlist();
        foreach (var twin in Twins)
        {
            _output.WriteLine($"FMTPRES-CENSUS allowlist {twin} " + string.Join(" ",
                Buckets.Append(EmitChanged).Select(b => $"{b}={rows.Count(r => r.Twin == twin && r.Bucket == b)}")));
        }

        census.Corpus.Count.Should().Be(census.SingleFile - census.Unparseable.Count);
        census.Corpus.Should().ContainKey(FormatRunsControl);
        census.Corpus.Values.Should().Contain(f => f.IsSkipped, "the .skip fixtures are in the parse-level corpus");
        foreach (var kind in InjectedCommentCounts.Kinds)
            totals[(int)kind].Should().BeGreaterThanOrEqualTo(1, $"injection kind {kind} must fire somewhere in the corpus");
        identifiers.Should().BeGreaterThanOrEqualTo(1);
        trailing.Should().BeGreaterThanOrEqualTo(totals[(int)InjectedCommentKind.LineTrailing],
            "T3 injects the line-trailing kind alone, so every slot T1 gives it is T3's too (plus the clause-colon slots T1 takes)");
        foreach (var (value, n) in skipped)
            n.Should().BeGreaterThan(0, $"T2 skips the #2166 contextual keyword '{value}': the corpus must contain one, else the skip set has a stale member");
        rows.Should().OnlyContain(r => census.Corpus.ContainsKey(r.Stem), "every allowlist row names a corpus fixture");

        // P22b drained the allowlist to EMPTY at Phase 4 Task 3 (every comment, escape and O7 row). The
        // literal anchors it: re-populating the allowlist is a visible decision — change this 0 in the
        // same commit and say why.
        rows.Count.Should().Be(0, "the formatter meaning-preservation allowlist is empty since P22b Phase 4 Task 3");
    }

    private static int CountMultiFileMemberFiles()
        => FixtureDiscoveryHelper.DiscoverFixtures(FixturesPathValue, includeSkipped: true)
            .Where(f => f.IsMultiFile)
            .Sum(f => Directory.EnumerateFiles(f.SpyFilePath, "*.spy", SearchOption.AllDirectories)
                .Count(p => !global::Sharpy.Compiler.Diagnostics.CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(FixturesPathValue, p))));

    // ================================================================
    // Allowlist and reporting
    // ================================================================

    internal sealed record Row(string Stem, string Twin, string Bucket, string Cite);

    private static readonly Lazy<IReadOnlyList<Row>> Allowlist = new(ReadAllowlist);

    private static readonly Lazy<ILookup<(string Stem, string Twin), Row>> AllowlistByCell =
        new(() => Allowlist.Value.Where(row => row.Bucket != EmitChanged).ToLookup(row => (row.Stem, row.Twin)));

    private static IReadOnlyList<Row> LoadAllowlist() => Allowlist.Value;

    /// <summary><c>stem twin bucket # #issue reason</c>; <c>#</c> starts the cite only after whitespace.</summary>
    private static IReadOnlyList<Row> ReadAllowlist()
    {
        var path = FindAllowlistPath()
            ?? throw new InvalidOperationException(
                $"Conformance/{AllowlistFileName} is missing. Its presence is what arms the ratchet.");
        var rows = new List<Row>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var split = line.IndexOf(" #", StringComparison.Ordinal);
            var fields = (split < 0 ? line : line.Substring(0, split)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cite = split < 0 ? "" : line.Substring(split + 2).Trim();
            if (fields.Length != 3 || !Twins.Contains(fields[1]) || !(Buckets.Contains(fields[2]) || fields[2] == EmitChanged))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' must read `stem twin bucket # #issue reason` with twin in {{{string.Join(", ", Twins)}}} and bucket in {{{string.Join(", ", Buckets)}}}.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(cite, @"^#\d+\b"))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' must cite an issue first (# #N reason).");
            if (!keys.Add($"{fields[0]} {fields[1]} {fields[2]}"))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' is listed twice.");
            rows.Add(new Row(fields[0], fields[1], fields[2], cite));
        }

        return rows;
    }

    private static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
            if (Directory.Exists(dir))
            {
                var path = Path.Combine(dir, AllowlistFileName);
                return File.Exists(path) ? path : null;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }

    private static string FirstError(IEnumerable<global::Sharpy.Compiler.Diagnostics.CompilerDiagnostic> diagnostics)
        => diagnostics.Where(d => d.Severity == global::Sharpy.Compiler.Diagnostics.CompilerDiagnosticSeverity.Error)
            .Select(d => $"{d.Code} L{d.Line}:{d.Column} {d.Message}")
            .FirstOrDefault() ?? "";

    private static string SequenceDifference(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        var missing = expected.GroupBy(x => x).SelectMany(g => Enumerable.Repeat(g.Key, Math.Max(0, g.Count() - actual.Count(a => a == g.Key)))).ToList();
        var extra = actual.GroupBy(x => x).SelectMany(g => Enumerable.Repeat(g.Key, Math.Max(0, g.Count() - expected.Count(e => e == g.Key)))).ToList();
        var first = Enumerable.Range(0, Math.Min(expected.Count, actual.Count)).FirstOrDefault(i => expected[i] != actual[i], -1);
        return $"{expected.Count} → {actual.Count}; missing [{string.Join(", ", missing.Take(8))}]; extra [{string.Join(", ", extra.Take(8))}]"
            + (missing.Count == 0 && extra.Count == 0 && first >= 0 ? $"; reordered from position {first} ('{expected[first]}' vs '{actual[first]}')" : "");
    }

    private static string FirstDifference(string a, string b)
    {
        var x = a.Split('\n');
        var y = b.Split('\n');
        for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            var l = i < x.Length ? x[i] : "<end>";
            var r = i < y.Length ? y[i] : "<end>";
            if (l != r)
                return $"Format(F) differs from F at line {i + 1}: '{l}' vs '{r}'";
        }

        return "Format(F) differs from F";
    }

    private static string Clip(string text) => text.Length > 4000 ? text[..4000] + "\n…" : text;
}
