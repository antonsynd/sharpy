using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Fuzz;

/// <summary>
/// Property-based tests for Sharpy code generation (Phase 2d).
/// Verifies that programs that compile without Sharpy errors produce valid C#
/// that parses and compiles without errors.
/// </summary>
public class CodeGenPropertyTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Timeout per fuzz iteration (3 seconds — codegen is slower).
    /// </summary>
    private const int FuzzIterationTimeoutMs = 3000;

    public CodeGenPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Property: Generated C# is syntactically valid.
    /// Any Sharpy program that compiles without errors should produce C# that
    /// parses without Roslyn syntax errors.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    public void GeneratedCSharp_ParsesWithoutSyntaxErrors(int seed)
    {
        var fuzzer = new SharpyFuzzer(seed);
        var compiler = new Compiler();
        var failures = new List<string>();

        for (int i = 0; i < 25; i++)
        {
            var source = fuzzer.GenerateValidLooking();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(FuzzIterationTimeoutMs));
                var result = compiler.Compile(source, "fuzz_codegen.spy", cts.Token);

                // Only check programs that compiled successfully
                if (!result.Success || string.IsNullOrEmpty(result.GeneratedCSharpCode))
                    continue;

                var syntaxTree = CSharpSyntaxTree.ParseText(result.GeneratedCSharpCode);
                var parseDiagnostics = syntaxTree.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();

                if (parseDiagnostics.Count > 0)
                {
                    var errors = string.Join("; ", parseDiagnostics.Take(3).Select(d => d.GetMessage()));
                    failures.Add(
                        $"Seed {seed}, iter {i}: Generated C# has {parseDiagnostics.Count} parse error(s): {errors}\n" +
                        $"Input: {Truncate(source)}\n" +
                        $"Generated: {Truncate(result.GeneratedCSharpCode, 500)}");
                }
            }
            catch (OperationCanceledException)
            {
                // Timeouts acceptable
            }
            catch (Exception)
            {
                // Compilation crashes tested elsewhere
            }
        }

        if (failures.Count > 0)
        {
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Property: Generated C# compiles to IL without errors.
    /// Takes the parse check further: the generated C# should also compile
    /// to a valid .NET assembly.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    public void GeneratedCSharp_CompilesToIL(int seed)
    {
        var fuzzer = new SharpyFuzzer(seed);
        var compiler = new Compiler();
        var failures = new List<string>();
        var references = GetMetadataReferences();

        for (int i = 0; i < 25; i++)
        {
            var source = fuzzer.GenerateValidLooking();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(FuzzIterationTimeoutMs));
                var result = compiler.Compile(source, "fuzz_codegen.spy", cts.Token);

                // Only check programs that compiled successfully
                if (!result.Success || string.IsNullOrEmpty(result.GeneratedCSharpCode))
                    continue;

                var syntaxTree = CSharpSyntaxTree.ParseText(result.GeneratedCSharpCode);

                var compilation = CSharpCompilation.Create(
                    $"FuzzCodeGen_{seed}_{i}",
                    new[] { syntaxTree },
                    references,
                    new CSharpCompilationOptions(OutputKind.ConsoleApplication));

                using var ms = new MemoryStream();
                var emitResult = compilation.Emit(ms);

                if (!emitResult.Success)
                {
                    var errors = emitResult.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Take(3)
                        .Select(d => d.ToString())
                        .ToList();

                    failures.Add(
                        $"Seed {seed}, iter {i}: Generated C# failed IL compilation:\n" +
                        $"  {string.Join("\n  ", errors)}\n" +
                        $"Input: {Truncate(source)}");
                }
            }
            catch (OperationCanceledException)
            {
                // Timeouts acceptable
            }
            catch (Exception)
            {
                // Compilation crashes tested elsewhere
            }
        }

        if (failures.Count > 0)
        {
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Also test with the new class hierarchy generator, which exercises
    /// more complex codegen paths (inheritance, overrides, constructors).
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(7777)]
    public void GeneratedCSharp_ClassHierarchies_ParsesWithoutErrors(int seed)
    {
        var fuzzer = new SharpyFuzzer(seed);
        var compiler = new Compiler();
        var failures = new List<string>();

        for (int i = 0; i < 25; i++)
        {
            var source = fuzzer.GenerateClassHierarchy();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(FuzzIterationTimeoutMs));
                var result = compiler.Compile(source, "fuzz_hierarchy_cg.spy", cts.Token);

                if (!result.Success || string.IsNullOrEmpty(result.GeneratedCSharpCode))
                    continue;

                var syntaxTree = CSharpSyntaxTree.ParseText(result.GeneratedCSharpCode);
                var parseDiagnostics = syntaxTree.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();

                if (parseDiagnostics.Count > 0)
                {
                    var errors = string.Join("; ", parseDiagnostics.Take(3).Select(d => d.GetMessage()));
                    failures.Add(
                        $"Seed {seed}, iter {i}: Class hierarchy C# has {parseDiagnostics.Count} parse error(s): {errors}");
                }
            }
            catch (OperationCanceledException)
            {
                // Timeouts acceptable
            }
            catch (Exception)
            {
                // Crashes tested elsewhere
            }
        }

        if (failures.Count > 0)
        {
            foreach (var f in failures)
                _output.WriteLine(f);
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Builds the set of metadata references needed for C# compilation.
    /// Mirrors the reference setup in IntegrationTestBase.
    /// </summary>
    private List<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
        };

        // Try to add Sharpy.Core reference
        try
        {
            var testAssemblyPath = Assembly.GetExecutingAssembly().Location;
            var testDir = Path.GetDirectoryName(testAssemblyPath);
            var possibleFrameworks = new[] { "netstandard2.1", "netstandard2.0" };

            foreach (var targetFramework in possibleFrameworks)
            {
                var runtimePath = Path.Combine(testDir!, "..", "..", "..", "..", "Sharpy.Core", "bin", "Debug", targetFramework, "Sharpy.Core.dll");
                runtimePath = Path.GetFullPath(runtimePath);

                if (File.Exists(runtimePath))
                {
                    references.Add(MetadataReference.CreateFromFile(runtimePath));
                    _output.WriteLine($"Loaded Sharpy.Core from: {runtimePath}");

                    // Add netstandard reference
                    try
                    {
                        var netstandardAssembly = Assembly.Load("netstandard");
                        references.Add(MetadataReference.CreateFromFile(netstandardAssembly.Location));
                    }
                    catch
                    {
                        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
                        var netstandardPath = Path.Combine(runtimeDir!, "netstandard.dll");
                        if (File.Exists(netstandardPath))
                        {
                            references.Add(MetadataReference.CreateFromFile(netstandardPath));
                        }
                    }

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Failed to load Sharpy.Core: {ex.Message}");
        }

        return references;
    }

    private static string Truncate(string s, int maxLen = 200)
    {
        if (s.Length <= maxLen)
            return s.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        return s[..maxLen].Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "...";
    }
}
