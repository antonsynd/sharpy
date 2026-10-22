using System.Diagnostics;
using FluentAssertions;
using Sharpy.TestInfrastructure.Formatting;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;

namespace Sharpy.Compiler.Tests.Lexer;

public partial class LiteralLossScannerDifferentialTests
{
    /// <summary>Every <see cref="ShapeStride"/>-th clean corpus document (ordinal order) parents the shapes (fixed, never random).</summary>
    private const int ShapeStride = 1;

    /// <summary>
    /// The continuation shapes' construction-known truth (<see cref="FormatterTwins.ContinuationShapes"/>; plan-f92797
    /// Design 5, lead rulings L8/L10/L11): for each shape, the recovery that drops its injected line resumes as a
    /// CONTINUED line (a bracket left open at the line's end), as a POSSIBLE HEADER (a colon at depth 0 read or given up,
    /// or a line end the lexer cannot read), and whether <c>BracketLeftOpenAtLine</c> is the injected line. <c>-nested</c>
    /// drops two header lines; <c>-cont</c> drops the header's continuation line (<see cref="ContinuationShape.DroppedOffset"/>);
    /// <c>bracket-eof</c> has no recovery (the lexer follows the bracket to the end of the source).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (bool Continued, bool Header, bool BracketAtInjected)> ContinuationTruth =
        new Dictionary<string, (bool, bool, bool)>(StringComparer.Ordinal)
        {
            [FormatterTwins.BracketEofShape] = (false, false, true),
            [FormatterTwins.BracketStringShape] = (true, false, true),
            [FormatterTwins.BracketBacktickShape] = (true, false, true),
            [FormatterTwins.BracketDroppedShape] = (true, false, true),
            [FormatterTwins.RecoveryShape] = (false, false, false),
            [FormatterTwins.RecoveryMisindentedShape] = (false, false, false),
            [FormatterTwins.RecoveryHeaderPrefix + "2"] = (false, true, false),
            [FormatterTwins.RecoveryHeaderPrefix + "4"] = (false, true, false),
            [FormatterTwins.RecoveryHeaderPrefix + "6"] = (false, true, false),
            [FormatterTwins.RecoveryHeaderPrefix + "tab"] = (false, true, false),
            [FormatterTwins.RecoveryHeaderPrefix + "string"] = (false, true, false),
            [FormatterTwins.RecoveryHeaderPrefix + "backtick"] = (false, true, false),
            [FormatterTwins.RecoveryHeaderNestedShape] = (false, true, false),
            [FormatterTwins.RecoveryHeaderSiblingShape] = (false, true, false),
            [FormatterTwins.RecoveryHeaderContShape] = (false, true, false),
        };

    /// <summary>The offset where 0-based <paramref name="line"/> of <paramref name="text"/> starts (<c>\r\n</c>, <c>\r</c> and <c>\n</c> each end a line).</summary>
    private static int LineStartOf(string text, int line)
    {
        var i = 0;
        for (var l = 0; l < line && i < text.Length; l++)
        {
            while (i < text.Length && text[i] is not ('\n' or '\r'))
                i++;
            if (i < text.Length && text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                i++;
            i++;
        }
        return i;
    }

    /// <summary>
    /// The verdicts of one literal-loss shape against its construction-known truth, or null when the shape's truth is not
    /// a construction fact (the <c>repair*</c>/<c>closeline*</c> shapes: R-FR/R-FV, owned by <c>LiteralStateTests</c>' rows
    /// and <c>Corpus_EveryAbortFreeLossShape_SetsItsMechanismsFlag</c>) or its construct did not fire where it was built
    /// (observed, never assumed — the dropped shapes on an opener line inside a bracket measure no indentation).
    /// <list type="bullet">
    /// <item>A budget stop (<see cref="FormatterTwins.BudgetStopLines"/>) leaves ALL of Q unread, so <c>LiteralLoss</c> is
    /// exactly <c>UnreadRemainder</c> when Q holds a literal spanning lines (<c>Kind</c> not null) and exactly <c>None</c>
    /// otherwise — the scanner's verdict on the unread text, read from the aborted construct's own start (an unterminated
    /// short string, a string aborting at an invalid escape, an f-string aborting in a field, a line dropped whole).</item>
    /// <item>A <c>dropped-SPY00NN</c> shape whose FIRST error is its code on a line where one of Q's literals spanning lines
    /// opens drops text holding that opener — <c>DroppedOpener</c> must be set.</item>
    /// </list>
    /// </summary>
    internal static (string Key, string? Disagreement)? JudgeLossShape(string name, LiteralLossShape shape, LexerNs.Lexer lexer, IReadOnlyList<(int Start, int End)> qSpans, string q)
    {
        if (FormatterTwins.IsBudgetShape(shape.Shape))
        {
            var want = shape.Kind == null ? LexerNs.LiteralLoss.None : LexerNs.LiteralLoss.UnreadRemainder;
            var key = $"{name}:{shape.LineShift}:shape:{shape.Shape}";
            return (key, lexer.LiteralLoss == want ? null : $"LiteralLoss lexer={lexer.LiteralLoss} truth={want} (Kind {shape.Kind ?? "none"})");
        }
        if (!shape.Shape.StartsWith(FormatterTwins.DroppedShapePrefix, StringComparison.Ordinal))
            return null;

        var first = lexer.Diagnostics.GetErrors().FirstOrDefault();
        if (first == null || first.Code != shape.Shape[FormatterTwins.DroppedShapePrefix.Length..] || first.Line is not { } errorLine)
            return null;
        var qLines = LineRanges(q);
        var line = errorLine - 1;
        if (line < 0 || line >= qLines.Count)
            return null;
        var (lineStart, lineEnd) = qLines[line];
        if (!qSpans.Any(s => s.Start >= lineStart && s.Start < lineEnd && s.End > lineEnd))
            return null;   // not an opener line of Q's (the dropped shapes keep Q's lines in place: LineShift 0)
        var dropped = (lexer.LiteralLoss & LexerNs.LiteralLoss.DroppedOpener) != 0;
        return ($"{name}:{errorLine}:shape:{shape.Shape}", dropped ? null : $"DroppedOpener lexer=false truth=true ({lexer.LiteralLoss})");
    }

    /// <summary>The verdicts of one continuation shape against <see cref="ContinuationTruth"/>.</summary>
    internal static (string Key, string? Disagreement) JudgeContinuationShape(string name, ContinuationShape shape, LexerNs.Lexer lexer)
    {
        var injected = shape.InsertedAt + 1;
        var key = $"{name}:{injected}:shape:{shape.Shape}";
        var truth = ContinuationTruth[shape.Shape];
        var wrong = new List<string>();
        var bracketAtInjected = lexer.BracketLeftOpenAtLine == injected;
        if (bracketAtInjected != truth.BracketAtInjected)
            wrong.Add($"BracketLeftOpenAtLine lexer={lexer.BracketLeftOpenAtLine?.ToString() ?? "null"} truth={(truth.BracketAtInjected ? injected.ToString() : "not " + injected)}");
        if (lexer.BracketLeftOpenAtLine is { } opener && opener < injected)
            wrong.Add($"BracketLeftOpenAtLine {opener} is above the injected line {injected}: Q is clean there");
        if (shape.Shape != FormatterTwins.BracketEofShape)
        {
            var dropped = shape.Shape == FormatterTwins.RecoveryHeaderNestedShape ? new[] { 0, 1 } : new[] { shape.DroppedOffset };
            foreach (var offset in dropped)
            {
                var resume = LineStartOf(shape.Text, shape.InsertedAt + offset + 1);
                if (!lexer.RecoveryResumes.Contains(resume))
                {
                    wrong.Add($"no recovery resumes after injected line {injected + offset} (resumes {string.Join(",", lexer.RecoveryResumes)})");
                    continue;
                }
                var continued = lexer.RecoveryResumesAfterAContinuedLine.Contains(resume);
                if (continued != truth.Continued)
                    wrong.Add($"Continued@{injected + offset} lexer={continued} truth={truth.Continued}");
                var header = lexer.RecoveryResumesAfterAPossibleHeader.Contains(resume);
                if (header != truth.Header)
                    wrong.Add($"PossibleHeader@{injected + offset} lexer={header} truth={truth.Header}");
            }
        }
        return (key, wrong.Count == 0 ? null : string.Join("; ", wrong));
    }

    /// <summary>
    /// P68 Phase 3 Task 3 (#2300 row C5): the P22 shape builders (<see cref="FormatterTwins.LiteralLossShapes"/> with
    /// <see cref="FormatterTwins.BudgetStopLines"/>, and <see cref="FormatterTwins.ContinuationShapes"/>) over the clean
    /// corpus, each document's lexer facts judged against the truth its CONSTRUCTION fixes (<see cref="JudgeLossShape"/>,
    /// <see cref="ContinuationTruth"/>) — the per-document exact verdicts the two shape sweeps in <c>LiteralStateTests</c>
    /// leave unasserted (the budget stop's exact flag, the dropped opener on every document its construct fired on, the
    /// possible-header and continued facts of every continuation shape). Loss shapes over Q and its wide twin (the SPY0014
    /// shape needs every level a multiple of 8); continuation shapes over Q and its lone-CR twin. Disagreements are
    /// ratcheted by <see cref="ScannerAllowlist"/> under <c>file:line:shape:NAME</c> keys.
    /// </summary>
    [Fact]
    public void Shapes_LexerFactsMatchTheirConstructionKnownTruth()
    {
        var clock = Stopwatch.StartNew();
        var allowlist = ScannerAllowlist.Load();
        var corpus = CorpusDocuments();
        var universe = new HashSet<string>(StringComparer.Ordinal);
        var disagreements = new Dictionary<string, string>(StringComparer.Ordinal);
        var judged = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var parents = 0;
        var clean = 0;
        var documents = 0;

        foreach (var (name, source) in corpus)
        {
            if (CleanLineFacts.Of(source) == null)
                continue;
            if (clean++ % ShapeStride != 0)
                continue;
            parents++;

            foreach (var (q, twin) in new[] { (source, ""), (FormatterTwins.WideIndented(source), "@wide") })
            {
                var parent = CleanLineFacts.Of(q);
                if (parent == null)
                    continue;
                foreach (var shape in FormatterTwins.LiteralLossShapes(q, includeMismatch: twin.Length > 0))
                {
                    var lexer = new LexerNs.Lexer(shape.Text);
                    lexer.TokenizeAll();
                    documents++;
                    if (JudgeLossShape(name + twin, shape, lexer, parent.Spans, q) is not { } verdict)
                        continue;
                    judged[shape.Shape] = judged.GetValueOrDefault(shape.Shape) + 1;
                    universe.Add(verdict.Key);
                    if (verdict.Disagreement != null)
                        disagreements[verdict.Key] = verdict.Disagreement;
                }
            }

            foreach (var (q, twin) in new[] { (source, ""), (FormatterTwins.Cr(source), "@cr") })
            {
                if (CleanLineFacts.Of(q) == null)
                    continue;
                foreach (var shape in FormatterTwins.ContinuationShapes(q))
                {
                    var lexer = new LexerNs.Lexer(shape.Text);
                    lexer.TokenizeAll();
                    documents++;
                    var (key, disagreement) = JudgeContinuationShape(name + twin, shape, lexer);
                    judged[shape.Shape] = judged.GetValueOrDefault(shape.Shape) + 1;
                    universe.Add(key);
                    if (disagreement != null)
                        disagreements[key] = disagreement;
                }
            }
        }

        var (unlisted, stale, allowlisted) = allowlist.Ratchet(ScannerAllowlist.IsShapeKey, universe, disagreements);
        clock.Stop();
        _output.WriteLine($"SCANNER-SHAPES parents {parents} of clean {clean} stride {ShapeStride} documents {documents} cells {universe.Count} "
            + $"disagreements {disagreements.Count} allowlisted {allowlisted} wall {clock.Elapsed.TotalSeconds:F1}s");
        _output.WriteLine($"SCANNER-SHAPES judged per shape: {string.Join(", ", judged.Select(j => $"{j.Key}={j.Value}"))}");
        foreach (var line in unlisted.Take(200))
            _output.WriteLine("SCANNER-SHAPES DISAGREE " + line);
        foreach (var line in stale.Concat(allowlist.Malformed))
            _output.WriteLine("SCANNER-SHAPES ALLOWLIST " + line);

        parents.Should().BeGreaterThanOrEqualTo(250, "the shapes' parents are a stride over the clean corpus");
        var expectedShapes = FormatterTwins.BudgetStopLines.Select(b => b.Shape)
            .Concat(FormatterTwins.DroppedCodes.Select(c => FormatterTwins.DroppedShapePrefix + c))
            .Concat(ContinuationTruth.Keys);
        expectedShapes.Where(s => judged.GetValueOrDefault(s) == 0).Should().BeEmpty("every judged shape must reach ≥ 1 document");
        ContinuationTruth.Keys.Should().BeEquivalentTo(FormatterTwins.ContinuationShapeNames, "the truth table covers every continuation shape the builder names");
        allowlist.Malformed.Should().BeEmpty();
        stale.Should().BeEmpty();
        unlisted.Should().BeEmpty();
    }
}
