using FluentAssertions;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Guards the #1438 call-target seam invariant: every call-resolution route records its target
/// through <c>RecordResolvedCallTarget</c> or is exempt with rationale, and the deprecation
/// check fires on every recording route (#1537 #1536 #1525).
/// <para>
/// Part (a) is a mechanical scan: <c>.SetCallTarget(</c> must appear only in
/// <c>SemanticInfo.cs</c> (the definition) and <c>RecordResolvedCallTarget</c> (the seam).
/// </para>
/// <para>
/// Part (b) is a route table: each row is a small program with <c>@deprecated</c> on the
/// target and an assertion that SPY0466 fires at the call site. A missing route means a
/// missing recording — the warning travels through the seam.
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class CallTargetSeamConformanceTests : IntegrationTestBase
{
    public CallTargetSeamConformanceTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void SetCallTarget_OnlyInSemanticInfoAndRecordResolvedCallTarget()
    {
        var compilerDir = FindCompilerSourceDirectory();
        Directory.Exists(compilerDir).Should().BeTrue(
            $"compiler source directory not found at {compilerDir}");

        var files = Directory.GetFiles(compilerDir, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains(".SetCallTarget("))
                    continue;

                // Allowed in SemanticInfo.cs (the definition)
                if (fileName == "SemanticInfo.cs")
                    continue;

                // Allowed inside RecordResolvedCallTarget (the seam) — identified by
                // being in the TypeChecker Calls file near the method
                if (fileName == "TypeChecker.Expressions.Access.Calls.cs"
                    && IsInsideRecordResolvedCallTarget(lines, i))
                    continue;

                violations.Add($"{fileName}:{i + 1}: {lines[i].Trim()}");
            }
        }

        violations.Should().BeEmpty(
            ".SetCallTarget( must only appear in SemanticInfo.cs (definition) and " +
            "RecordResolvedCallTarget (the seam) — #1438");
    }

    private static bool IsInsideRecordResolvedCallTarget(string[] lines, int lineIndex)
    {
        for (int i = lineIndex - 1; i >= Math.Max(0, lineIndex - 10); i--)
        {
            if (lines[i].Contains("RecordResolvedCallTarget"))
                return true;
        }
        return false;
    }

    [Theory]
    [InlineData("FreeFunction", """
        @deprecated("old")
        def greet() -> str:
            return "hi"
        def main():
            print(greet())
        """)]
    [InlineData("OverloadedFunction", """
        @deprecated("old")
        def process(x: int) -> str:
            return str(x)
        def process(x: str, y: str) -> str:
            return x + y
        def main():
            print(process(1))
        """)]
    [InlineData("OverloadedInstanceMethod", """
        class Calc:
            @deprecated("old")
            def run(self, x: int) -> str:
                return str(x)
            def run(self, x: str, y: str) -> str:
                return x + y
        def main():
            c = Calc()
            print(c.run(1))
        """)]
    [InlineData("SingleInstanceMethod", """
        class Calc:
            @deprecated("old")
            def compute(self, x: int) -> int:
                return x * 2
        def main():
            c = Calc()
            r: int = c.compute(3)
            print(r)
        """)]
    [InlineData("SingleInitConstructor", """
        class Widget:
            value: int
            @deprecated("old")
            def __init__(self, n: int):
                self.value = n
        def main():
            w = Widget(5)
            print(w.value)
        """)]
    [InlineData("OverloadedInitConstructor", """
        class Widget:
            value: int
            @deprecated("old")
            def __init__(self, n: int):
                self.value = n
            def __init__(self, a: str, b: str):
                self.value = len(a) + len(b)
        def main():
            w = Widget(5)
            print(w.value)
        """)]
    [InlineData("PipeForwardWithCall", """
        @deprecated("old")
        def add(a: int, b: int) -> int:
            return a + b
        def main():
            r: int = 3 |> add(4)
            print(r)
        """)]
    [InlineData("GenericFunctionType", """
        @deprecated("old")
        def identity[T](x: T) -> T:
            return x
        def main():
            r: int = identity[int](42)
            print(r)
        """)]
    public void Route_FiresDeprecationWarning(string route, string source)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"route {route} should compile: {string.Join(", ", result.CompilationErrors)}");
        result.CompilationWarnings.Should().Contain(w => w.Contains("SPY0466") || w.Contains("is deprecated"),
            $"route {route} must fire SPY0466 through the call-target seam");
    }

    private static string FindCompilerSourceDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var compilerPath = Path.Combine(current, "src", "Sharpy.Compiler");
            if (Directory.Exists(compilerPath))
                return compilerPath;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Sharpy.Compiler"));
    }
}
