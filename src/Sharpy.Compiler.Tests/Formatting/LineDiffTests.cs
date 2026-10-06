using System.Collections.Concurrent;
using System.Diagnostics;
using CsCheck;
using FluentAssertions;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Tests.Conformance;
using Sharpy.TestInfrastructure.Formatting;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Formatting;

/// <summary>
/// <see cref="LineDiff"/> (P22e Phase 2 Task 1, #2168): the hunks between a source and its formatted
/// text, over line content, and their application.
/// <list type="bullet">
/// <item>(i) every corpus fixture Q and each twin: <c>Apply(Q, Hunks(Q, Format(Q))) == Format(Q)</c>
/// (modulo line breaks for the CRLF twin, whose result has no bare <c>\n</c>);</item>
/// <item>(ii) hunks are sorted, non-overlapping and non-adjacent — checked on every <c>Hunks</c> call here;</item>
/// <item>(iii) a CsCheck property over random line lists, cross-checked against a test-local reference
/// LCS diff (agreement on <c>Apply</c>, not on the hunks);</item>
/// <item>(iv) no trailing line break, the empty document, one line, a lone <c>\r</c>, CRLF;</item>
/// <item>(v) a middle over the (lowered) size bound is one hunk and still satisfies (i);</item>
/// <item>(vi) minimality: under the bound, the source lines the hunks cover number exactly
/// <c>len(a) − LCS(a, b)</c>, the LCS length taken from the reference diff.</item>
/// </list>
/// </summary>
public class LineDiffTests
{
    private readonly ITestOutputHelper _output;

    public LineDiffTests(ITestOutputHelper output) => _output = output;

    // ================================================================
    // Twins — the seam property (i) runs over
    // ================================================================

    internal const string IdentityTwin = "identity";
    internal const string CrlfTwin = "crlf";
    internal const string CommentTwin = "comment";
    internal const string WideTwin = "wide";

    /// <summary>
    /// The twins of a corpus fixture that property (i) formats — the four the route-parity sweep
    /// drives: <c>identity</c>, <c>crlf</c>, and <c>FormatterTwins</c>' <c>comment</c> and <c>wide</c>.
    /// <c>Crlf</c> marks a twin whose comparison is modulo line breaks.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (Func<string, string> Make, bool Crlf)> TwinTransforms =
        new Dictionary<string, (Func<string, string>, bool)>(StringComparer.Ordinal)
        {
            [IdentityTwin] = (source => source, false),
            [CrlfTwin] = (ToCrlf, true),
            [CommentTwin] = (source => FormatterTwins.CommentInjected(source).Text, false),
            [WideTwin] = (FormatterTwins.WideIndented, false),
        };

    public static IEnumerable<object[]> TwinNames() => TwinTransforms.Keys.Select(name => new object[] { name });

    private static string ToCrlf(string source) => source.Replace("\r\n", "\n").Replace("\n", "\r\n");

    internal sealed record FormattedCase(string Stem, string Q, string Formatted);

    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<FormattedCase>>> FormattedCorpus = new(StringComparer.Ordinal);

    /// <summary>Every corpus fixture's twin and its <c>Format</c>, computed once per twin per process.</summary>
    private static IReadOnlyList<FormattedCase> Formatted(string twin)
        => FormattedCorpus.GetOrAdd(twin, t => new Lazy<IReadOnlyList<FormattedCase>>(() =>
        {
            var make = TwinTransforms[t].Make;
            return FormatterMeaningPreservationSweepTests.CorpusCensus.Value.Corpus.Values
                .Select(f =>
                {
                    var q = make(f.Source);
                    return new FormattedCase(f.Name, q, FormatterService.Format(q).FormattedText);
                })
                .ToList();
        })).Value;

    // ================================================================
    // (i) + (ii) over the corpus
    // ================================================================

    [Theory]
    [MemberData(nameof(TwinNames))]
    public void Corpus_ApplyingTheHunksGivesTheFormattedText(string twin)
    {
        var watch = Stopwatch.StartNew();
        var cases = Formatted(twin);
        var formatSeconds = watch.Elapsed.TotalSeconds;
        var crlf = TwinTransforms[twin].Crlf;
        var failures = new List<string>();
        int changed = 0, multiHunk = 0, hunkTotal = 0;
        foreach (var c in cases)
        {
            if (crlf && HasBareLineFeed(c.Q))
                failures.Add($"{c.Stem}: instrument — the CRLF twin has a bare \\n");
            if (Check(c, crlf, LineDiff.DefaultMaxCells) is { } failure)
                failures.Add(failure);
            var hunks = LineDiff.Hunks(c.Q, c.Formatted);
            changed += hunks.Count > 0 ? 1 : 0;
            multiHunk += hunks.Count > 1 ? 1 : 0;
            hunkTotal += hunks.Count;
        }

        _output.WriteLine($"LINEDIFF-I twin={twin} corpus={cases.Count} changed={changed} multi-hunk={multiHunk} hunks={hunkTotal} "
            + $"format={formatSeconds:F1}s total={watch.Elapsed.TotalSeconds:F1}s");
        failures.Should().BeEmpty();
        cases.Count.Should().BeGreaterThan(100, "the corpus is the meaning-preservation sweep's");
        changed.Should().BeGreaterThan(0, "Format changes some fixture, else (i) is vacuous");
    }

    /// <summary>
    /// The positive control of (ii) and (v), kept out of (i) so that (i) stays blind to how coarse the
    /// hunks are (minimality is (vi)'s): some fixture changes in more than one place, so
    /// non-adjacency is exercised and lowering the bound is observable.
    /// </summary>
    [Fact]
    public void Corpus_Control_SomeFixtureChangesInSeveralPlaces()
    {
        var multiHunk = Formatted(IdentityTwin).Count(c => LineDiff.Hunks(c.Q, c.Formatted).Count > 1);
        _output.WriteLine($"LINEDIFF-CONTROL multi-hunk={multiHunk}");
        multiHunk.Should().BeGreaterThan(0);
    }

    /// <summary>(v) with the bound lowered to 0: every changed middle is one hunk, and (i) still holds.</summary>
    [Fact]
    public void Corpus_OverTheBound_TheMiddleIsOneHunk_AndApplyStillGivesTheFormattedText()
    {
        var failures = new List<string>();
        foreach (var c in Formatted(IdentityTwin))
        {
            if (Check(c, crlf: false, maxCells: 0) is { } failure)
                failures.Add(failure);
            var hunks = LineDiff.Hunks(c.Q, c.Formatted, maxCells: 0);
            if (hunks.Count > 1)
                failures.Add($"{c.Stem}: {hunks.Count} hunks over the bound, expected one");
        }

        failures.Should().BeEmpty();
    }

    /// <summary>(vi) on the corpus: the hunks cover exactly <c>n − LCS</c> source lines.</summary>
    [Fact]
    public void Corpus_HunksAreMinimal()
    {
        var failures = new List<string>();
        foreach (var c in Formatted(IdentityTwin))
        {
            var a = LineDiff.Split(c.Q).Lines;
            var b = LineDiff.Split(c.Formatted).Lines;
            var covered = LineDiff.Hunks(a, b).Sum(h => h.End - h.Start);
            var expected = a.Count - ReferenceLcsLength(a, b);
            if (covered != expected)
                failures.Add($"{c.Stem}: hunks cover {covered} source lines, n − LCS = {expected}");
        }

        failures.Should().BeEmpty();
    }

    private static string? Check(FormattedCase c, bool crlf, long maxCells)
    {
        IReadOnlyList<LineHunk> hunks;
        string applied;
        try
        {
            hunks = LineDiff.Hunks(c.Q, c.Formatted, maxCells);
            applied = LineDiff.Apply(c.Q, hunks);
        }
        catch (Exception e)
        {
            return $"{c.Stem}: {e.GetType().Name}: {e.Message}";
        }

        if (WellFormed(hunks, LineDiff.Split(c.Q).Lines.Count) is { } malformed)
            return $"{c.Stem}: {malformed}";
        if (crlf)
        {
            if (HasBareLineFeed(applied))
                return $"{c.Stem}: the applied CRLF text has a bare \\n";
            if (Lf(applied) != Lf(c.Formatted))
                return $"{c.Stem}: Apply differs from Format modulo line breaks";
        }
        else if (applied != c.Formatted)
        {
            return $"{c.Stem}: Apply differs from Format";
        }

        return null;
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n");

    private static bool HasBareLineFeed(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n' && (i == 0 || text[i - 1] != '\r'))
                return true;
        }

        return false;
    }

    /// <summary>(ii): sorted, in range, each hunk a change, non-overlapping and non-adjacent.</summary>
    private static string? WellFormed(IReadOnlyList<LineHunk> hunks, int sourceLines)
    {
        for (var k = 0; k < hunks.Count; k++)
        {
            var h = hunks[k];
            if (h.Start < 0 || h.End < h.Start || h.End > sourceLines)
                return $"hunk {k} [{h.Start}, {h.End}) is out of range for {sourceLines} lines";
            if (h.End == h.Start && h.NewLines.Count == 0)
                return $"hunk {k} at {h.Start} is empty";
            if (k > 0 && hunks[k - 1].End >= h.Start)
                return $"hunk {k - 1} [{hunks[k - 1].Start}, {hunks[k - 1].End}) touches or overlaps hunk {k} [{h.Start}, {h.End})";
        }

        return null;
    }

    // ================================================================
    // (iii) + (vi) — CsCheck over random line lists
    // ================================================================

    private static readonly Gen<string[]> GenLines = Gen.OneOfConst("a", "b", "c", "", "    d", "e e").Array[0, 14];
    private static readonly Gen<string> GenBreak = Gen.OneOfConst("\n", "\r\n", "\r");

    /// <summary>(iii) + (ii): <c>Apply(a, Hunks(a, b)) == b</c>, and the reference diff applies to the same text.</summary>
    [Fact]
    public void Property_ApplyOfHunksIsTheTarget_AndAgreesWithTheReference()
    {
        Gen.Select(GenLines, GenLines, GenBreak).Sample(t =>
        {
            var (aLines, bLines, sourceBreak) = t;
            var a = string.Join(sourceBreak, aLines);
            var b = string.Join("\n", bLines);
            var aSplit = LineDiff.Split(a).Lines;
            var bSplit = LineDiff.Split(b).Lines;

            var hunks = LineDiff.Hunks(a, b);
            WellFormed(hunks, aSplit.Count).Should().BeNull();

            // A source with a line break inserts its own; one line (no break) inserts \n.
            var eol = aSplit.Count > 1 ? sourceBreak : "\n";
            var expected = string.Join(eol, bSplit);
            LineDiff.Apply(a, hunks).Should().Be(expected);
            LineDiff.Apply(a, ReferenceDiff(aSplit, bSplit).Hunks).Should().Be(expected, "the reference diff applies to the same text");
        }, iter: 2000);
    }

    /// <summary>(vi): under the bound the hunks cover exactly <c>n − LCS</c> source lines.</summary>
    [Fact]
    public void Property_HunksAreMinimal()
    {
        Gen.Select(GenLines, GenLines).Sample(t =>
        {
            var (aLines, bLines) = t;
            var aSplit = LineDiff.Split(string.Join("\n", aLines)).Lines;
            var bSplit = LineDiff.Split(string.Join("\n", bLines)).Lines;
            var lcs = ReferenceDiff(aSplit, bSplit).Lcs;
            ReferenceLcsLength(aSplit, bSplit).Should().Be(lcs);
            LineDiff.Hunks(aSplit, bSplit).Sum(h => h.End - h.Start).Should().Be(aSplit.Count - lcs);
        }, iter: 2000);
    }

    // ================================================================
    // (iv) edges and (v) the bound, by hand
    // ================================================================

    [Fact]
    public void MissingFinalLineBreak_IsAnInsertionAtTheEnd()
    {
        AssertHunks("a\nb", "a\nb\n", (2, 2, new[] { "" }));
        LineDiff.Apply("a\nb", LineDiff.Hunks("a\nb", "a\nb\n")).Should().Be("a\nb\n");
        LineDiff.Apply("a\r\nb", LineDiff.Hunks("a\r\nb", "a\nb\n")).Should().Be("a\r\nb\r\n");
    }

    [Fact]
    public void RemovedFinalLineBreak_IsADeletionAtTheEnd()
    {
        AssertHunks("a\nb\n", "a\nb", (2, 3, Array.Empty<string>()));
        LineDiff.Apply("a\nb\n", LineDiff.Hunks("a\nb\n", "a\nb")).Should().Be("a\nb");
    }

    [Fact]
    public void AnInsertionTouchingAReplacement_IsOneHunk()
    {
        // The final line break added after a replaced last line: one hunk ending at n (decision 2).
        AssertHunks("x=1", "x = 1\n", (0, 1, new[] { "x = 1", "" }));
        LineDiff.Apply("x=1", LineDiff.Hunks("x=1", "x = 1\n")).Should().Be("x = 1\n");
        AssertHunks("a\nB\nc\n", "a\nb\nX\nc\n", (1, 2, new[] { "b", "X" }));
    }

    [Fact]
    public void EmptyDocument()
    {
        LineDiff.Split("").Lines.Should().Equal("");
        LineDiff.Hunks("", "").Should().BeEmpty();
        // "" is [""] and "x\n" is ["x", ""]: the common suffix "" stays, "x" is inserted before it.
        AssertHunks("", "x\n", (0, 0, new[] { "x" }));
        LineDiff.Apply("", LineDiff.Hunks("", "x\n")).Should().Be("x\n");
        AssertHunks("x\n", "", (0, 1, Array.Empty<string>()));
        LineDiff.Apply("x\n", LineDiff.Hunks("x\n", "")).Should().Be("");
    }

    [Fact]
    public void OneLine()
    {
        AssertHunks("x", "y", (0, 1, new[] { "y" }));
        LineDiff.Apply("x", LineDiff.Hunks("x", "y")).Should().Be("y");
        LineDiff.Hunks("x", "x").Should().BeEmpty();
    }

    [Fact]
    public void LoneCarriageReturn_EndsALine_AndIsTheDocumentsLineBreak()
    {
        LineDiff.Split("a\rb\r").Lines.Should().Equal("a", "b", "");
        LineDiff.Split("a\rb\r").Breaks.Should().Equal("\r", "\r");
        LineDiff.DocumentLineBreak("a\rb").Should().Be("\r");
        AssertHunks("a\rb\r", "a\nc\n", (1, 2, new[] { "c" }));
        LineDiff.Apply("a\rb\r", LineDiff.Hunks("a\rb\r", "a\nc\n")).Should().Be("a\rc\r");
        LineDiff.Apply("a\rb\r", LineDiff.Hunks("a\rb\r", "a\nX\nb\n")).Should().Be("a\rX\rb\r");
    }

    [Fact]
    public void Crlf_StaysCrlf_AndOnlyChangedLinesAreHunks()
    {
        AssertHunks("a\r\nb\r\n", "a\nb\nc\n", (2, 2, new[] { "c" }));
        LineDiff.Apply("a\r\nb\r\n", LineDiff.Hunks("a\r\nb\r\n", "a\nb\nc\n")).Should().Be("a\r\nb\r\nc\r\n");
        LineDiff.Hunks("a\r\nb\r\n", "a\nb\n").Should().BeEmpty("line content is compared, not line breaks");
    }

    [Fact]
    public void Apply_KeepsEachLinesOwnBreak_AndTheHunksLastBreak()
    {
        // Mixed breaks: unchanged lines keep theirs; a hunk keeps the break of its last replaced line;
        // breaks a hunk writes between new lines are the document's first (\n here).
        const string source = "a\nb\r\nc\rd";
        var hunks = new[] { new LineHunk(1, 3, new[] { "B", "X", "C" }) };
        LineDiff.Apply(source, hunks).Should().Be("a\nB\nX\nC\rd");
        LineDiff.Apply(source, new[] { new LineHunk(1, 3, Array.Empty<string>()) }).Should().Be("a\nd");
        LineDiff.Apply(source, new[] { new LineHunk(3, 4, Array.Empty<string>()) }).Should().Be("a\nb\r\nc");
        LineDiff.Apply(source, new[] { new LineHunk(4, 4, new[] { "e" }) }).Should().Be("a\nb\r\nc\rd\ne");
    }

    [Fact]
    public void Apply_RefusesOutOfOrderOrOutOfRangeHunks()
    {
        var act1 = () => LineDiff.Apply("a\nb", new[] { new LineHunk(1, 2, new[] { "x" }), new LineHunk(0, 1, new[] { "y" }) });
        act1.Should().Throw<ArgumentException>();
        var act2 = () => LineDiff.Apply("a\nb", new[] { new LineHunk(1, 3, new[] { "x" }) });
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TheBound_AMiddleOfMoreCellsIsOneHunk_AtMostTheBoundIsDiffed()
    {
        // Prefix "p", suffix "s"; the middle is 3 × 3 = 9 cells with two changed lines.
        const string source = "p\na\nk\nb\ns";
        const string target = "p\nA\nk\nB\ns";
        AssertHunks(source, target, maxCells: 9, (1, 2, new[] { "A" }), (3, 4, new[] { "B" }));
        AssertHunks(source, target, maxCells: 8, (1, 4, new[] { "A", "k", "B" }));
        LineDiff.Apply(source, LineDiff.Hunks(source, target, maxCells: 8)).Should().Be(target);
        LineDiff.DefaultMaxCells.Should().Be(4_000_000);
    }

    private static void AssertHunks(string source, string target, params (int Start, int End, string[] NewLines)[] expected)
        => AssertHunks(source, target, LineDiff.DefaultMaxCells, expected);

    private static void AssertHunks(string source, string target, long maxCells, params (int Start, int End, string[] NewLines)[] expected)
    {
        var hunks = LineDiff.Hunks(source, target, maxCells);
        hunks.Select(h => (h.Start, h.End, string.Join("|", h.NewLines)))
            .Should().Equal(expected.Select(e => (e.Start, e.End, string.Join("|", e.NewLines))));
        hunks.Select(h => h.NewLines.Count).Should().Equal(expected.Select(e => e.NewLines.Length));
    }

    // ================================================================
    // The reference diff: a full-table LCS, no trimming, walked from the end
    // ================================================================

    /// <summary>A naive LCS diff of the whole lists (no prefix/suffix trim, the other tie-break), and the LCS length.</summary>
    private static (IReadOnlyList<LineHunk> Hunks, int Lcs) ReferenceDiff(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        int n = a.Count, m = b.Count;
        var table = new int[n + 1, m + 1];
        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
                table[i, j] = a[i - 1] == b[j - 1] ? table[i - 1, j - 1] + 1 : Math.Max(table[i - 1, j], table[i, j - 1]);
        }

        // Walk back; ops in reverse, 'k' keep / 'd' delete a[i] / 'i' insert b[j].
        var ops = new List<(char Op, int I, int J)>();
        int x = n, y = m;
        while (x > 0 || y > 0)
        {
            if (x > 0 && y > 0 && a[x - 1] == b[y - 1] && table[x, y] == table[x - 1, y - 1] + 1)
            {
                ops.Add(('k', x - 1, y - 1));
                x--;
                y--;
            }
            else if (y > 0 && (x == 0 || table[x, y - 1] >= table[x - 1, y]))
            {
                ops.Add(('i', x, y - 1));
                y--;
            }
            else
            {
                ops.Add(('d', x - 1, y));
                x--;
            }
        }

        ops.Reverse();
        var hunks = new List<LineHunk>();
        int? start = null;
        var lines = new List<string>();
        var end = 0;
        foreach (var (op, i, j) in ops)
        {
            if (op == 'k')
            {
                if (start is { } s)
                    hunks.Add(new LineHunk(s, end, lines.ToArray()));
                start = null;
                lines.Clear();
                end = i + 1;
                continue;
            }

            start ??= end;
            if (op == 'd')
                end = i + 1;
            else
                lines.Add(b[j]);
        }

        if (start is { } last)
            hunks.Add(new LineHunk(last, end, lines.ToArray()));
        return (hunks, table[n, m]);
    }

    private static int ReferenceLcsLength(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var previous = new int[b.Count + 1];
        var current = new int[b.Count + 1];
        for (var i = 1; i <= a.Count; i++)
        {
            for (var j = 1; j <= b.Count; j++)
                current[j] = a[i - 1] == b[j - 1] ? previous[j - 1] + 1 : Math.Max(previous[j], current[j - 1]);
            (previous, current) = (current, previous);
        }

        return previous[b.Count];
    }
}
