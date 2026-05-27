using System.Text.RegularExpressions;
using CsCheck;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.CodeGen;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class IncrementalCompilationPropertyTests
{
    private readonly ITestOutputHelper _output;

    private static readonly Regex TempPathPattern = new(
        @"sharpy_test_[0-9a-f\-]+", RegexOptions.Compiled);

    public IncrementalCompilationPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void IncrementalCompile_MatchesFullCompile()
    {
        Gen.Int[1, 1000].SelectMany(val1 =>
            Gen.Int[1, 1000].Select(val2 => (val1, val2))
        ).Sample(pair =>
        {
            var (v1, v2) = pair;

            foreach (var (mainSpy, libSpy, name) in GenerateTemplates(v1, v2))
            {
                _output.WriteLine($"Testing template '{name}' with values ({v1}, {v2})");

                // Full compile (no incremental)
                var (fullDiags, fullCsharp) = CompileFull(mainSpy, libSpy);

                // Incremental compile: first compile populates cache, second reads it
                var (incrDiags, incrCsharp) = CompileIncremental(mainSpy, libSpy);

                AssertDiagnosticsEqual(fullDiags, incrDiags, name);
                AssertGeneratedCSharpEqual(fullCsharp, incrCsharp, name);
            }
        }, iter: 10);
    }

    [Fact]
    public void IncrementalCompile_RecompilesOnChange()
    {
        Gen.Int[1, 1000].SelectMany(val1 =>
            Gen.Int[1, 1000].Select(val2 => (val1, val2))
        ).Sample(pair =>
        {
            var (v1, v2) = pair;

            var mainSpy = $"from lib import add\n\ndef main():\n    print(add({v1}, {v2}))\n";
            var libSpyOriginal = $"def add(a: int, b: int) -> int:\n    return a + b\n";
            var libSpyModified = $"def add(a: int, b: int) -> int:\n    return a + b + 1\n";

            _output.WriteLine($"Testing incremental recompilation with values ({v1}, {v2})");

            // Incremental compile: first compile with original, then modify and recompile
            Dictionary<string, string> incrCsharp;
            List<Sharpy.Compiler.Diagnostics.CompilerDiagnostic> incrDiags;

            using (var helper = new ProjectCompilationHelper(_output))
            {
                helper.WithRootNamespace("Test");
                helper.AddSourceFile("main.spy", mainSpy);
                helper.AddSourceFile("lib.spy", libSpyOriginal);
                helper.CreateProjectFile();

                // First compile (populates cache)
                var firstResult = helper.Compile();
                if (!firstResult.Success)
                {
                    _output.WriteLine("First compile failed, skipping");
                    return;
                }

                // Modify lib.spy
                helper.UpdateSourceFile("lib.spy", libSpyModified);

                // Enable incremental and recompile
                helper.WithIncremental(true);
                var incrResult = helper.Compile();

                incrDiags = incrResult.Diagnostics.GetAll().ToList();
                incrCsharp = new Dictionary<string, string>(incrResult.GeneratedCSharpFiles);
            }

            // Fresh full compile of modified project
            var (freshDiags, freshCsharp) = CompileFull(mainSpy, libSpyModified);

            AssertDiagnosticsEqual(freshDiags, incrDiags, "function_import_modified");
            AssertGeneratedCSharpEqual(freshCsharp, incrCsharp, "function_import_modified");
        }, iter: 5);
    }

    private static IEnumerable<(string mainSpy, string libSpy, string name)> GenerateTemplates(
        int v1, int v2)
    {
        // Template 1: Function import
        yield return (
            $"from lib import add\n\ndef main():\n    print(add({v1}, {v2}))\n",
            $"def add(a: int, b: int) -> int:\n    return a + b\n",
            "function_import"
        );

        // Template 2: Class import
        yield return (
            $"from lib import Point\n\ndef main():\n    p: Point = Point({v1}, {v2})\n    print(p.x)\n",
            "class Point:\n    x: int\n    y: int\n    def __init__(self, x: int, y: int):\n        self.x = x\n        self.y = y\n",
            "class_import"
        );

        // Template 3: Constant import
        yield return (
            $"from lib import MAX_SIZE\n\ndef main():\n    print(MAX_SIZE)\n",
            $"MAX_SIZE: int = {v1}\n",
            "constant_import"
        );
    }

    private (List<Sharpy.Compiler.Diagnostics.CompilerDiagnostic> diags, Dictionary<string, string> csharp)
        CompileFull(string mainSpy, string libSpy)
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Test");
        helper.AddSourceFile("main.spy", mainSpy);
        helper.AddSourceFile("lib.spy", libSpy);
        helper.CreateProjectFile();

        var result = helper.Compile();
        return (
            result.Diagnostics.GetAll().ToList(),
            new Dictionary<string, string>(result.GeneratedCSharpFiles));
    }

    private (List<Sharpy.Compiler.Diagnostics.CompilerDiagnostic> diags, Dictionary<string, string> csharp)
        CompileIncremental(string mainSpy, string libSpy)
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Test");
        helper.AddSourceFile("main.spy", mainSpy);
        helper.AddSourceFile("lib.spy", libSpy);
        helper.CreateProjectFile();

        // First compile: populates the cache (non-incremental)
        var warmupResult = helper.Compile();
        if (!warmupResult.Success)
        {
            _output.WriteLine("Warmup compile failed, returning warmup results");
            return (
                warmupResult.Diagnostics.GetAll().ToList(),
                new Dictionary<string, string>(warmupResult.GeneratedCSharpFiles));
        }

        // Second compile: incremental, reads from cache
        helper.WithIncremental(true);
        var result = helper.Compile();
        return (
            result.Diagnostics.GetAll().ToList(),
            new Dictionary<string, string>(result.GeneratedCSharpFiles));
    }

    private void AssertDiagnosticsEqual(
        List<Sharpy.Compiler.Diagnostics.CompilerDiagnostic> expected,
        List<Sharpy.Compiler.Diagnostics.CompilerDiagnostic> actual,
        string templateName)
    {
        var normExpected = expected
            .Select(d => (d.Code, NormalizePath(d.Message)))
            .OrderBy(d => d.Code)
            .ThenBy(d => d.Item2)
            .ToList();

        var normActual = actual
            .Select(d => (d.Code, NormalizePath(d.Message)))
            .OrderBy(d => d.Code)
            .ThenBy(d => d.Item2)
            .ToList();

        Assert.True(
            normExpected.Count == normActual.Count,
            $"[{templateName}] Diagnostic count mismatch: " +
            $"expected {normExpected.Count}, got {normActual.Count}.\n" +
            $"Expected: [{string.Join(", ", normExpected.Select(d => $"{d.Code}: {d.Item2}"))}]\n" +
            $"Actual: [{string.Join(", ", normActual.Select(d => $"{d.Code}: {d.Item2}"))}]");

        for (int i = 0; i < normExpected.Count; i++)
        {
            Assert.True(
                normExpected[i].Code == normActual[i].Code &&
                normExpected[i].Item2 == normActual[i].Item2,
                $"[{templateName}] Diagnostic [{i}] mismatch:\n" +
                $"Expected: {normExpected[i].Code}: {normExpected[i].Item2}\n" +
                $"Actual: {normActual[i].Code}: {normActual[i].Item2}");
        }
    }

    private void AssertGeneratedCSharpEqual(
        Dictionary<string, string> expected,
        Dictionary<string, string> actual,
        string templateName)
    {
        var normExpected = NormalizeCSharp(expected);
        var normActual = NormalizeCSharp(actual);

        var keysExpected = normExpected.Keys.OrderBy(k => k).ToList();
        var keysActual = normActual.Keys.OrderBy(k => k).ToList();

        Assert.True(
            keysExpected.SequenceEqual(keysActual),
            $"[{templateName}] Generated C# file keys differ.\n" +
            $"Expected: [{string.Join(", ", keysExpected)}]\n" +
            $"Actual: [{string.Join(", ", keysActual)}]");

        foreach (var key in keysExpected)
        {
            Assert.True(
                normExpected[key] == normActual[key],
                $"[{templateName}] Generated C# for '{key}' differs.\n" +
                $"Expected:\n{normExpected[key]}\n\n" +
                $"Actual:\n{normActual[key]}");
        }
    }

    private static string NormalizePath(string s) =>
        TempPathPattern.Replace(s, "sharpy_test_NORMALIZED");

    private static Dictionary<string, string> NormalizeCSharp(Dictionary<string, string> files)
    {
        var result = new Dictionary<string, string>();
        foreach (var (key, value) in files)
        {
            var normKey = Path.GetFileName(key).Replace(".spy", "");
            var normValue = TempPathPattern.Replace(value, "sharpy_test_NORMALIZED");
            result[normKey] = normValue;
        }
        return result;
    }
}
