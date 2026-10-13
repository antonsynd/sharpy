using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Pretty;
using Sharpy.Compiler.Text;
using Sharpy.TestInfrastructure.Formatting;
using Xunit;
using Xunit.Abstractions;
using SModule = Sharpy.Compiler.Parser.Ast.Module;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The instrument of P50 (R-ET, #2239): <b>a blank-line run the lexer records between two anchors is
/// written by <c>sharpyc format</c> as exactly ONE blank line, except at a body start, the module
/// start, after a decorator line and at end of file, where it is written as none; canonical spacing is
/// a minimum the written count never exceeds.</b>
///
/// <para>Corpus: <see cref="FormatterMeaningPreservationSweepTests.CorpusCensus"/> (every single-file
/// fixture that lexes and parses clean, <c>.skip</c> and <c>Formatting/</c> included). Twins:
/// <c>identity</c> (Q = P) and <c>comment</c> (<see cref="FormatterTwins.CommentInjected"/>, a comment
/// at every anchor kind — the before/after-comment axis of every run).</para>
///
/// <para><b>The oracle is differential and model-free.</b> Q₀ = Q with every run's lines deleted, Q⁺ =
/// Q with two extra empty lines in every run; F, F₀, F⁺ are their formatted texts. Instrument checks
/// come first: Q₀ has no run and Q₀/Q⁺ parse to Q's AST with Q's comment sequence. The ITEMS of a text
/// are its logical lines (the token groups the lexer ends with a <c>Newline</c>) and its own-line
/// comments, in source order — not its tokens: the formatter respells tokens
/// (<c>except A, B:</c> → <c>except (A, B):</c>) and may add an item (the <c>...</c> body it writes for
/// a bodyless definition), never moves one. F and F₀ must have the same items (else
/// <c>instrument</c>); Q's items map onto F's by a longest common subsequence of their keys (first
/// token, or comment text), and every item a run precedes must map. <c>gapBefore(i)</c> is the number of
/// source lines between item i−1's last line and item i's first. Then: at an item a <c>Between</c>
/// run precedes in Q, <c>gap_F(i) == max(1, gap_F₀(i))</c>; at every other item,
/// <c>gap_F(i) == gap_F₀(i)</c>; F has no leading or trailing blank line; <c>F⁺ == F</c>; and
/// <c>Format(F) == F</c>. F₀ supplies the canonical spacing, so the oracle needs no model of it —
/// only the placement predicate (<see cref="Classify"/>, restated here on purpose: the production
/// predicate is the cursor's), whose bytes the hand fixture <c>Formatting/blank_line_placements</c>
/// pins per placement class.</para>
///
/// <para>Buckets: <c>instrument</c> (a twin does not parse or changes the program, Q₀ keeps a run,
/// F and F₀ differ, a run's item has no counterpart in F), <c>blankLost</c> (a <c>Between</c> run written as
/// no line — the defect R-ET ruled), <c>blankShape</c> (any other gap: a run invented, not collapsed,
/// a dropped placement kept, a leading or trailing blank line), <c>blankLength</c> (<c>F⁺ != F</c>:
/// the run's length leaks), <c>notIdempotent</c>. A cell whose Q, Q₀ or Q⁺ <c>Format</c> refuses is
/// counted, not judged — refusals are the meaning sweep's business.</para>
///
/// <para><b>Ratchet.</b> <c>Conformance/formatter-blank-line-allowlist.txt</c> holds rows
/// <c>stem twin bucket # #N reason</c> and pins <c>pin twin bucket count digest # #N reason</c> (the
/// failing stems of a (twin, bucket) key: their number and the first 16 hex characters of SHA-256 over
/// the sorted, <c>\n</c>-joined stems — the <c>code-action-shape-allowlist.txt</c> precedent). A failing
/// cell no row or pin covers is red; a row whose bucket holds now is red (drain on fix); a pin whose
/// count or digest differs is red in both directions.</para>
/// </summary>
public class FormatterBlankLineSweepTests
{
    private const string AllowlistFileName = "formatter-blank-line-allowlist.txt";
    internal const string PlacementFixture = "Formatting/blank_line_placements";

    internal const string Identity = "identity";
    internal const string Comment = "comment";

    internal const string Instrument = "instrument";
    internal const string BlankLost = "blankLost";
    internal const string BlankShape = "blankShape";
    internal const string BlankLength = "blankLength";
    internal const string NotIdempotent = "notIdempotent";

    private static readonly string[] Twins = { Identity, Comment };
    private static readonly string[] Buckets = { Instrument, BlankLost, BlankShape, BlankLength, NotIdempotent };

    private readonly ITestOutputHelper _output;

    public FormatterBlankLineSweepTests(ITestOutputHelper output) => _output = output;

    private static IReadOnlyDictionary<string, FormatterMeaningPreservationSweepTests.Fixture> Corpus
        => FormatterMeaningPreservationSweepTests.CorpusCensus.Value.Corpus;

    // ================================================================
    // The sweeps
    // ================================================================

    [Fact]
    public void Identity_UserBlankLinesSurviveFormatting() => Sweep(Identity);

    [Fact]
    public void CommentInjected_UserBlankLinesSurviveFormatting() => Sweep(Comment);

    private void Sweep(string twin)
    {
        var clock = Stopwatch.StartNew();
        var verdicts = new ConcurrentDictionary<string, Verdict>(StringComparer.Ordinal);
        Parallel.ForEach(Corpus.Keys, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            stem => verdicts[stem] = Evaluate(Corpus[stem].Source, twin));
        clock.Stop();

        var failing = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var (stem, verdict) in verdicts.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var state = verdict.Refused ? "refused" : verdict.Failures.Count == 0 ? "ok" : string.Join(",", verdict.Failures.Keys);
            _output.WriteLine($"FMTBLANK {stem} {twin} runs={verdict.Runs} {state}"
                + string.Concat(verdict.Failures.Select(kv => $" | {kv.Key}: {kv.Value}")));
            foreach (var bucket in verdict.Failures.Keys)
            {
                if (!failing.TryGetValue(bucket, out var stems))
                    failing[bucket] = stems = new SortedSet<string>(StringComparer.Ordinal);
                stems.Add(stem);
            }
        }

        _output.WriteLine($"FMTBLANK-CENSUS {twin} cells={verdicts.Count} with-runs={verdicts.Values.Count(v => v.Runs > 0)} "
            + $"refused={verdicts.Values.Count(v => v.Refused)} "
            + string.Join(" ", Buckets.Select(b => $"{b}={(failing.TryGetValue(b, out var s) ? s.Count : 0)}"))
            + $" wall={clock.ElapsedMilliseconds}ms");
        foreach (var (bucket, stems) in failing)
            _output.WriteLine($"FMTBLANK-PIN {twin} {bucket} {stems.Count} {Digest(stems)}");

        // A refused cell is skipped, not judged, so the count is anchored: a blank-line change that
        // drove Q, Q₀ or Q⁺ into SPY0912 would otherwise hide its cell behind the exemption
        // (contract §2; 0 at 4aca8ebfe, both twins, measured).
        verdicts.Values.Count(v => v.Refused).Should().Be(0,
            $"the {twin} twin's refused cells are census, not judgement — a new one is a formatter refusal to fix, never a cell to skip: "
            + string.Join(", ", verdicts.Where(kv => kv.Value.Refused).Select(kv => kv.Key).Take(10)));

        var problems = Compare(twin, failing, Allowlist.Value);
        if (problems.Count > 0)
        {
            var first = failing.Values.SelectMany(s => s).FirstOrDefault(stem => verdicts[stem].Failures.Count > 0);
            Assert.Fail($"{twin} twin: a user blank line between statements must survive formatting as one line.\n  "
                + string.Join("\n  ", problems.Take(40))
                + (first != null ? $"\n--- first failing cell {first}: {string.Join("; ", verdicts[first].Failures.Select(kv => $"{kv.Key}: {kv.Value}"))}" : ""));
        }
    }

    /// <summary>The ratchet: every failing (stem, bucket) is covered by a row or by its key's pin, and every row and pin is exact.</summary>
    internal static List<string> Compare(string twin, IReadOnlyDictionary<string, SortedSet<string>> failing, IReadOnlyList<AllowlistEntry> allowlist)
    {
        var problems = new List<string>();
        var pins = allowlist.Where(e => e.IsPin && e.Twin == twin).ToDictionary(e => e.Bucket, StringComparer.Ordinal);
        var rows = allowlist.Where(e => !e.IsPin && e.Twin == twin).ToList();
        foreach (var bucket in Buckets)
        {
            var stems = failing.TryGetValue(bucket, out var s) ? s : new SortedSet<string>(StringComparer.Ordinal);
            if (pins.TryGetValue(bucket, out var pin))
            {
                var digest = Digest(stems);
                if (pin.Count != stems.Count || pin.Digest != digest)
                {
                    problems.Add($"pin {twin} {bucket}: pinned count={pin.Count} digest={pin.Digest}, actual count={stems.Count} digest={digest}"
                        + (stems.Count == 0 ? $" — every cell holds now: drain the pin from Conformance/{AllowlistFileName}" : $": {string.Join(", ", stems.Take(5))}"));
                }
                continue;
            }

            foreach (var stem in stems)
            {
                if (!rows.Any(r => r.Stem == stem && r.Bucket == bucket))
                    problems.Add($"{stem} {twin} {bucket}: fails and is not listed");
            }
        }

        foreach (var row in rows)
        {
            if (!failing.TryGetValue(row.Bucket, out var stems) || !stems.Contains(row.Stem))
                problems.Add($"{row.Stem} {twin} {row.Bucket} holds now — {row.Cite} is fixed for this cell: drain: delete the row from Conformance/{AllowlistFileName}");
        }

        return problems;
    }

    // ================================================================
    // One cell
    // ================================================================

    internal sealed record Verdict(SortedDictionary<string, string> Failures, bool Refused, int Runs);

    internal static Verdict Evaluate(string source, string twin)
    {
        var failures = new SortedDictionary<string, string>(StringComparer.Ordinal);
        string q;
        switch (twin)
        {
            case Identity:
                q = source;
                break;
            case Comment:
                {
                    var original = FormatterMeaningPreservationSweepTests.Observe(source);
                    (q, var counts) = FormatterTwins.CommentInjected(source);
                    var twinObs = FormatterMeaningPreservationSweepTests.Observe(q);
                    if (counts[InjectedCommentKind.EndOfFile] != 1)
                        failures[Instrument] = $"the injector did not run (counts: {counts})";
                    else if (twinObs.HasErrors)
                        failures[Instrument] = $"the comment twin does not parse: {twinObs.FirstError}";
                    else if (!SameAst(original.Module!, twinObs.Module!))
                        failures[Instrument] = "the comment twin parses to a different AST";
                    else if (twinObs.Comments.Count != original.Comments.Count + counts.Total)
                        failures[Instrument] = $"the comment twin has {twinObs.Comments.Count} comments, expected {original.Comments.Count} + {counts.Total} injected";
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(twin), twin, null);
        }

        if (failures.Count > 0)
            return new Verdict(failures, false, 0);

        var runs = Runs(Lex(q));
        if (runs.Count == 0)
        {
            // No run: Q₀ = Q⁺ = Q, so the gap oracle holds by construction; idempotence is still judged.
            var plain = FormatterService.Format(q);
            if (plain.Diagnostics.Count > 0)
                return new Verdict(failures, true, 0);
            if (FormatterService.Format(plain.FormattedText).FormattedText != plain.FormattedText)
                failures[NotIdempotent] = "Format(F) != F";
            return new Verdict(failures, false, 0);
        }

        var q0 = WithoutRuns(q, runs);
        var qPlus = WithLongerRuns(q, runs);
        var instrument = InstrumentProblem(q, runs, q0, qPlus);
        if (instrument != null)
        {
            failures[Instrument] = instrument;
            return new Verdict(failures, false, runs.Count);
        }

        var f = FormatterService.Format(q);
        var f0 = FormatterService.Format(q0);
        var fPlus = FormatterService.Format(qPlus);
        if (f.Diagnostics.Count > 0 || f0.Diagnostics.Count > 0 || fPlus.Diagnostics.Count > 0)
            return new Verdict(failures, true, runs.Count);

        var again = FormatterService.Format(f.FormattedText).FormattedText;
        foreach (var (bucket, detail) in Judge(q, f.FormattedText, f0.FormattedText, fPlus.FormattedText, again))
            failures.TryAdd(bucket, detail);
        return new Verdict(failures, false, runs.Count);
    }

    private static string? InstrumentProblem(string q, IReadOnlyList<Run> runs, string q0, string qPlus)
    {
        var obs = FormatterMeaningPreservationSweepTests.Observe(q);
        var obs0 = FormatterMeaningPreservationSweepTests.Observe(q0);
        var obsPlus = FormatterMeaningPreservationSweepTests.Observe(qPlus);
        if (obs0.HasErrors)
            return $"Q₀ (runs deleted) does not parse: {obs0.FirstError}";
        if (obsPlus.HasErrors)
            return $"Q⁺ (runs lengthened) does not parse: {obsPlus.FirstError}";
        if (Runs(Lex(q0)).Count != 0)
            return "Q₀ still has a blank-line run";
        if (Runs(Lex(qPlus)).Count != runs.Count)
            return "Q⁺ has a different number of runs";
        if (!SameAst(obs.Module!, obs0.Module!) || !SameAst(obs.Module!, obsPlus.Module!))
            return "Q₀ or Q⁺ parses to a different AST";
        if (!obs.Comments.SequenceEqual(obs0.Comments, StringComparer.Ordinal) || !obs.Comments.SequenceEqual(obsPlus.Comments, StringComparer.Ordinal))
            return "Q₀ or Q⁺ has a different comment sequence";
        return null;
    }

    /// <summary>
    /// The gap oracle on one cell's texts: Q (the source of the runs), F = Format(Q), F₀ = Format(Q₀),
    /// F⁺ = Format(Q⁺) and <paramref name="fAgain"/> = Format(F).
    /// </summary>
    internal static IEnumerable<(string Bucket, string Detail)> Judge(string q, string f, string f0, string fPlus, string fAgain)
    {
        var qItems = Items(q);
        var fItems = Items(f);
        var f0Items = Items(f0);
        if (!fItems.Select(i => i.Key).SequenceEqual(f0Items.Select(i => i.Key), StringComparer.Ordinal))
        {
            yield return (Instrument, $"F and F₀ have different items ({fItems.Count} vs {f0Items.Count}): the run-free twin is not the same layout");
            yield break;
        }

        // Q's items map onto F's by their keys: the formatter may add an item (the `...` body of a
        // bodyless definition) but never moves one, so a longest common subsequence is the alignment.
        var toF = Align(qItems, fItems);
        var qRuns = Runs(Lex(q));
        var between = new HashSet<int>();
        foreach (var run in qRuns.Where(r => r.Placement == Placement.Between))
        {
            var index = qItems.FindIndex(item => item.StartLine == run.Trivia.Line);
            if (index < 0 || toF[index] < 0)
            {
                yield return (Instrument, index < 0
                    ? $"the run ending at line {run.Trivia.Line} precedes no item"
                    : $"the item after the run ending at line {run.Trivia.Line} ('{qItems[index].Key}') has no counterpart in F");
                yield break;
            }
            between.Add(toF[index]);
        }

        // The detached-comment rule (owner ruling 2026-10-13): where it applies, the gap is restated
        // here — the run-free twin F₀ attaches every comment to the definition, so it cannot supply it.
        var overrides = new Dictionary<int, int>();
        foreach (var (qIndex, gap) in DetachedCommentGaps(qItems, qRuns))
        {
            if (toF[qIndex] < 0)
            {
                yield return (Instrument, $"the item at Q line {qItems[qIndex].StartLine} ('{qItems[qIndex].Key}') has no counterpart in F");
                yield break;
            }
            overrides[toF[qIndex]] = gap;
        }

        var fGaps = Gaps(f, fItems);
        var f0Gaps = Gaps(f0, f0Items);
        string? lost = null;
        string? shape = null;
        if (f0Gaps.Leading != 0 || fGaps.Leading != 0)
            shape ??= $"leading blank lines: F={fGaps.Leading} F₀={f0Gaps.Leading}";
        if (f0Gaps.Trailing != 0 || fGaps.Trailing != 0)
            shape ??= $"trailing blank lines: F={fGaps.Trailing} F₀={f0Gaps.Trailing}";
        for (var i = 1; i < fItems.Count; i++)
        {
            var expected = overrides.TryGetValue(i, out var restated) ? restated
                : between.Contains(i) ? Math.Max(1, f0Gaps.Before[i]) : f0Gaps.Before[i];
            if (fGaps.Before[i] == expected)
                continue;
            var at = $"before F line {fItems[i].StartLine} ('{fItems[i].Key}'): {fGaps.Before[i]} blank line(s), expected {expected} (F₀: {f0Gaps.Before[i]})";
            if (between.Contains(i) && fGaps.Before[i] == 0)
                lost ??= at;
            else
                shape ??= at;
        }

        if (lost != null)
            yield return (BlankLost, lost);
        if (shape != null)
            yield return (BlankShape, shape);
        if (fPlus != f)
            yield return (BlankLength, "Format(Q⁺) != Format(Q): the run's length reaches the output");
        if (fAgain != f)
            yield return (NotIdempotent, "Format(F) != F");
    }

    private static readonly HashSet<TokenType> DefinitionKeywords = new()
    {
        TokenType.Def, TokenType.Class, TokenType.Struct, TokenType.Interface, TokenType.Enum,
        TokenType.Union, TokenType.Delegate, TokenType.Async,
    };

    /// <summary>
    /// The gaps the detached-comment rule fixes (owner ruling 2026-10-13, black's layout), restated: for
    /// a top-level definition (a module-level line starting with a definition keyword, or the first of
    /// its decorators) whose own-line comments above it (at column 1 — an indented comment is a closing
    /// body's end) contain a blank-line run AFTER at least one
    /// comment, the comments up to the last such run are module-level content. The item right after
    /// that run gets the canonical 2 (the definition, or the first comment still attached to it); the
    /// first detached comment gets 2 when the previous top-level statement is a definition, else 1 when
    /// it is an import (the import block's one blank line is a minimum — found at verification, the
    /// restated oracle had the writer's own omission) or the module docstring (owner ruling 2026-10-13 A:
    /// one blank line after it before anything that is not a definition) or a run precedes it, else 0.
    /// Bodies are untouched (their canonical spacing already agrees).
    /// </summary>
    internal static IEnumerable<(int Index, int Gap)> DetachedCommentGaps(IReadOnlyList<Item> items, IReadOnlyList<Run> runs)
    {
        var runLines = runs.Where(r => r.Placement == Placement.Between).Select(r => r.Trivia.Line).ToHashSet();
        var lastTopLevelIsDefinition = false;
        var lastTopLevelIsImport = false;
        var lastTopLevelIsDocstring = false;
        var topLevelSeen = false;
        var previousTopLevel = (TokenType?)null;
        for (var d = 0; d < items.Count; d++)
        {
            var item = items[d];
            if (item.Depth != 0)
                continue;
            var continuesDecorators = previousTopLevel == TokenType.At && PreviousCode(items, d) is { Depth: 0, First: TokenType.At };
            previousTopLevel = item.First;
            if (continuesDecorators)
                continue;
            var startsDefinition = item.First is { } kind && (DefinitionKeywords.Contains(kind) || kind == TokenType.At && DecoratesDefinition(items, d));
            if (startsDefinition)
            {
                var c0 = d;
                // Module-level comments only: a comment indented at a closing body's column is that
                // body's end (the unparser's CloseBody claims it), not the definition's.
                while (c0 > 0 && items[c0 - 1].Depth < 0 && items[c0 - 1].Column == 1)
                    c0--;
                var j = -1;
                for (var k = d; k > c0; k--)
                {
                    if (runLines.Contains(items[k].StartLine))
                    {
                        j = k;
                        break;
                    }
                }

                if (j > 0)
                {
                    yield return (j, 2);
                    if (c0 > 0)
                        yield return (c0, lastTopLevelIsDefinition ? 2 : lastTopLevelIsImport || lastTopLevelIsDocstring || runLines.Contains(items[c0].StartLine) ? 1 : 0);
                }
            }

            lastTopLevelIsDefinition = startsDefinition;
            lastTopLevelIsImport = item.First is TokenType.Import or TokenType.From;
            // The module docstring: the first top-level item of the file is a string.
            lastTopLevelIsDocstring = !topLevelSeen && item.First == TokenType.String;
            topLevelSeen = true;
        }
    }

    private static Item? PreviousCode(IReadOnlyList<Item> items, int index)
    {
        for (var k = index - 1; k >= 0; k--)
        {
            if (items[k].Depth >= 0)
                return items[k];
        }
        return null;
    }

    /// <summary>True when the decorator line at <paramref name="index"/> decorates a definition (the first non-decorator line after it starts one).</summary>
    private static bool DecoratesDefinition(IReadOnlyList<Item> items, int index)
    {
        for (var k = index + 1; k < items.Count; k++)
        {
            if (items[k].Depth < 0 || items[k].First == TokenType.At)
                continue;
            return items[k].First is { } kind && DefinitionKeywords.Contains(kind);
        }
        return false;
    }

    // ================================================================
    // Observation helpers (restated on purpose — the cursor is production)
    // ================================================================

    /// <summary>Where a blank-line run sits (P50 Design Decision 1): kept as one line when <see cref="Between"/>, dropped otherwise.</summary>
    internal enum Placement
    {
        Between,
        BodyStart,
        ModuleStart,
        AfterDecorator,
        EndOfFile,
    }

    /// <summary>A blank-line run: the trivia, the token whose leading trivia holds it, its index there, its placement.</summary>
    internal sealed record Run(Trivia Trivia, Token Carrier, int Index, Placement Placement);

    internal static IReadOnlyList<Token> Lex(string text)
        => FileCompilationPipeline.Lex(new SourceText(text, "<blank-sweep>"), NullLogger.Instance, preserveTrivia: true).Tokens;

    /// <summary>Every blank-line run of the stream, classified by <see cref="Classify"/>.</summary>
    internal static List<Run> Runs(IReadOnlyList<Token> tokens)
    {
        var runs = new List<Run>();
        Token? previous = null;
        Token? lineFirst = null;
        Token? previousLineFirst = null;
        var seenCode = false;
        foreach (var token in tokens)
        {
            var leading = token.LeadingTrivia ?? Array.Empty<Trivia>();
            for (var k = 0; k < leading.Count; k++)
            {
                if (leading[k].Kind == TriviaKind.BlankLines)
                    runs.Add(new Run(leading[k], token, k, Classify(token, leading, k, previous, previousLineFirst, seenCode)));
            }

            if (token.Type == TokenType.Newline)
            {
                if (lineFirst != null)
                    previousLineFirst = lineFirst;
                lineFirst = null;
            }
            else if (token.Type is not (TokenType.Indent or TokenType.Dedent or TokenType.Eof))
            {
                lineFirst ??= token;
                seenCode = true;
            }

            previous = token;
        }

        return runs;
    }

    /// <summary>
    /// The placement predicate (Decision 1), restated: <c>EndOfFile</c> — the carrier is <c>Eof</c> and
    /// no comment follows the run; otherwise a run after a comment on its carrier is <c>Between</c>; a
    /// first item is <c>ModuleStart</c> before any code, <c>BodyStart</c> right after an <c>Indent</c>,
    /// <c>AfterDecorator</c> when the previous logical line starts with <c>@</c>, else <c>Between</c>.
    /// </summary>
    private static Placement Classify(Token carrier, IReadOnlyList<Trivia> leading, int index, Token? previous, Token? previousLineFirst, bool seenCode)
    {
        if (carrier.Type == TokenType.Eof && !leading.Skip(index + 1).Any(t => t.Kind == TriviaKind.Comment))
            return Placement.EndOfFile;
        if (index > 0)
            return Placement.Between;
        if (!seenCode)
            return Placement.ModuleStart;
        if (previous?.Type == TokenType.Indent)
            return Placement.BodyStart;
        if (previousLineFirst?.Type == TokenType.At)
            return Placement.AfterDecorator;
        return Placement.Between;
    }

    /// <summary>
    /// An item: a logical line or an own-line comment, with its first and last source line (1-based) and
    /// its key — the first token's type and value, or the comment's text — which aligns Q's items with F's.
    /// </summary>
    internal readonly record struct Item(int StartLine, int EndLine, string Key, int Depth = -1, TokenType? First = null, int Column = 0);

    /// <summary>For each item of <paramref name="a"/>, the index of its counterpart in <paramref name="b"/> (or −1): a longest common subsequence of the keys.</summary>
    internal static int[] Align(IReadOnlyList<Item> a, IReadOnlyList<Item> b)
    {
        var map = Enumerable.Repeat(-1, a.Count).ToArray();
        int lo = 0, hiA = a.Count, hiB = b.Count;
        while (lo < hiA && lo < hiB && a[lo].Key == b[lo].Key)
        {
            map[lo] = lo;
            lo++;
        }
        while (hiA > lo && hiB > lo && a[hiA - 1].Key == b[hiB - 1].Key)
        {
            map[hiA - 1] = hiB - 1;
            hiA--;
            hiB--;
        }

        int n = hiA - lo, m = hiB - lo;
        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                lcs[i, j] = a[lo + i].Key == b[lo + j].Key ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
        for (int i = 0, j = 0; i < n && j < m;)
        {
            if (a[lo + i].Key == b[lo + j].Key)
            {
                map[lo + i] = lo + j;
                i++;
                j++;
            }
            else if (lcs[i + 1, j] >= lcs[i, j + 1])
                i++;
            else
                j++;
        }

        return map;
    }

    /// <summary>
    /// The items of <paramref name="text"/> in source order: each logical line (first line = its first
    /// code token's, last line = the line of the <c>Newline</c> that ends it, or its last token's end
    /// line) and each own-line comment between logical lines (a comment inside brackets belongs to its line).
    /// </summary>
    internal static List<Item> Items(string text)
    {
        var tokens = Lex(text);
        var items = new List<Item>();
        var open = false;
        int start = 0, end = 0, depth = 0, lineDepth = 0;
        var key = "";
        TokenType? first = null;
        foreach (var token in tokens)
        {
            if (token.Type == TokenType.Indent)
                depth++;
            else if (token.Type == TokenType.Dedent)
                depth--;
            if (!open)
            {
                foreach (var trivia in token.LeadingTrivia ?? Array.Empty<Trivia>())
                {
                    if (trivia.Kind == TriviaKind.Comment && !trivia.IsInline)
                        items.Add(new Item(trivia.Line, trivia.Line, "#" + FormatterTwins.CommentText(trivia), Column: trivia.Column));
                }
            }

            if (token.Type is TokenType.Newline or TokenType.Eof)
            {
                if (open)
                    items.Add(new Item(start, Math.Max(end, token.Type == TokenType.Newline ? token.Line : end), key, lineDepth, first));
                open = false;
                continue;
            }

            if (token.Type is TokenType.Indent or TokenType.Dedent)
                continue;
            if (!open)
            {
                open = true;
                start = token.Line;
                end = token.Line;
                key = $"{token.Type}:{token.Value}";
                lineDepth = depth;
                first = token.Type;
            }

            end = Math.Max(end, EndLine(token, text));
        }

        return items;
    }

    /// <summary>The line a token's source text ends on: its line plus the line breaks inside its extent.</summary>
    private static int EndLine(Token token, string text)
    {
        var line = token.Line;
        if (token.Position < 0)
            return line;
        var stop = Math.Min(text.Length, token.Position + token.Length);
        for (var i = token.Position; i < stop; i++)
        {
            if (text[i] == '\r' && i + 1 < stop && text[i + 1] == '\n')
                i++;
            if (text[i] is '\n' or '\r')
                line++;
        }

        return line;
    }

    internal sealed record GapReading(int Leading, int Trailing, int[] Before);

    /// <summary>The blank lines before each item, before the first and after the last.</summary>
    internal static GapReading Gaps(string text, IReadOnlyList<Item> items)
    {
        var lines = LineDiff.Split(text).Lines;
        var physical = lines.Count - (lines[lines.Count - 1].Length == 0 ? 1 : 0);
        var before = new int[items.Count];
        for (var i = 1; i < items.Count; i++)
            before[i] = Math.Max(0, items[i].StartLine - items[i - 1].EndLine - 1);
        var leading = items.Count > 0 ? items[0].StartLine - 1 : 0;
        var trailing = items.Count > 0 ? Math.Max(0, physical - items[items.Count - 1].EndLine) : physical;
        return new GapReading(leading, trailing, before);
    }

    /// <summary>Q₀: <paramref name="text"/> with every run's lines (<c>[Line − Count, Line − 1]</c>) deleted.</summary>
    internal static string WithoutRuns(string text, IReadOnlyList<Run> runs)
    {
        var (lines, breaks) = LineDiff.Split(text);
        var drop = new HashSet<int>();
        foreach (var run in runs)
        {
            for (var line = run.Trivia.Line - run.Trivia.BlankLineCount; line <= run.Trivia.Line - 1; line++)
                drop.Add(line - 1);
        }

        var sb = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (drop.Contains(i))
                continue;
            sb.Append(lines[i]);
            if (i < breaks.Count)
                sb.Append(breaks[i]);
        }

        return sb.ToString();
    }

    /// <summary>Q⁺: <paramref name="text"/> with two more empty lines at the end of every run.</summary>
    internal static string WithLongerRuns(string text, IReadOnlyList<Run> runs)
    {
        var (lines, breaks) = LineDiff.Split(text);
        var lineBreak = breaks.Count > 0 ? breaks[0] : "\n";
        var insertBefore = runs.Select(r => r.Trivia.Line - 1).ToHashSet();
        var sb = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (insertBefore.Contains(i))
                sb.Append(lineBreak).Append(lineBreak);
            sb.Append(lines[i]);
            if (i < breaks.Count)
                sb.Append(breaks[i]);
        }

        return sb.ToString();
    }

    private static bool SameAst(SModule a, SModule b)
        => StructuralEqualityComparer.Instance.Equals(
            AstNormalizer.Instance.NormalizeModule(a),
            AstNormalizer.Instance.NormalizeModule(b));

    // ================================================================
    // Controls and census
    // ================================================================

    /// <summary>
    /// The oracle sees each bucket on a hand-written F, and nothing on the ruled one: the run between
    /// two body statements kept as one line holds; the same run lost is <c>blankLost</c>; a body-start
    /// run kept is <c>blankShape</c>; an F⁺ that differs is <c>blankLength</c>.
    /// </summary>
    [Fact]
    public void PositiveControl_TheOracleSeesALostRunAKeptBodyStartAndALeakedLength()
    {
        const string q = "def f():\n\n    a = 1\n\n\n    b = 2\n";
        const string f0 = "def f():\n    a = 1\n    b = 2\n";
        const string ruled = "def f():\n    a = 1\n\n    b = 2\n";

        Runs(Lex(q)).Select(r => r.Placement).Should().Equal(Placement.BodyStart, Placement.Between);
        Judge(q, ruled, f0, ruled, ruled).Should().BeEmpty();
        Judge(q, f0, f0, f0, f0).Select(o => o.Bucket).Should().Equal(BlankLost);
        Judge(q, "def f():\n\n    a = 1\n\n    b = 2\n", f0, "def f():\n\n    a = 1\n\n    b = 2\n", "def f():\n\n    a = 1\n\n    b = 2\n")
            .Select(o => o.Bucket).Should().Equal(BlankShape);
        Judge(q, ruled, f0, "def f():\n    a = 1\n\n\n    b = 2\n", ruled).Select(o => o.Bucket).Should().Equal(BlankLength);
        Judge(q, ruled, f0, ruled, f0).Select(o => o.Bucket).Should().Equal(NotIdempotent);

        // An item the formatter adds (here `z`, standing for the `...` of a bodyless definition) shifts
        // no judgement: Q's items align with F's by key, so the run is still judged before `y`.
        const string q2 = "x = 1\n\ny = 2\n";
        const string inserted = "x = 1\nz\n\ny = 2\n";
        Judge(q2, inserted, "x = 1\nz\ny = 2\n", inserted, inserted).Should().BeEmpty();
        Judge(q2, "x = 1\nz\ny = 2\n", "x = 1\nz\ny = 2\n", "x = 1\nz\ny = 2\n", "x = 1\nz\ny = 2\n")
            .Select(o => o.Bucket).Should().Equal(BlankLost);
        Judge(q2, inserted, "x = 1\ny = 2\n", inserted, inserted).Select(o => o.Bucket).Should().Equal(Instrument);

        // The detached-comment rule (owner ruling 2026-10-13): a comment a blank line separates from
        // the definition is module-level; the definition gets its 2 directly above it. F₀ (runs
        // deleted) attaches the comment, so the oracle restates these two gaps.
        const string q3 = "x = 1\n\n# c\n\ndef f():\n    pass\n";
        const string f03 = "x = 1\n\n\n# c\ndef f():\n    pass\n";
        const string ruled3 = "x = 1\n\n# c\n\n\ndef f():\n    pass\n";
        Judge(q3, ruled3, f03, ruled3, ruled3).Should().BeEmpty();
        const string attachedShape = "x = 1\n\n\n# c\n\ndef f():\n    pass\n";
        Judge(q3, attachedShape, f03, attachedShape, attachedShape).Select(o => o.Bucket).Should().Equal(BlankShape);
    }

    /// <summary>
    /// The twins are what they claim: Q₀ deletes exactly the runs (an empty and a whitespace-only line
    /// included) and Q⁺ lengthens each by two, on LF and CRLF text alike; items ignore a blank line inside
    /// brackets or a triple-quoted string.
    /// </summary>
    [Fact]
    public void PositiveControl_TheTwinsDeleteAndLengthenExactlyTheRuns()
    {
        const string q = "x = 1\n\n    \ny = [\n\n    2,\n]\nz = \"\"\"a\n\nb\"\"\"\n";
        var runs = Runs(Lex(q));
        runs.Should().ContainSingle().Which.Trivia.BlankLineCount.Should().Be(2);
        WithoutRuns(q, runs).Should().Be("x = 1\ny = [\n\n    2,\n]\nz = \"\"\"a\n\nb\"\"\"\n");
        WithLongerRuns(q, runs).Should().Be("x = 1\n\n    \n\n\ny = [\n\n    2,\n]\nz = \"\"\"a\n\nb\"\"\"\n");
        Items(q).Select(i => (i.StartLine, i.EndLine)).Should().Equal((1, 1), (4, 7), (8, 10));

        var crlf = q.Replace("\n", "\r\n");
        var crlfRuns = Runs(Lex(crlf));
        WithoutRuns(crlf, crlfRuns).Should().Be(WithoutRuns(q, runs).Replace("\n", "\r\n"));
        WithLongerRuns(crlf, crlfRuns).Should().Be(WithLongerRuns(q, runs).Replace("\n", "\r\n"));
        Items(crlf).Should().Equal(Items(q));
    }

    /// <summary>
    /// Every placement class occurs in the corpus, each anchored to the placement fixture by name; the
    /// counts are printed for the record; the allowlist names corpus stems and its size is a literal.
    /// </summary>
    [Fact]
    public void Census_EveryPlacementClassIsObservedAndTheAllowlistIsStated()
    {
        var stemsByPlacement = Enum.GetValues<Placement>().ToDictionary(p => p, _ => new SortedSet<string>(StringComparer.Ordinal));
        var runsByPlacement = Enum.GetValues<Placement>().ToDictionary(p => p, _ => 0);
        var withRuns = 0;
        foreach (var (stem, fixture) in Corpus)
        {
            var runs = Runs(Lex(fixture.Source));
            if (runs.Count > 0)
                withRuns++;
            foreach (var run in runs)
            {
                runsByPlacement[run.Placement]++;
                stemsByPlacement[run.Placement].Add(stem);
            }
        }

        var allowlist = Allowlist.Value;
        _output.WriteLine($"FMTBLANK-CENSUS corpus={Corpus.Count} cells-with-runs={withRuns} runs "
            + string.Join(" ", runsByPlacement.Select(kv => $"{kv.Key}={kv.Value}"))
            + $" allowlist rows={allowlist.Count(e => !e.IsPin)} pins="
            + string.Join(",", allowlist.Where(e => e.IsPin).Select(e => $"{e.Twin}/{e.Bucket}/{e.Count}")));

        Corpus.Should().ContainKey(PlacementFixture);
        foreach (var placement in Enum.GetValues<Placement>())
            stemsByPlacement[placement].Should().Contain(PlacementFixture, $"the placement fixture must hold a {placement} run");
        allowlist.Where(e => !e.IsPin).Should().OnlyContain(e => Corpus.ContainsKey(e.Stem), "every allowlist row names a corpus fixture");

        // P50 Phase 1 pinned the defect R-ET ruled (blankLost 1057 per twin, #2239); the Phase 2 fix drained
        // both pins. Re-populating the allowlist is a visible decision — change these literals in the same
        // commit as the file and say why.
        allowlist.Count(e => !e.IsPin).Should().Be(0, "the blank-line sweep has no per-stem rows");
        allowlist.Count(e => e.IsPin).Should().Be(0, "the P50 Phase 1 pins were drained by the Phase 2 fix");
    }

    // ================================================================
    // Allowlist
    // ================================================================

    internal sealed record AllowlistEntry(bool IsPin, string Stem, string Twin, string Bucket, int Count, string Digest, string Cite);

    private static readonly Lazy<IReadOnlyList<AllowlistEntry>> Allowlist = new(ReadAllowlist);

    /// <summary>
    /// <c>stem twin bucket # #N reason</c> or <c>pin twin bucket count digest # #N reason</c>; <c>#</c>
    /// starts the cite only after whitespace. A key is pinned or listed by row, never both.
    /// </summary>
    private static IReadOnlyList<AllowlistEntry> ReadAllowlist()
    {
        var path = FindAllowlistPath()
            ?? throw new InvalidOperationException($"Conformance/{AllowlistFileName} is missing. Its presence is what arms the ratchet.");
        var entries = new List<AllowlistEntry>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var split = line.IndexOf(" #", StringComparison.Ordinal);
            var fields = (split < 0 ? line : line.Substring(0, split)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cite = split < 0 ? "" : line.Substring(split + 2).Trim();
            if (!Regex.IsMatch(cite, @"^#\d+\b"))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' must cite an issue first (# #N reason).");

            AllowlistEntry entry;
            if (fields.Length == 5 && fields[0] == "pin" && int.TryParse(fields[3], out var count)
                && (fields[4] == "-" || Regex.IsMatch(fields[4], "^[0-9a-f]{16}$")))
                entry = new AllowlistEntry(true, "", fields[1], fields[2], count, fields[4], cite);
            else if (fields.Length == 3 && fields[0] != "pin")
                entry = new AllowlistEntry(false, fields[0], fields[1], fields[2], 0, "", cite);
            else
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' must read `stem twin bucket # #N reason` or `pin twin bucket count digest # #N reason`.");

            if (!Twins.Contains(entry.Twin) || !Buckets.Contains(entry.Bucket))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}': twin must be one of {{{string.Join(", ", Twins)}}} and bucket one of {{{string.Join(", ", Buckets)}}}.");
            if (!keys.Add(entry.IsPin ? $"pin {entry.Twin} {entry.Bucket}" : $"{entry.Stem} {entry.Twin} {entry.Bucket}"))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' is listed twice.");
            entries.Add(entry);
        }

        foreach (var row in entries.Where(e => !e.IsPin))
        {
            if (entries.Any(e => e.IsPin && e.Twin == row.Twin && e.Bucket == row.Bucket))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{row.Stem} {row.Twin} {row.Bucket}' is a row under a pinned key.");
        }

        return entries;
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

    /// <summary>The first 16 hex characters of SHA-256 over the sorted, <c>\n</c>-joined stems; <c>-</c> for none.</summary>
    internal static string Digest(IEnumerable<string> stems)
    {
        var sorted = stems.OrderBy(s => s, StringComparer.Ordinal).ToList();
        if (sorted.Count == 0)
            return "-";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", sorted)));
        return Convert.ToHexString(hash).ToLowerInvariant().Substring(0, 16);
    }
}
