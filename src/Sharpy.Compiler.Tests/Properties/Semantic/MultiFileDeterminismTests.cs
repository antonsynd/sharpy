using System.Text.RegularExpressions;
using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// Enforces the A3 determinism contract (#1036, root-causing #1032): multi-file
/// compilation output — diagnostics and generated C# — must be independent of the order
/// in which source files are enumerated. After the ordinal source-file sort at load and
/// compile entry, output is not merely order-independent as a set but byte-identical and
/// diagnostic order is stable, so these tests compare STRICTLY IN ORDER (no sort-before-
/// compare normalization). A failure here is a live #1032 instance, not flake.
/// </summary>
[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class MultiFileDeterminismTests
{
    private readonly ITestOutputHelper _output;

    // Shared across all fixture compiles so the (expensive) reference load happens once.
    private static readonly Lazy<ModuleRegistry> SharedModuleRegistry = new(() =>
    {
        var registry = new ModuleRegistry(NullLogger.Instance);
        registry.LoadReference(SharpyCoreReference.Location);
        return registry;
    });

    public MultiFileDeterminismTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [MemberData(nameof(TestPrograms))]
    public void CompilationOrder_DoesNotAffectResult(string name, string mainSpy, string libSpy)
    {
        _output.WriteLine($"Testing: {name}");
        var (diags1, csharp1) = CompileWithOrder(mainSpy, libSpy, mainFirst: true);
        var (diags2, csharp2) = CompileWithOrder(mainSpy, libSpy, mainFirst: false);

        AssertDiagnosticsIdenticalInOrder(diags1, diags2);
        AssertGeneratedCSharpIdentical(csharp1, csharp2);
    }

    [Fact]
    public void GeneratedMultiFilePrograms_AreOrderIndependent()
    {
        Gen.Select(
            Gen.OneOfConst("greet", "compute", "transform", "process"),
            Gen.OneOfConst("int", "str", "bool"),
            Gen.OneOfConst("Widget", "Item", "Entry", "Record"),
            Gen.OneOfConst("int", "str")
        ).Sample((funcName, paramType, className, fieldType) =>
        {
            var defaultVal = paramType switch
            {
                "int" => "0",
                "str" => "\"hello\"",
                "bool" => "True",
                _ => "0"
            };
            var fieldDefault = fieldType switch
            {
                "int" => "42",
                "str" => "\"test\"",
                _ => "0"
            };

            var libSpy = $"def {funcName}(x: {paramType}) -> {paramType}:\n    return x\n\nclass {className}:\n    value: {fieldType}\n    def __init__(self, v: {fieldType}):\n        self.value = v\n";
            var mainSpy = $"from lib import {funcName}, {className}\n\ndef main():\n    print({funcName}({defaultVal}))\n    obj: {className} = {className}({fieldDefault})\n    print(obj.value)\n";

            var (diags1, csharp1) = CompileWithOrder(mainSpy, libSpy, mainFirst: true);
            var (diags2, csharp2) = CompileWithOrder(mainSpy, libSpy, mainFirst: false);

            AssertDiagnosticsIdenticalInOrder(diags1, diags2, throwOnMismatch: true);
            AssertGeneratedCSharpIdentical(csharp1, csharp2, throwOnMismatch: true);
        }, iter: 20);
    }

    /// <summary>
    /// The core A3 harness: every multi-file fixture directory is compiled twice —
    /// once in ordinal-sorted order, once in reversed order — and the two compilations
    /// must produce identical diagnostics (in order) and byte-identical generated C#.
    /// This exercises the compile-entry defensive sort over the whole fixture corpus;
    /// removing that sort re-breaks #1032 and turns these cases red. Compile-only: no
    /// execution, so the whole corpus stays fast.
    /// </summary>
    [Theory]
    [MemberData(nameof(MultiFileFixtures))]
    public void MultiFileFixture_CompileOutput_IsOrderIndependent(string testName, string fixtureDir)
    {
        _output.WriteLine($"Fixture: {testName}");

        var files = Directory.GetFiles(fixtureDir, "*.spy", SearchOption.AllDirectories);
        var sorted = files.OrderBy(f => f, StringComparer.Ordinal).ToList();
        var reversed = Enumerable.Reverse(sorted).ToList();

        // A no-op when there is only one file, but the discovery filter guarantees >1.
        var (diagsSorted, csharpSorted) = CompileFixtureWithOrder(fixtureDir, sorted);
        var (diagsReversed, csharpReversed) = CompileFixtureWithOrder(fixtureDir, reversed);

        // Both compilations read the same on-disk files, so paths are identical between
        // runs — compare messages verbatim (no temp-path normalization needed here).
        AssertDiagnosticsIdenticalInOrder(diagsSorted, diagsReversed, normalizeMessages: false);
        AssertGeneratedCSharpIdentical(csharpSorted, csharpReversed, normalizeValues: false);
    }

    public static IEnumerable<object[]> MultiFileFixtures()
    {
        foreach (var fixture in FixtureDiscoveryHelper.DiscoverFixtures().Where(f => f.IsMultiFile))
        {
            yield return new object[] { fixture.TestName, fixture.SpyFilePath };
        }
    }

    private (List<CompilerDiagnostic> diags, Dictionary<string, string> csharp)
        CompileWithOrder(string mainSpy, string libSpy, bool mainFirst)
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Test");
        helper.AddSourceFile("main.spy", mainSpy);
        helper.AddSourceFile("lib.spy", libSpy);
        helper.CreateProjectFile();

        // Drive the loaded (already sorted) source list into an explicit order. The
        // compile-entry sort re-normalizes it, which is exactly the property under test:
        // whatever order a caller supplies, the output is the sorted-order output.
        Func<IReadOnlyList<string>, IReadOnlyList<string>> reorder = mainFirst
            ? sf => sf.OrderBy(f => f, StringComparer.Ordinal).ToList()
            : sf => sf.OrderByDescending(f => f, StringComparer.Ordinal).ToList();

        var result = helper.Compile(reorder);

        return (
            result.Diagnostics.GetAll().ToList(),
            new Dictionary<string, string>(result.GeneratedCSharpFiles));
    }

    private (List<CompilerDiagnostic> diags, Dictionary<string, string> csharp)
        CompileFixtureWithOrder(string fixtureDir, IReadOnlyList<string> orderedFiles)
    {
        var mainSpy = Path.Combine(fixtureDir, "main.spy");
        var entryPoint = File.Exists(mainSpy)
            ? "main.spy"
            : Path.GetFileName(orderedFiles[0]);

        var config = new ProjectConfig
        {
            ProjectDirectory = fixtureDir,
            ProjectFilePath = Path.Combine(fixtureDir, "project.spyproj"),
            RootNamespace = "Test",
            OutputType = "exe",
            EntryPoint = entryPoint,
            // Fresh list each call: ProjectCompiler.Compile sorts SourceFiles in place.
            SourceFiles = orderedFiles.ToList(),
            Configuration = "Debug",
            TargetFramework = "net10.0",
        };

        var compiler = new ProjectCompiler(NullLogger.Instance, moduleRegistry: SharedModuleRegistry.Value);
        var result = compiler.Compile(config);

        return (
            result.Diagnostics.GetAll().ToList(),
            new Dictionary<string, string>(result.GeneratedCSharpFiles));
    }

    private static void AssertDiagnosticsIdenticalInOrder(
        IReadOnlyList<CompilerDiagnostic> a,
        IReadOnlyList<CompilerDiagnostic> b,
        bool normalizeMessages = true,
        bool throwOnMismatch = false)
    {
        string Msg(string s) => normalizeMessages ? NormalizePath(s) : s;

        if (a.Count != b.Count)
        {
            Fail(throwOnMismatch, $"Diagnostic count mismatch: {a.Count} vs {b.Count}");
            return;
        }

        for (int i = 0; i < a.Count; i++)
        {
            bool same = a[i].Code == b[i].Code
                && Msg(a[i].Message) == Msg(b[i].Message)
                && a[i].Line == b[i].Line
                && a[i].Column == b[i].Column
                && FileKey(a[i].FilePath) == FileKey(b[i].FilePath);

            if (!same)
            {
                Fail(throwOnMismatch,
                    $"Diagnostic mismatch at index {i}:\n" +
                    $"  A: [{a[i].Code}] {Msg(a[i].Message)} @ {FileKey(a[i].FilePath)}:{a[i].Line}:{a[i].Column}\n" +
                    $"  B: [{b[i].Code}] {Msg(b[i].Message)} @ {FileKey(b[i].FilePath)}:{b[i].Line}:{b[i].Column}");
                return;
            }
        }
    }

    private static void AssertGeneratedCSharpIdentical(
        Dictionary<string, string> a,
        Dictionary<string, string> b,
        bool normalizeValues = true,
        bool throwOnMismatch = false)
    {
        var na = NormalizeCSharp(a, normalizeValues);
        var nb = NormalizeCSharp(b, normalizeValues);

        var keysA = na.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        var keysB = nb.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        if (!keysA.SequenceEqual(keysB))
        {
            Fail(throwOnMismatch,
                $"Generated C# file keys differ: [{string.Join(", ", keysA)}] vs [{string.Join(", ", keysB)}]");
            return;
        }

        foreach (var key in keysA)
        {
            if (na[key] != nb[key])
            {
                Fail(throwOnMismatch, $"Generated C# differs for {key}");
                return;
            }
        }
    }

    private static void Fail(bool throwOnMismatch, string message)
    {
        if (throwOnMismatch)
        {
            throw new Exception(message);
        }

        Assert.Fail(message);
    }

    private static string FileKey(string? filePath) =>
        string.IsNullOrEmpty(filePath) ? "" : Path.GetFileName(filePath);

    private static string NormalizePath(string s) =>
        TempPathPattern.Replace(s, "sharpy_test_NORMALIZED");

    private static readonly Regex TempPathPattern = new(
        @"sharpy_test_[0-9a-f\-]+", RegexOptions.Compiled);

    private static Dictionary<string, string> NormalizeCSharp(Dictionary<string, string> files, bool normalizeValues)
    {
        var result = new Dictionary<string, string>();
        foreach (var (key, value) in files)
        {
            var normKey = Path.GetFileName(key).Replace(".spy", "");
            var normValue = normalizeValues
                ? TempPathPattern.Replace(value, "sharpy_test_NORMALIZED")
                : value;
            result[normKey] = normValue;
        }
        return result;
    }

    public static TheoryData<string, string, string> TestPrograms => new()
    {
        {
            "function_import",
            "from lib import add\n\ndef main():\n    print(add(1, 2))\n",
            "def add(a: int, b: int) -> int:\n    return a + b\n"
        },
        {
            "class_import",
            "from lib import Point\n\ndef main():\n    p: Point = Point(3, 4)\n    print(p.x)\n",
            "class Point:\n    x: int\n    y: int\n    def __init__(self, x: int, y: int):\n        self.x = x\n        self.y = y\n"
        },
        {
            "constant_import",
            "from lib import MAX_SIZE\n\ndef main():\n    print(MAX_SIZE)\n",
            "MAX_SIZE: int = 100\n"
        }
    };
}
