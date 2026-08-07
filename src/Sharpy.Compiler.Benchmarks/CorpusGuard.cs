using Sharpy.Compiler.Diagnostics;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyParser = Sharpy.Compiler.Parser.Parser;
using Sharpy.Compiler.Lexer;

namespace Sharpy.Compiler.Benchmarks;

/// <summary>
/// Setup-time assertions that a benchmark's input actually reaches the stage the benchmark claims
/// to time (#1224).
/// </summary>
/// <remarks>
/// A <see cref="Compiler"/> that rejects its input returns a failed <see cref="CompilationResult"/>
/// early rather than throwing, so an invalid corpus member produces a perfectly ordinary-looking
/// number that times a prefix of the pipeline under a label claiming all of it. That is not
/// hypothetical: the LSP latency baseline recorded a 227-line "full analysis" row for months that
/// stopped at the parser (#1140), and this project's own combined-corpus throughput row timed a
/// semantic-analysis failure (#1224).
/// <para>
/// These run once per harness family in <c>GlobalSetup</c>, not per benchmark method: the guard
/// belongs to whatever shared path reads the input. Throwing from <c>GlobalSetup</c> fails the
/// benchmark run outright, which is the point — a silently-failing input must not be able to
/// produce a number.
/// </para>
/// </remarks>
internal static class CorpusGuard
{
    /// <summary>
    /// Asserts <paramref name="source"/> compiles cleanly through the whole pipeline, in the same
    /// configuration the benchmark method uses (<c>new Compiler()</c>, no references).
    /// </summary>
    public static void AssertCompiles(string source, string label)
    {
        var result = new Compiler().Compile(source, label);
        if (result.Success)
            return;

        throw new InvalidOperationException(
            $"Benchmark input '{label}' does not compile, so timing it would measure an early "
            + "return rather than a compilation (#1224). Errors: "
            + string.Join(" | ", result.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.Message}")));
    }

    /// <summary>
    /// Asserts <paramref name="source"/> tokenizes cleanly — the stage a lexer benchmark times.
    /// Semantic validity is deliberately not required here: a lexer row over a source that does not
    /// type-check still measures exactly the tokenization it claims to.
    /// </summary>
    public static IReadOnlyList<Token> AssertTokenizes(string source, string label)
    {
        var tokens = new SharpyLexer(source).TokenizeAll();
        if (tokens.Count == 0 || tokens[^1].Type != TokenType.Eof)
        {
            throw new InvalidOperationException(
                $"Benchmark input '{label}' did not tokenize to EOF, so the lexer row would measure "
                + "a truncated token stream (#1224).");
        }

        return tokens;
    }

    /// <summary>
    /// Asserts <paramref name="tokens"/> parse cleanly — the stage a parser benchmark times. As
    /// with <see cref="AssertTokenizes"/>, semantic validity is not required.
    /// </summary>
    public static void AssertParses(IReadOnlyList<Token> tokens, string label)
    {
        var parser = new SharpyParser(tokens.ToList());
        var module = parser.ParseModule();
        var errors = parser.Diagnostics.GetErrors().ToList();
        if (!module.Body.Any() || errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Benchmark input '{label}' does not parse, so the parser row would measure error "
                + "recovery rather than a parse (#1224). Errors: "
                + string.Join(" | ", errors.Select(d => $"{d.Code} {d.Message}")));
        }
    }
}
