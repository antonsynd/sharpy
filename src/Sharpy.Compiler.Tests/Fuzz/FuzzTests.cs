using System;
using System.Collections.Generic;
using Sharpy.Compiler.Logging;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;

namespace Sharpy.Compiler.Tests.Fuzz;

/// <summary>
/// Fuzz testing harness for the Sharpy compiler (Phase 6.3).
/// Runs the compiler on randomly generated inputs and asserts that no
/// unhandled exceptions escape. The compiler should always return a
/// CompilationResult with appropriate diagnostics, never crash.
/// </summary>
public class FuzzTests
{
    private readonly ITestOutputHelper _output;

    public FuzzTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Fuzz the lexer with random token sequences.
    /// The lexer should never throw an unhandled exception.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    [InlineData(2025)]
    [InlineData(9999)]
    public void Lexer_RandomTokens_NeverThrowsUnhandledException(int seed)
    {
        var fuzzer = new SharplyFuzzer(seed);
        var failures = new List<string>();

        for (int i = 0; i < 100; i++)
        {
            var input = fuzzer.GenerateRandomTokens();
            try
            {
                var lexer = new LexerNs.Lexer(input);
                lexer.TokenizeAll();
                // Errors in diagnostics are fine - unhandled exceptions are not
            }
            catch (Exception ex)
            {
                failures.Add($"Seed {seed}, iteration {i}: {ex.GetType().Name}: {ex.Message}\nInput: {Truncate(input)}");
            }
        }

        if (failures.Count > 0)
        {
            _output.WriteLine($"Failures ({failures.Count}/100):");
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Fuzz the lexer with inputs designed to stress edge cases.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(100)]
    [InlineData(256)]
    [InlineData(512)]
    public void Lexer_StressInputs_NeverThrowsUnhandledException(int seed)
    {
        var fuzzer = new SharplyFuzzer(seed);
        var failures = new List<string>();

        for (int i = 0; i < 50; i++)
        {
            var input = fuzzer.GenerateLexerStress();
            try
            {
                var lexer = new LexerNs.Lexer(input);
                lexer.TokenizeAll();
            }
            catch (Exception ex)
            {
                failures.Add($"Seed {seed}, iteration {i}: {ex.GetType().Name}: {ex.Message}\nInput: {Truncate(input)}");
            }
        }

        if (failures.Count > 0)
        {
            _output.WriteLine($"Failures ({failures.Count}/50):");
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Fuzz the full compiler pipeline with valid-looking programs.
    /// The compiler should never throw an unhandled exception.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    [InlineData(2025)]
    [InlineData(9999)]
    public void Compiler_ValidLookingPrograms_NeverThrowsUnhandledException(int seed)
    {
        var fuzzer = new SharplyFuzzer(seed);
        var compiler = new Compiler();
        var failures = new List<string>();

        for (int i = 0; i < 50; i++)
        {
            var input = fuzzer.GenerateValidLooking();
            try
            {
                var result = compiler.Compile(input, "fuzz_test.spy");
                // Success or failure with diagnostics are both fine.
                // We only care that it doesn't throw.
            }
            catch (Exception ex)
            {
                failures.Add($"Seed {seed}, iteration {i}: {ex.GetType().Name}: {ex.Message}\nInput: {Truncate(input)}");
            }
        }

        if (failures.Count > 0)
        {
            _output.WriteLine($"Failures ({failures.Count}/50):");
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Fuzz the full compiler pipeline with intentional syntax errors.
    /// The compiler should report errors via diagnostics, not crash.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    [InlineData(2025)]
    [InlineData(9999)]
    public void Compiler_SyntaxErrors_NeverThrowsUnhandledException(int seed)
    {
        var fuzzer = new SharplyFuzzer(seed);
        var compiler = new Compiler();
        var failures = new List<string>();

        for (int i = 0; i < 50; i++)
        {
            var input = fuzzer.GenerateWithSyntaxErrors();
            try
            {
                var result = compiler.Compile(input, "fuzz_error.spy");
                // Most of these should fail - that's expected
            }
            catch (Exception ex)
            {
                failures.Add($"Seed {seed}, iteration {i}: {ex.GetType().Name}: {ex.Message}\nInput: {Truncate(input)}");
            }
        }

        if (failures.Count > 0)
        {
            _output.WriteLine($"Failures ({failures.Count}/50):");
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Fuzz the full compiler with completely random token sequences.
    /// These are unlikely to be valid programs, but the compiler should
    /// never crash.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    [InlineData(2025)]
    [InlineData(9999)]
    public void Compiler_RandomTokens_NeverThrowsUnhandledException(int seed)
    {
        var fuzzer = new SharplyFuzzer(seed);
        var compiler = new Compiler();
        var failures = new List<string>();

        for (int i = 0; i < 50; i++)
        {
            var input = fuzzer.GenerateRandomTokens();
            try
            {
                var result = compiler.Compile(input, "fuzz_random.spy");
            }
            catch (Exception ex)
            {
                failures.Add($"Seed {seed}, iteration {i}: {ex.GetType().Name}: {ex.Message}\nInput: {Truncate(input)}");
            }
        }

        if (failures.Count > 0)
        {
            _output.WriteLine($"Failures ({failures.Count}/50):");
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Fuzz with edge-case single inputs that have historically been
    /// problematic for compilers: empty string, single newline, etc.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("#")]
    [InlineData("# comment only\n")]
    [InlineData("...")]
    [InlineData("@")]
    [InlineData("(((")]
    [InlineData(")))")]
    [InlineData("[[[")]
    [InlineData("]]]")]
    [InlineData("{{{")]
    [InlineData("}}}")]
    [InlineData("\\")]
    [InlineData("def")]
    [InlineData("class")]
    [InlineData("0x")]
    [InlineData("0b")]
    [InlineData("0o")]
    [InlineData("999999999999999999999999999999")]
    [InlineData("f\"")]
    [InlineData("f\"{")]
    [InlineData("r\"")]
    [InlineData("\"\\")]
    public void Compiler_EdgeCaseInputs_NeverThrowsUnhandledException(string input)
    {
        var compiler = new Compiler();
        try
        {
            compiler.Compile(input, "edge_case.spy");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Compiler threw unhandled {ex.GetType().Name} for input: {Truncate(input)}\n{ex.Message}");
        }
    }

    /// <summary>
    /// Similarly, the lexer alone should handle edge cases gracefully.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("0x")]
    [InlineData("0b")]
    [InlineData("0o")]
    [InlineData("f\"")]
    [InlineData("f\"{")]
    [InlineData("r\"")]
    [InlineData("\"\\")]
    [InlineData("...")]
    [InlineData("\\")]
    public void Lexer_EdgeCaseInputs_NeverThrowsUnhandledException(string input)
    {
        try
        {
            var lexer = new LexerNs.Lexer(input);
            lexer.TokenizeAll();
        }
        catch (Exception ex)
        {
            Assert.Fail($"Lexer threw unhandled {ex.GetType().Name} for input: {Truncate(input)}\n{ex.Message}");
        }
    }

    private static string Truncate(string s, int maxLen = 200)
    {
        if (s.Length <= maxLen)
            return s.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        return s[..maxLen].Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "...";
    }
}
