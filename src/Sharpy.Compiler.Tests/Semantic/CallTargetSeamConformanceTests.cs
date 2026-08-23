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
/// missing recording — the warning travels through the seam. Single-file routes ride
/// <c>Route_FiresDeprecationWarning</c>; the import-boundary routes (imported overloaded,
/// module-qualified overloaded) ride <c>MultiFileRoute_FiresDeprecationWarning</c>.
/// </para>
/// <para>
/// Part (c) is the exemption table: routes that deliberately do NOT reach the seam, each with
/// its rationale and an issue/code citation (<see cref="ExemptRoutes"/>). A guard asserts every
/// exemption stays cited — an uncited exemption is a silent hole, not a decision.
/// </para>
/// <para>
/// MUTATION-TESTED (2026-08-20, plan-930411 Task 1.4): with the #1537 recording block in
/// <c>CheckFunctionCall</c>'s member-access arm disabled locally (the
/// <c>RecordResolvedCallTarget(call, singleMethod)</c> call commented out), the
/// <c>SingleInstanceMethod</c> row failed with "route SingleInstanceMethod must fire SPY0466"
/// while the other rows stayed green; restored, all rows green. The route table detects a
/// removed recording, not just a removed warning.
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class CallTargetSeamConformanceTests : IntegrationTestBase
{
    public CallTargetSeamConformanceTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Call-resolution routes that deliberately bypass <c>RecordResolvedCallTarget</c>. Each row
    /// is (route, rationale-with-citation). Kept next to the positive table so adding a new
    /// resolution route forces a decision: record through the seam, or document the exemption here.
    /// </summary>
    public static readonly IReadOnlyList<(string Route, string Rationale)> ExemptRoutes = new[]
    {
        ("LambdaLiteral",
         "a lambda has no Symbol — nothing to record and nothing decoratable; positional validation stays on CheckLambdaCall, and with no named parameters kwargs stay honestly unvalidatable (#1591)"),
        ("BclSynthesizedFunctionType",
         "delegate Invoke / BCL-derived / closed-extension FunctionTypes carry no Symbol (#1537's measured boundary); positional validation stays on CheckLambdaCall — single instance-method calls DO resolve a Symbol and kwarg-validate at the #1591 recording seam before falling through to it"),
        ("DelegateTypedInvocation",
         "the invoked variable's FunctionType has no Symbol, so the call cannot record; the reference site (f = add) is the deprecation surface and checks nothing today — #1593"),
        ("BuiltinsQualifiedSingle",
         "builtins aren't user-decoratable, so no positive @deprecated control exists; builtins.f resolves through the registry to the identical symbols as the bare spelling (#1381 agreement)"),
        ("SuperInitDelegation",
         "super() receivers are excluded from the member-access arm (Object is not SuperExpression); the emitter's constructor fallback (RoslynEmitter Constructors.cs) never reads a recorded target for this shape — deprecation gap tracked by #1594"),
        ("SelfInitDelegation",
         "self.__init__ falls into the member-access arm but dunders are deliberately excluded from #1537's recording (recording __init__ would change the construction route #1536 owns) — deprecation gap tracked by #1594"),
    };

    [Fact]
    public void ExemptRoutes_AreAllCited()
    {
        foreach (var (route, rationale) in ExemptRoutes)
        {
            rationale.Should().MatchRegex("#\\d{3,4}|CheckLambdaCall|registry",
                $"exempt route {route} must cite an issue or the code seam that owns it");
        }
    }

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

    [Theory]
    [InlineData("ImportedOverloadedFunction", """
        from lib import process
        def main():
            print(process(1))
        """)]
    [InlineData("ModuleQualifiedOverloadedFunction", """
        import lib
        def main():
            print(lib.process(1))
        """)]
    public void MultiFileRoute_FiresDeprecationWarning(string route, string mainSource)
    {
        using var helper = new Helpers.ProjectCompilationHelper(Output);
        helper.WithRootNamespace("SeamRoutes")
            .AddSourceFile("lib.spy", """
                @deprecated("old")
                def process(x: int) -> str:
                    return str(x)
                def process(x: str, y: str) -> str:
                    return x + y
                """)
            .AddSourceFile("main.spy", mainSource, isEntryPoint: true)
            .CreateProjectFile();

        var result = helper.Compile();
        result.Success.Should().BeTrue(
            $"route {route} should compile: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        result.Diagnostics.GetWarnings().Should().Contain(
            w => w.Code == "SPY0466" || w.Message.Contains("deprecated"),
            $"route {route} must fire SPY0466 across the import boundary through the call-target seam");
    }

    // The OUT-of-source-set renamed-alias twin (#1525, from math import log as mylog) lives in
    // Sharpy.Cli.Tests/E2E/ModeDivergenceTests.OutOfSourceSetRenamedAlias_DispatchesImportedOverloads_UnderRun:
    // stdlib MODULE resolution (ModuleRegistry.LoadReference) only exists in the deployed CLI
    // layout, not in this project's in-process harness (Sharpy.Core/Sharpy.Stdlib refs only).

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
