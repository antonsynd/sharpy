using System.Text.RegularExpressions;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class MultiFileDeterminismTests
{
    private readonly ITestOutputHelper _output;

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

        var normDiags1 = diags1.Select(d => (d.Code, NormalizePath(d.Message))).OrderBy(d => d.Code).ThenBy(d => d.Item2).ToList();
        var normDiags2 = diags2.Select(d => (d.Code, NormalizePath(d.Message))).OrderBy(d => d.Code).ThenBy(d => d.Item2).ToList();

        Assert.Equal(normDiags1.Count, normDiags2.Count);
        for (int i = 0; i < normDiags1.Count; i++)
        {
            Assert.Equal(normDiags1[i].Code, normDiags2[i].Code);
            Assert.Equal(normDiags1[i].Item2, normDiags2[i].Item2);
        }

        var norm1 = NormalizeCSharp(csharp1);
        var norm2 = NormalizeCSharp(csharp2);

        var keys1 = norm1.Keys.OrderBy(k => k).ToList();
        var keys2 = norm2.Keys.OrderBy(k => k).ToList();
        Assert.Equal(keys1, keys2);

        foreach (var key in keys1)
        {
            Assert.Equal(norm1[key], norm2[key]);
        }
    }

    private (List<Sharpy.Compiler.Diagnostics.CompilerDiagnostic> diags, Dictionary<string, string> csharp)
        CompileWithOrder(string mainSpy, string libSpy, bool mainFirst)
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Test");

        if (mainFirst)
        {
            helper.AddSourceFile("main.spy", mainSpy);
            helper.AddSourceFile("lib.spy", libSpy);
        }
        else
        {
            helper.AddSourceFile("lib.spy", libSpy);
            helper.AddSourceFile("main.spy", mainSpy);
        }

        helper.CreateProjectFile();
        var result = helper.Compile();

        return (
            result.Diagnostics.GetAll().ToList(),
            new Dictionary<string, string>(result.GeneratedCSharpFiles));
    }

    private static string NormalizePath(string s) =>
        TempPathPattern.Replace(s, "sharpy_test_NORMALIZED");

    private static readonly Regex TempPathPattern = new(
        @"sharpy_test_[0-9a-f\-]+", RegexOptions.Compiled);

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
