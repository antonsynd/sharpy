using System.Text;
using CsCheck;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Parser;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class ParserFromStringPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ParserFromStringPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// A generated module paired with the seed its whitespace injection uses.
    ///
    /// <para>The seed comes from CsCheck, so the seed CsCheck PRINTS on failure fully determines
    /// the run and both reproduction and shrinking work. It used to be
    /// <c>source.GetHashCode()</c>, which .NET randomizes per process: the printed seed picked the
    /// same module and then a different injection, so re-running it reproduced the failure only by
    /// luck — measured at 3 of 8 runs on the same seed, tree and binary (#1439). Every flake cost a
    /// triage that concluded "the helper again".</para>
    ///
    /// <para><b>Reproduction verified 2026-08-14</b> the way the issue measured the defect. A
    /// seed-dependent fault was injected into
    /// <see cref="WhitespaceVariation_DoesNotChangeTokenSequence"/> (fire iff
    /// <c>new Random(sample.Seed).Next(4) == 0</c>), the failure's printed seed captured
    /// (<c>0oNYfiuozFN2</c>, shrunk to injection seed 1), and that seed re-run 8 times:
    /// <b>8 of 8 red, every run shrinking to the same injection seed 1</b> — against the 3-of-8
    /// baseline. Probe removed. Shrinking works too, which it could not before: the seed now
    /// determines the injection, so CsCheck can hold it fixed while it minimizes.</para>
    /// </summary>
    private static Gen<(Sharpy.Compiler.Parser.Ast.Module Module, int Seed)> ModuleWithSeed() =>
        Gen.Select(
            Gen.Int[1, 3].SelectMany(fuel => GenSharpy.Module(GenContext.Default with { Fuel = fuel })),
            Gen.Int,
            (module, seed) => (module, seed));

    private static string Print((Sharpy.Compiler.Parser.Ast.Module Module, int Seed) sample)
        => $"seed {sample.Seed}\n{Sharpy.Compiler.Pretty.Unparser.Unparse(sample.Module)}";

    [Fact]
    public void WhitespaceVariation_NeverCrashesParser()
    {
        ModuleWithSeed().Sample(sample =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(sample.Module);
            var modified = InsertRandomWhitespace(source, sample.Seed);

            var lexer = new Sharpy.Compiler.Lexer.Lexer(modified);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
        }, print: Print, iter: 100);
    }

    [Fact]
    public void WhitespaceVariation_DiagnosticsAreDeterministic()
    {
        ModuleWithSeed().Sample(sample =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(sample.Module);
            var modified = InsertBlankLines(source, sample.Seed);

            var diags1 = ParseAndGetDiagnostics(modified);
            var diags2 = ParseAndGetDiagnostics(modified);

            if (diags1.Count != diags2.Count)
                throw new Exception(
                    $"Non-deterministic diagnostics: {diags1.Count} vs {diags2.Count}");

            for (int i = 0; i < diags1.Count; i++)
            {
                if (diags1[i] != diags2[i])
                    throw new Exception(
                        $"Diagnostic #{i} differs: '{diags1[i]}' vs '{diags2[i]}'");
            }
        }, print: Print, iter: 100);
    }

    [Fact]
    public void WhitespaceVariation_DoesNotChangeTokenSequence()
    {
        ModuleWithSeed().Sample(sample =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(sample.Module);
            var modified = InsertRandomWhitespace(source, sample.Seed);

            var originalTokens = GetMeaningfulTokens(source);
            var modifiedTokens = GetMeaningfulTokens(modified);

            if (originalTokens.Count != modifiedTokens.Count)
                throw new Exception(
                    $"Token count changed: {originalTokens.Count} -> {modifiedTokens.Count}\n" +
                    $"Original:\n{source}\n\nModified:\n{modified}");

            for (int i = 0; i < originalTokens.Count; i++)
            {
                if (originalTokens[i] != modifiedTokens[i])
                    throw new Exception(
                        $"Token #{i} changed: {originalTokens[i]} -> {modifiedTokens[i]}\n" +
                        $"Original:\n{source}\n\nModified:\n{modified}");
            }
        }, print: Print, iter: 100);
    }

    /// <summary>
    /// Expands existing spaces that lie OUTSIDE every lexer token — never removes a space, never
    /// inserts where there is none, and leaves leading indentation exactly as it found it.
    ///
    /// <para><b>The lexer says where tokens are; nothing re-scans for them (#1439).</b> This used
    /// to walk each line tracking a single <c>inString</c>/<c>stringChar</c> pair, which cannot
    /// represent a template string whose interpolations contain their own quotes: on
    /// <c>t"{b"abc"}{"abc 123"}{super()}"</c> the scanner's state flipped on the INNER quotes and
    /// it then expanded spaces inside <c>"abc 123"</c>, changing the string's value. The test
    /// failed for the generator's fault, and the report blamed the parser. Lexing the source once
    /// and expanding only what no token covers handles template strings, byte-string prefixes and
    /// nested interpolation quoting by construction, because none of them are special cases here —
    /// they are just spans the lexer already knows.</para>
    ///
    /// <para>Using the lexer to build a generator for a lexer property is sound in this direction:
    /// the property compares token SEQUENCES across two lexings, so a lexer that misreports spans
    /// perturbs both sides and the property still fails. It cannot self-certify.</para>
    ///
    /// <para>The RNG is seeded by the caller from a CsCheck-generated value — see
    /// <see cref="ModuleWithSeed"/> for why it is no longer <c>source.GetHashCode()</c>.</para>
    /// </summary>
    private static string InsertRandomWhitespace(string source, int seed)
    {
        var rng = new Random(seed);
        var covered = TokenCoverage(source);
        var result = new StringBuilder(source.Length);
        var inIndentation = true;

        for (int i = 0; i < source.Length; i++)
        {
            var c = source[i];
            result.Append(c);

            if (c == '\n')
            {
                inIndentation = true;
                continue;
            }

            if (c is not (' ' or '\t'))
            {
                inIndentation = false;
                continue;
            }

            // Leading indentation is structural — expanding it would change INDENT/DEDENT and the
            // property would be testing something else entirely.
            if (c == ' ' && !inIndentation && !covered[i] && rng.Next(3) == 0)
                result.Append(' ', rng.Next(1, 4));
        }

        return result.ToString();
    }

    /// <summary>
    /// Marks every source offset that lies inside some token's span, computed from each token's
    /// line/column start and its SOURCE length (<c>Token.Length</c>, which is
    /// <c>SourceLength ?? Value.Length</c> — a string token's value is shorter than its source, so
    /// using <c>Value.Length</c> would leave its closing quote uncovered).
    ///
    /// <para>A lexer exception propagates deliberately. Swallowing it would silently degrade this
    /// helper to "inject nothing", and a whitespace-variation property that injects no whitespace
    /// passes vacuously — the failure mode where a guard reports green because it stopped
    /// testing.</para>
    /// </summary>
    private static bool[] TokenCoverage(string source)
    {
        var covered = new bool[source.Length];
        var lineStarts = new List<int> { 0 };
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
                lineStarts.Add(i + 1);
        }

        foreach (var token in new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll())
        {
            if (token.Line < 1 || token.Line > lineStarts.Count || token.Column < 1)
                continue;

            var start = lineStarts[token.Line - 1] + token.Column - 1;
            var end = Math.Min(source.Length, start + Math.Max(token.Length, 0));
            for (int i = Math.Max(0, start); i < end; i++)
                covered[i] = true;
        }

        return covered;
    }

    /// <summary>
    /// Inserts blank lines between top-level statements (lines starting at column 0).
    /// This tests that the parser correctly handles extra blank lines between statements.
    /// The RNG is seeded by the caller from a CsCheck-generated value — see
    /// <see cref="ModuleWithSeed"/> for why it is no longer <c>source.GetHashCode()</c>.
    /// </summary>
    private static string InsertBlankLines(string source, int seed)
    {
        var rng = new Random(seed);
        var lines = source.Split('\n');
        var result = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            result.Add(lines[i]);

            // After a non-empty line with no indentation, maybe add a blank line
            if (i < lines.Length - 1
                && lines[i].Length > 0
                && !char.IsWhiteSpace(lines[i][0])
                && rng.Next(2) == 0)
            {
                result.Add("");
            }
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Extracts the sequence of meaningful (non-whitespace, non-structural) tokens
    /// from source code. Filters out Newline, Indent, Dedent, and Eof tokens so
    /// that whitespace-only changes can be verified to not affect the token stream.
    /// </summary>
    private static List<(TokenType Type, string Value)> GetMeaningfulTokens(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
        List<Token> tokens;

        try
        {
            tokens = lexer.TokenizeAll();
        }
        catch
        {
            // If lexing fails, return empty list; the caller will see a count mismatch
            return [];
        }

        return tokens
            .Where(t => t.Type != TokenType.Newline
                     && t.Type != TokenType.Indent
                     && t.Type != TokenType.Dedent
                     && t.Type != TokenType.Eof)
            .Select(t => (t.Type, t.Value))
            .ToList();
    }

    /// <summary>
    /// The shape #1439 was filed on, pinned as a fixed input instead of left to the generator's
    /// luck: a template string whose interpolations contain their own quotes, including a
    /// byte-string prefix and a space INSIDE a nested literal. The hand quote-scanner this helper
    /// used to carry flipped its <c>inString</c> state on the inner quotes and expanded that
    /// space, changing the program's meaning — a generator corrupting its own input and reporting
    /// it as a parser bug.
    ///
    /// <para>Two hundred seeds, because one seed proves nothing about an RNG-driven helper: the
    /// old scanner corrupted this input only on the fraction of seeds that rolled an expansion at
    /// that offset.</para>
    ///
    /// <para><b>Mutation, run 2026-08-14.</b> Replacing <see cref="TokenCoverage"/>'s body with
    /// the pre-fix single-state hand quote-scanner turns this test RED at <b>seed 2</b>, printing
    /// the corruption verbatim: <c>x = t"{b"abc"}{"abc 123"}{super()}"</c> became
    /// <c>x = t"{b"abc"}{"abc  123"}{super()}"</c>. Reverted; the token-span version is green over
    /// all 200 seeds.</para>
    ///
    /// <para><b>And what the same run established about the random properties:</b> under that
    /// mutation <c>WhitespaceVariation_DoesNotChangeTokenSequence</c> stayed GREEN. The generator
    /// does not reliably produce nested-quote template strings, so the randomized property cannot
    /// be relied on to catch this — which is the whole reason the shape is pinned here as a fixed
    /// input rather than left to the corpus.</para>
    /// </summary>
    [Fact]
    public void InsertRandomWhitespace_LeavesNestedQuoteTemplateStringsIntact()
    {
        const string Source =
            "class C:\n"
            + "    def m(self) -> None:\n"
            + "        x = t\"{b\"abc\"}{\"abc 123\"}{super()}\"\n"
            + "        print(x)\n";

        var expected = GetMeaningfulTokens(Source);

        // The generator is only meaningful if the input lexes to something in the first place.
        Assert.NotEmpty(expected);
        Assert.Contains(expected, t => t.Value == "abc 123");

        for (int seed = 0; seed < 200; seed++)
        {
            var modified = InsertRandomWhitespace(Source, seed);
            var actual = GetMeaningfulTokens(modified);

            Assert.True(
                expected.SequenceEqual(actual),
                $"seed {seed} changed the token stream — whitespace injection corrupted string "
                + $"content.\nOriginal:\n{Source}\nModified:\n{modified}");
        }
    }

    /// <summary>
    /// The positive control for the test above: whitespace injection must actually INJECT. A
    /// helper that expands nothing keeps every token stream identical and passes every
    /// whitespace-variation property vacuously, which is the failure mode a
    /// "nothing changed" assertion cannot distinguish from success on its own.
    /// </summary>
    [Fact]
    public void InsertRandomWhitespace_ActuallyInjectsOutsideTokens()
    {
        const string Source = "x = 1 + 2\ny = 3 + 4\n";

        var injected = Enumerable.Range(0, 50)
            .Select(seed => InsertRandomWhitespace(Source, seed))
            .Where(modified => modified != Source)
            .ToList();

        Assert.NotEmpty(injected);
        foreach (var modified in injected)
            Assert.Equal(GetMeaningfulTokens(Source), GetMeaningfulTokens(modified));
    }

    private static List<string> ParseAndGetDiagnostics(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        _ = parser.ParseModule();
        return parser.Diagnostics.GetAll().Select(d => d.ToString()!).ToList();
    }
}
