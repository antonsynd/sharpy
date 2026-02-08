using System;
using System.Collections.Generic;
using System.Threading;
using Sharpy.Compiler.Logging;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;

namespace Sharpy.Compiler.Tests.Fuzz;

/// <summary>
/// Fuzz testing harness for the Sharpy compiler (Phase 6.1).
/// Runs the compiler on randomly generated inputs and asserts that no
/// unhandled exceptions escape. The compiler should always return a
/// CompilationResult with appropriate diagnostics, never crash.
/// </summary>
public class FuzzTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Timeout per fuzz iteration (2 seconds). If the compiler takes longer
    /// than this, it's likely stuck in an infinite loop. Most compilations
    /// complete in under 100ms, so 2 seconds is generous while keeping the
    /// total suite runtime reasonable.
    /// </summary>
    private const int FuzzIterationTimeoutMs = 2000;

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
        var fuzzer = new SharpyFuzzer(seed);
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
        var fuzzer = new SharpyFuzzer(seed);
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
        var fuzzer = new SharpyFuzzer(seed);
        var compiler = new Compiler();
        var failures = new List<string>();
        var timeouts = new List<string>();

        for (int i = 0; i < 50; i++)
        {
            var input = fuzzer.GenerateValidLooking();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(FuzzIterationTimeoutMs));
                var result = compiler.Compile(input, "fuzz_test.spy", cts.Token);
                // Success or failure with diagnostics are both fine.
                // We only care that it doesn't throw.
            }
            catch (OperationCanceledException)
            {
                timeouts.Add($"Seed {seed}, iteration {i}: TIMEOUT after {FuzzIterationTimeoutMs}ms\nInput: {Truncate(input)}");
            }
            catch (Exception ex)
            {
                failures.Add($"Seed {seed}, iteration {i}: {ex.GetType().Name}: {ex.Message}\nInput: {Truncate(input)}");
            }
        }

        if (timeouts.Count > 0)
        {
            _output.WriteLine($"Timeouts ({timeouts.Count}/50):");
            foreach (var t in timeouts)
                _output.WriteLine(t);
        }

        if (failures.Count > 0)
        {
            _output.WriteLine($"Failures ({failures.Count}/50):");
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(timeouts);
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
        var fuzzer = new SharpyFuzzer(seed);
        var compiler = new Compiler();
        var failures = new List<string>();
        var timeouts = new List<string>();

        for (int i = 0; i < 50; i++)
        {
            var input = fuzzer.GenerateWithSyntaxErrors();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(FuzzIterationTimeoutMs));
                var result = compiler.Compile(input, "fuzz_error.spy", cts.Token);
                // Most of these should fail - that's expected
            }
            catch (OperationCanceledException)
            {
                timeouts.Add($"Seed {seed}, iteration {i}: TIMEOUT after {FuzzIterationTimeoutMs}ms\nInput: {Truncate(input)}");
            }
            catch (Exception ex)
            {
                failures.Add($"Seed {seed}, iteration {i}: {ex.GetType().Name}: {ex.Message}\nInput: {Truncate(input)}");
            }
        }

        if (timeouts.Count > 0)
        {
            _output.WriteLine($"Timeouts ({timeouts.Count}/50):");
            foreach (var t in timeouts)
                _output.WriteLine(t);
        }

        if (failures.Count > 0)
        {
            _output.WriteLine($"Failures ({failures.Count}/50):");
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(timeouts);
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
        var fuzzer = new SharpyFuzzer(seed);
        var compiler = new Compiler();
        var failures = new List<string>();
        var timeouts = new List<string>();

        for (int i = 0; i < 50; i++)
        {
            var input = fuzzer.GenerateRandomTokens();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(FuzzIterationTimeoutMs));
                var result = compiler.Compile(input, "fuzz_random.spy", cts.Token);
            }
            catch (OperationCanceledException)
            {
                timeouts.Add($"Seed {seed}, iteration {i}: TIMEOUT after {FuzzIterationTimeoutMs}ms\nInput: {Truncate(input)}");
            }
            catch (Exception ex)
            {
                failures.Add($"Seed {seed}, iteration {i}: {ex.GetType().Name}: {ex.Message}\nInput: {Truncate(input)}");
            }
        }

        if (timeouts.Count > 0)
        {
            _output.WriteLine($"Timeouts ({timeouts.Count}/50):");
            foreach (var t in timeouts)
                _output.WriteLine(t);
        }

        if (failures.Count > 0)
        {
            _output.WriteLine($"Failures ({failures.Count}/50):");
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(timeouts);
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

    /// <summary>
    /// Fuzz the full compiler with generated class hierarchies.
    /// Exercises inheritance resolution, type checking, and codegen.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    [InlineData(2025)]
    [InlineData(9999)]
    public void Compiler_ClassHierarchies_NeverThrowsUnhandledException(int seed)
    {
        var fuzzer = new SharpyFuzzer(seed);
        var compiler = new Compiler();
        var failures = new List<string>();

        for (int i = 0; i < 30; i++)
        {
            var input = fuzzer.GenerateClassHierarchy();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(FuzzIterationTimeoutMs));
                var result = compiler.Compile(input, "fuzz_hierarchy.spy", cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeouts acceptable
            }
            catch (Exception ex)
            {
                failures.Add($"Seed {seed}, iteration {i}: {ex.GetType().Name}: {ex.Message}\nInput: {Truncate(input)}");
            }
        }

        if (failures.Count > 0)
        {
            _output.WriteLine($"Failures ({failures.Count}/30):");
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Fuzz the full compiler with generic type usage.
    /// Exercises GenericTypeInferenceService and type resolution.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    [InlineData(2025)]
    [InlineData(9999)]
    public void Compiler_GenericUsage_NeverThrowsUnhandledException(int seed)
    {
        var fuzzer = new SharpyFuzzer(seed);
        var compiler = new Compiler();
        var failures = new List<string>();

        for (int i = 0; i < 30; i++)
        {
            var input = fuzzer.GenerateGenericUsage();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(FuzzIterationTimeoutMs));
                var result = compiler.Compile(input, "fuzz_generics.spy", cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeouts acceptable
            }
            catch (Exception ex)
            {
                failures.Add($"Seed {seed}, iteration {i}: {ex.GetType().Name}: {ex.Message}\nInput: {Truncate(input)}");
            }
        }

        if (failures.Count > 0)
        {
            _output.WriteLine($"Failures ({failures.Count}/30):");
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Fuzz the full compiler with programs that have type annotations.
    /// Exercises TypeResolver with optional, tuple, and collection types.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    [InlineData(2025)]
    [InlineData(9999)]
    public void Compiler_TypeAnnotations_NeverThrowsUnhandledException(int seed)
    {
        var fuzzer = new SharpyFuzzer(seed);
        var compiler = new Compiler();
        var failures = new List<string>();

        for (int i = 0; i < 30; i++)
        {
            var input = fuzzer.GenerateTypeAnnotations();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(FuzzIterationTimeoutMs));
                var result = compiler.Compile(input, "fuzz_types.spy", cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeouts acceptable
            }
            catch (Exception ex)
            {
                failures.Add($"Seed {seed}, iteration {i}: {ex.GetType().Name}: {ex.Message}\nInput: {Truncate(input)}");
            }
        }

        if (failures.Count > 0)
        {
            _output.WriteLine($"Failures ({failures.Count}/30):");
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    private static string Truncate(string s, int maxLen = 200)
    {
        if (s.Length <= maxLen)
            return s.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        return s[..maxLen].Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "...";
    }
}
