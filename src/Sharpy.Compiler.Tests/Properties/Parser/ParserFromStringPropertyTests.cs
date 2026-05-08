using System.Text;
using CsCheck;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Parser;

[Trait("Category", "Property")]
public class ParserFromStringPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ParserFromStringPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void WhitespaceVariation_NeverCrashesParser()
    {
        Gen.Int[1, 3].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var modified = InsertRandomWhitespace(source);

            var lexer = new Sharpy.Compiler.Lexer.Lexer(modified);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }

    [Fact]
    public void WhitespaceVariation_DiagnosticsAreDeterministic()
    {
        Gen.Int[1, 3].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var modified = InsertBlankLines(source);

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
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }

    [Fact]
    public void WhitespaceVariation_DoesNotChangeTokenSequence()
    {
        Gen.Int[1, 3].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var modified = InsertRandomWhitespace(source);

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
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);
    }

    /// <summary>
    /// Inserts random extra spaces between tokens on the same line. Only expands
    /// existing whitespace gaps (never removes spaces or inserts where there is none),
    /// preserves leading indentation exactly, and skips content inside string literals.
    /// Uses a deterministic RNG seeded from the source hash for reproducibility.
    /// </summary>
    private static string InsertRandomWhitespace(string source)
    {
        var rng = new Random(source.GetHashCode());
        var lines = source.Split('\n');
        var result = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();
            if (trimmed.Length == 0)
            {
                result.Add(line);
                continue;
            }

            // Find leading indentation length
            int indent = trimmed.Length - trimmed.TrimStart().Length;
            var content = trimmed[indent..];

            var sb = new StringBuilder();
            sb.Append(trimmed[..indent]); // preserve indentation exactly

            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];

                // Track string literal boundaries (skip escaped quotes)
                if (!inString && (c == '"' || c == '\''))
                {
                    inString = true;
                    stringChar = c;
                }
                else if (inString && c == stringChar && (i == 0 || content[i - 1] != '\\'))
                {
                    inString = false;
                }

                sb.Append(c);

                // Only expand existing spaces that are outside string literals
                if (!inString && c == ' ' && rng.Next(3) == 0)
                {
                    sb.Append(' ', rng.Next(1, 4));
                }
            }

            result.Add(sb.ToString());
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Inserts blank lines between top-level statements (lines starting at column 0).
    /// This tests that the parser correctly handles extra blank lines between statements.
    /// Uses a deterministic RNG seeded from the source hash for reproducibility.
    /// </summary>
    private static string InsertBlankLines(string source)
    {
        var rng = new Random(source.GetHashCode());
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

    private static List<string> ParseAndGetDiagnostics(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        _ = parser.ParseModule();
        return parser.Diagnostics.GetAll().Select(d => d.ToString()!).ToList();
    }
}
