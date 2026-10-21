using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Sharpy.TestInfrastructure.Formatting;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Lexing;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// The corpus position instrument (#2300 rows C2 + C3 + C6 at the corpus scale, P68 Phase 2). Over every fixture
/// under <see cref="FixtureRoots.All"/> (build copies excluded, as the literal-loss differential does), each file is
/// lexed in nine twins — line ending {as-is, <see cref="FormatterTwins.Crlf"/>, <see cref="FormatterTwins.Cr"/>} ×
/// end of file {as-is, the trailing line break stripped, one extra trailing line break} — and every twin must:
/// <list type="bullet">
/// <item>not throw (only the lexer's own recorded diagnostics end a lex);</item>
/// <item>reconstruct: <see cref="TokenPositionReconstruction.Violations(string, LexerNs.Lexer, IReadOnlyList{LexerNs.Token})"/>
/// empty — every token's (line, column, position, length) maps back to its source slice;</item>
/// <item>agree with the as-is file on the token (Type, Value) sequence, the lexer diagnostics' (Code, Line, Column)
/// multiset, <see cref="LexerNs.Lexer.LiteralLoss"/>, the recovery resumes and their continued/possible-header
/// subsets (as LINE numbers — offsets move with the line break) and <see cref="LexerNs.Lexer.BracketLeftOpenAtLine"/>.</item>
/// </list>
/// The line-ending twins are compared exactly. The end-of-file twins are compared with three stated normalizations,
/// each the end of the source moving and nothing else: a <c>Newline</c> followed only by <c>Dedent</c>/<c>Eof</c> is
/// dropped (a stripped break removes it, an extra one adds it to a file without one; <c>Newline</c>'s value is always
/// <c>"\n"</c>); a diagnostic positioned in the trailing line breaks or at the source end reads <c>END</c> (an
/// unterminated literal is reported where the source ends, which a stripped break moves up a line); and a recovery
/// resume in the trailing line breaks or at the source end is dropped (an abort on the last line of a file with no
/// final break stops <see cref="LexerNs.Lexer.TokenizeAll"/> without a recovery). A source whose text ends in a
/// backslash is compared across its line-ending twins only: its trailing break is the backslash's operand (a
/// continuation with one, SPY0016 without), so its EOF twins are different programs — the EOF roster's
/// <c>EndingIsOperand</c> rule; those twins must still not throw and must reconstruct.
/// <para>
/// Expected 0-red at base: this is an instrument for a tests-only tracker, and its mutation record (the commit body:
/// a dropped <c>_column</c> advance after <c>&gt;&gt;</c>, <c>ReadLiteralName</c>'s <c>sourceLength:</c> removed,
/// <c>ConsumeLineBreak</c>'s <c>\r\n</c> two-step reduced to one) is what shows it is not vacuous. No GapDiscovery
/// trait: the Stryker filter runs the Lexer namespaces, and this must be inside it.
/// </para>
/// <para>
/// <c>SHARPY_DUMP_TOKENS=&lt;dir&gt;</c> (test-only; set it on one filtered invocation's command line, never in a
/// test) writes each file's as-is token dump and a SHA-256 per twin under that directory — the before/after
/// direction record for a behaviour-neutral lexer edit (P68 Design Decision 8: <c>diff -r</c> must be empty).
/// </para>
/// </summary>
public class LexerCorpusTwinTests
{
    private const int FileFloor = 3_000;   // 4,053 @ 220259b51 (3,809 Compiler + 244 Stdlib), measured
    private const int CleanFloor = 2_500;  // see the CORPUS-TWIN line for the measured count

    private static readonly string[] LineEndings = { "as-is", "crlf", "cr" };
    private static readonly string[] Eofs = { "as-is", "stripped", "extra" };

    private readonly ITestOutputHelper _output;

    public LexerCorpusTwinTests(ITestOutputHelper output) => _output = output;

    private sealed record Lexed(string Source, LexerNs.Lexer Lexer, List<LexerNs.Token> Tokens);

    private static (Lexed? Lexed, Exception? Thrown) Lex(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        try
        {
            return (new Lexed(source, lexer, lexer.TokenizeAll()), null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    internal static string EofTwin(string source, string eof) => eof switch
    {
        "stripped" => source.EndsWith("\r\n", StringComparison.Ordinal) ? source[..^2]
            : source.EndsWith('\n') || source.EndsWith('\r') ? source[..^1]
            : source,
        "extra" => source + "\n",
        _ => source,
    };

    internal static string LineEndingTwin(string source, string lineEnding) => lineEnding switch
    {
        "crlf" => FormatterTwins.Crlf(source),
        "cr" => FormatterTwins.Cr(source),
        _ => source,
    };

    /// <summary>Where the source's trailing line breaks start: an offset at or past it is "the end".</summary>
    private static int ContentEnd(string source) => source.TrimEnd('\r', '\n').Length;

    private static int LineOf(IReadOnlyList<int> lineStarts, int offset)
    {
        var line = 0;
        while (line < lineStarts.Count && lineStarts[line] <= offset)
            line++;
        return line;
    }

    /// <summary>What a twin must agree on, as comparable strings; <paramref name="atEnd"/> applies the EOF normalizations.</summary>
    private sealed record Signature(List<string> Tokens, List<string> Diagnostics, string Facts);

    private static Signature Sign(Lexed lexed, bool atEnd)
    {
        var tokens = lexed.Tokens.Select(t => $"{t.Type}|{t.Value}").ToList();
        if (atEnd)
        {
            var newline = lexed.Tokens.FindLastIndex(t => t.Type == TokenType.Newline);
            if (newline >= 0 && lexed.Tokens.Skip(newline + 1).All(t => t.Type is TokenType.Dedent or TokenType.Eof))
                tokens.RemoveAt(newline);
        }

        var lineStarts = TokenPositionReconstruction.LineStarts(lexed.Source);
        var contentEnd = ContentEnd(lexed.Source);
        var diagnostics = lexed.Lexer.Diagnostics.GetAll().Select(d =>
        {
            if (atEnd && d.Line is { } line && d.Column is { } column && line >= 1 && line <= lineStarts.Count
                && lineStarts[line - 1] + column - 1 >= contentEnd)
                return $"{d.Code}@END";
            return $"{d.Code}@{d.Line}:{d.Column}";
        }).OrderBy(s => s, StringComparer.Ordinal).ToList();

        string Lines(IEnumerable<int> offsets) => string.Join(",", offsets
            .Where(offset => !atEnd || offset < contentEnd)
            .Select(offset => LineOf(lineStarts, offset)).OrderBy(l => l));

        var lexer = lexed.Lexer;
        var facts = $"loss={lexer.LiteralLoss} resumes=[{Lines(lexer.RecoveryResumes)}] "
            + $"continued=[{Lines(lexer.RecoveryResumesAfterAContinuedLine)}] header=[{Lines(lexer.RecoveryResumesAfterAPossibleHeader)}] "
            + $"bracketOpenAt={lexer.BracketLeftOpenAtLine?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}";
        return new Signature(tokens, diagnostics, facts);
    }

    private static IEnumerable<string> Disagreements(string where, Signature expected, Signature actual)
    {
        if (!expected.Tokens.SequenceEqual(actual.Tokens))
        {
            var at = 0;
            while (at < expected.Tokens.Count && at < actual.Tokens.Count && expected.Tokens[at] == actual.Tokens[at])
                at++;
            yield return $"TOKENS {where}: first difference at #{at}: as-is {Shown(expected.Tokens, at)} twin {Shown(actual.Tokens, at)} "
                + $"(counts {expected.Tokens.Count}/{actual.Tokens.Count})";
        }
        if (!expected.Diagnostics.SequenceEqual(actual.Diagnostics))
            yield return $"DIAGNOSTICS {where}: as-is [{string.Join(", ", expected.Diagnostics)}] twin [{string.Join(", ", actual.Diagnostics)}]";
        if (expected.Facts != actual.Facts)
            yield return $"FACTS {where}: as-is {expected.Facts} twin {actual.Facts}";
    }

    private static string Shown(List<string> tokens, int at) =>
        at < tokens.Count ? $"'{tokens[at].Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal)}'" : "<end>";

    private static string Dump(Lexed lexed)
    {
        var text = new StringBuilder();
        foreach (var t in lexed.Tokens)
            text.Append(t.Type).Append('|').Append(Escape(t.Value)).Append('|').Append(t.Line).Append(':').Append(t.Column)
                .Append('@').Append(t.Position).Append('+').Append(t.Length).Append('\n');
        foreach (var d in lexed.Lexer.Diagnostics.GetAll())
            text.Append("DIAG|").Append(d.Code).Append('|').Append(d.Line).Append(':').Append(d.Column).Append('|').Append(Escape(d.Message)).Append('\n');
        var lexer = lexed.Lexer;
        text.Append("FACTS|loss=").Append(lexer.LiteralLoss)
            .Append("|resumes=").Append(string.Join(",", lexer.RecoveryResumes))
            .Append("|continued=").Append(string.Join(",", lexer.RecoveryResumesAfterAContinuedLine.OrderBy(o => o)))
            .Append("|header=").Append(string.Join(",", lexer.RecoveryResumesAfterAPossibleHeader.OrderBy(o => o)))
            .Append("|bracketOpenAt=").Append(lexer.BracketLeftOpenAtLine).Append('\n');
        return text.ToString();
    }

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);

    internal static List<(string Name, string Path)> Population()
    {
        var files = new List<(string, string)>();
        foreach (var root in FixtureRoots.All)
        {
            files.AddRange(Directory.EnumerateFiles(root.Path, "*.spy", SearchOption.AllDirectories)
                .Select(f => (Relative: Path.GetRelativePath(root.Path, f), Full: f))
                .Where(f => !Sharpy.Compiler.Diagnostics.CrashBundleWriter.IsNonSourceSegment(f.Relative))
                .OrderBy(f => f.Relative, StringComparer.Ordinal)
                .Select(f => (root.Label.Length == 0 ? f.Relative : $"{root.Label}/{f.Relative}", f.Full)));
        }
        return files;
    }

    [Fact]
    public void Corpus_PositionsReconstruct_InEveryLineEndingAndEofTwin()
    {
        var clock = Stopwatch.StartNew();
        var dumpRoot = Environment.GetEnvironmentVariable("SHARPY_DUMP_TOKENS");
        var files = Population();
        var violations = new List<(string Scope, string Violation)>();
        var clean = 0;
        var operandEnds = 0;
        long tokens = 0;

        foreach (var (name, path) in files)
        {
            var original = File.ReadAllText(path);
            var (reference, thrown) = Lex(original);
            if (reference == null)
            {
                violations.Add(($"corpus {name}", $"THROW {thrown!.GetType().Name} {name} [as-is/as-is]: {thrown.Message}"));
                continue;
            }
            if (!reference.Lexer.Diagnostics.HasErrors)
                clean++;
            // A source whose text ends in a backslash reads its trailing break as the backslash's operand (a
            // continuation; none or spaces = SPY0016): its EOF twins are different programs, so only its line-ending
            // twins are compared — every twin must still not throw and must reconstruct (the roster's rule).
            var endingIsOperand = original.TrimEnd('\r', '\n').EndsWith('\\');
            if (endingIsOperand)
                operandEnds++;
            var exact = Sign(reference, atEnd: false);
            var atEnd = Sign(reference, atEnd: true);
            var dump = dumpRoot == null ? null : new StringBuilder();

            foreach (var eof in Eofs)
            {
                foreach (var lineEnding in LineEndings)
                {
                    var twin = $"{lineEnding}/{eof}";
                    var source = LineEndingTwin(EofTwin(original, eof), lineEnding);
                    var (lexed, error) = eof == "as-is" && lineEnding == "as-is" ? (reference, (Exception?)null) : Lex(source);
                    if (lexed == null)
                    {
                        violations.Add(($"corpus {name}", $"THROW {error!.GetType().Name} {name} [{twin}]: {error.Message}"));
                        dump?.Append(twin).Append(" THROW ").Append(error.GetType().Name).Append('\n');
                        continue;
                    }
                    tokens += lexed.Tokens.Count;

                    violations.AddRange(TokenPositionReconstruction.Violations(source, lexed.Lexer, lexed.Tokens)
                        .Select(v => ($"corpus {name}", $"{v} :: {name} [{twin}]")));
                    var expected = eof == "as-is" ? exact : atEnd;
                    if (eof == "as-is" || !endingIsOperand)
                        violations.AddRange(Disagreements($"{name} [{twin}]", expected, Sign(lexed, atEnd: eof != "as-is")).Select(v => ($"corpus {name}", v)));

                    if (dump != null)
                    {
                        var text = Dump(lexed);
                        if (twin == "as-is/as-is")
                            dump.Insert(0, text);
                        dump.Append(twin).Append(" sha256 ").Append(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))).Append('\n');
                    }
                }
            }

            if (dump != null)
            {
                var target = Path.Combine(dumpRoot!, name + ".tokens");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, dump.ToString());
            }
        }

        var (rows, malformed) = LexerPositionAllowlist.Read();
        var (remaining, stale, allowlisted) = LexerPositionAllowlist.Apply(rows, "corpus", violations);
        var failures = malformed.Concat(stale).Concat(remaining).ToList();
        _output.WriteLine($"CORPUS-TWIN files {files.Count} clean {clean} tokens {tokens} twins 9 backslashEnds {operandEnds} "
            + $"violations {remaining.Count} allowlisted {allowlisted} elapsed {clock.Elapsed.TotalSeconds:F1}s");
        foreach (var failure in failures.Take(200))
            _output.WriteLine("CORPUS-TWIN " + failure);

        files.Count.Should().BeGreaterThanOrEqualTo(FileFloor, "the corpus is the instrument's population");
        clean.Should().BeGreaterThanOrEqualTo(CleanFloor, "most of the corpus must lex clean for the reconstruction to bite");
        failures.Should().BeEmpty("a position defect outside the contract is a findings-ledger line and a row in {0} citing its issue — never a changed rule",
            LexerPositionAllowlist.FileName);
    }

    [Theory]
    [InlineData("x = 1\n", "stripped", "x = 1")]
    [InlineData("x = 1\r\n", "stripped", "x = 1")]
    [InlineData("x = 1\r", "stripped", "x = 1")]
    [InlineData("x = 1", "stripped", "x = 1")]
    [InlineData("x = 1\n\n", "stripped", "x = 1\n")]
    [InlineData("x = 1\n", "extra", "x = 1\n\n")]
    [InlineData("x = 1", "extra", "x = 1\n")]
    public void EofTwin_MovesOnlyTheEnd(string source, string eof, string expected) =>
        EofTwin(source, eof).Should().Be(expected);

    [Fact]
    public void EofTwins_AreDiscriminating()
    {
        // Positive control for the comparison: a twin that changes a token (not only the end) is a disagreement.
        var (reference, _) = Lex("x = 1\ny = 2\n");
        var (changed, _) = Lex("x = 1\ny = 3\n");
        Disagreements("control", Sign(reference!, atEnd: true), Sign(changed!, atEnd: true)).Should().ContainSingle()
            .Which.Should().StartWith("TOKENS");

        // ... and a diagnostic that moves within the content is one too.
        var (early, _) = Lex("x = $\ny = 2\n");
        var (late, _) = Lex("x = 1\ny = $\n");
        Disagreements("control", Sign(early!, atEnd: true), Sign(late!, atEnd: true))
            .Should().Contain(d => d.StartsWith("DIAGNOSTICS", StringComparison.Ordinal));
    }
}
