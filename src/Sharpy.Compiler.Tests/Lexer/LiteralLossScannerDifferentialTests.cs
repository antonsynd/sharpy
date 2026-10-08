using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// The literal-loss scanner (<see cref="LexerNs.Lexer.HoldsALiteralSpanningLines"/>) is a hand model of
/// the lexer's literal reads; this differential judges it against the real lexer (#2271, verify round @
/// 5c8d81f28 — the model was refuted four times by hand before this guard existed). Over every document
/// that lexes clean, at every line start and token start outside a literal: the scanner's verdict on
/// <c>[at, end of line)</c> must equal "a literal the lexer read starts there and crosses the line break",
/// and its verdict on the remainder from each line start must equal "a literal read from that line on
/// spans lines". A document with a lexer error is outside the oracle (the lexer's spans stop at the abort),
/// so the hand rows assert a clean lex first — a row that errors is a test defect, not a vacuous pass.
/// </summary>
public class LiteralLossScannerDifferentialTests
{
    private readonly ITestOutputHelper _output;

    public LiteralLossScannerDifferentialTests(ITestOutputHelper output) => _output = output;

    /// <summary>The <c>[start, end)</c> of each line's text, the break excluded (<c>\n</c>, <c>\r</c>, <c>\r\n</c>).</summary>
    private static List<(int Start, int End)> LineRanges(string source)
    {
        var ranges = new List<(int, int)>();
        var start = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] is '\n' or '\r')
            {
                ranges.Add((start, i));
                if (source[i] == '\r' && i + 1 < source.Length && source[i + 1] == '\n')
                    i++;
                start = i + 1;
            }
        }
        if (start < source.Length)
            ranges.Add((start, source.Length));
        return ranges;
    }

    /// <summary>
    /// Every (line, offset) where the scanner and the lexer disagree on a clean document; empty when the
    /// document does not lex clean (callers assert that first).
    /// </summary>
    internal static IEnumerable<string> Disagreements(string name, string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        if (lexer.Diagnostics.HasErrors)
            yield break;
        var spans = LexerNs.LiteralSpans.Of(tokens);
        var inside = LexerNs.LiteralSpans.LinesStartingInside(source, spans);
        var lines = LineRanges(source);
        for (var k = 0; k < lines.Count; k++)
        {
            if (inside.Contains(k + 1))
                continue;
            var (lineStart, lineEnd) = lines[k];
            var starts = new SortedSet<int> { lineStart };
            foreach (var token in tokens)
            {
                if (token.Position >= lineStart && token.Position < lineEnd)
                    starts.Add(token.Position);
            }
            foreach (var at in starts)
            {
                if (spans.Any(span => span.Start < at && at < span.End))
                    continue;
                var lexerCrosses = spans.Any(span => span.Start >= at && span.Start < lineEnd && span.End > lineEnd);
                var scanner = LexerNs.Lexer.HoldsALiteralSpanningLines(source.AsSpan(at, lineEnd - at));
                if (lexerCrosses != scanner)
                    yield return $"{name} L{k + 1}@{at - lineStart}: lexer={lexerCrosses} scanner={scanner} :: {source.Substring(at, lineEnd - at)}";
            }
            var lexerRemainder = spans.Any(span => span.Start >= lineStart && source.AsSpan(span.Start, span.End - span.Start).IndexOfAny('\n', '\r') >= 0);
            var scannerRemainder = LexerNs.Lexer.HoldsALiteralSpanningLines(source.AsSpan(lineStart));
            if (lexerRemainder != scannerRemainder)
                yield return $"{name} remainder from L{k + 1}: lexer={lexerRemainder} scanner={scannerRemainder}";
        }
    }

    [Fact]
    public void Corpus_ScannerAgreesWithTheLexerOnEveryCleanDocument()
    {
        var root = FixtureRoots.CompilerTests.Path;
        var files = Directory.EnumerateFiles(root, "*.spy", SearchOption.AllDirectories)
            .Where(f => !Sharpy.Compiler.Diagnostics.CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(root, f)))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        var disagreements = new List<string>();
        var clean = 0;
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            var lexer = new LexerNs.Lexer(source);
            lexer.TokenizeAll();
            if (!lexer.Diagnostics.HasErrors)
                clean++;
            disagreements.AddRange(Disagreements(Path.GetRelativePath(root, file), source));
        }
        _output.WriteLine($"SCANNER-DIFF files {files.Count} clean {clean} disagreements {disagreements.Count}");
        foreach (var disagreement in disagreements.Take(200))
            _output.WriteLine("SCANNER-DIFF " + disagreement);

        clean.Should().BeGreaterThan(100, "the corpus is the oracle's population");
        disagreements.Should().BeEmpty();
    }

    /// <summary>Hand rows, each a clean document pinning one arm of the model against the lexer.</summary>
    public static TheoryData<string, string> CleanHandRows => new()
    {
        { "nested triple in a field", "x = f\"{'\"\"\"'}\"\ny = 1\n" },
        { "byte triple", "x = b\"\"\"\nk\n\"\"\"\n" },
        { "rb is a name then a triple", "x = rb\"\"\"\nk\n\"\"\"\n" },
        { "br is a name then a triple", "x = br\"\"\"\nk\n\"\"\"\n" },
        { "t triple", "x = t\"\"\"\nk\n\"\"\"\n" },
        { "dr triple", "x = dr\"\"\"\n    k\n    \"\"\"\n" },
        { "d short then a triple", "x = d\"abc\" + \"\"\"\nk\n\"\"\"\n" },
        { "comment in a field spanning lines", "x = f\"{x # y\n}\"\n" },
        { "triple inside a backtick name", "`a\"\"\"b` = 1\ny = 1\n" },
        { "backtick name with a quote in a field", "y = f\"{`a}'`}\" + 'z'\n" },
        { "triple f-string with a field spanning lines", "x = f\"\"\"{\na}\"\"\"\n" },
        { "lambda colon in a field", "x = f\"{(lambda: 1)()}\"\ny = \"\"\"\nk\n\"\"\"\n" },
        { "dict colon in a field", "x = f\"{ {'a': 1}['a'] }\"\n" },
        { "walrus in a field", "x = f\"{(y:=1)}\"\n" },
        { "!= in a field", "x = f\"{a!=b}\"\n" },
        { "!r conversion then a triple", "x = f\"{x!r}\" + \"\"\"\nk\n\"\"\"\n" },
        { "nested triple f-string in a field then a triple", "x = f\"{f'''a'''}\" + \"\"\"\nk\n\"\"\"\n" },
        { "lone CR line ends", "x = \"\"\"\rk\r\"\"\"\r" },
    };

    [Theory]
    [MemberData(nameof(CleanHandRows))]
    public void HandRow_ScannerAgreesWithTheLexer(string name, string source)
    {
        var lexer = new LexerNs.Lexer(source);
        lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeFalse("{0} must lex clean to be inside the oracle: {1}", name,
            string.Join("; ", lexer.Diagnostics.GetErrors().Select(d => $"{d.Code}@{d.Line}:{d.Column} {d.Message}")));

        Disagreements(name, source).Should().BeEmpty();
    }
}
