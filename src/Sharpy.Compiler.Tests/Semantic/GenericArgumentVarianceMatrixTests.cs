using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Generic argument variance matrix (#1748, #1701 Decision 4).
///
/// <para><b>Contract.</b> <c>IsAssignable</c> is the one assignability authority: invariant generic
/// arguments compare by canonical identity (via <c>TypeArgumentsSatisfyVariance</c>). Every route
/// formerly in <c>assignability-allowlist.txt</c> is drained.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class GenericArgumentVarianceMatrixTests : IntegrationTestBase
{
    public GenericArgumentVarianceMatrixTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData("InvariantCall_SameType",
        "def first(xs: list[int]) -> int:\n    return xs[0]\n\ndef main():\n    ys: list[int] = [1]\n    print(first(ys))\n",
        true, null)]
    [InlineData("InvariantPipe_SameType",
        "def first(xs: list[int]) -> int:\n    return xs[0]\n\ndef main():\n    ys: list[int] = [1]\n    print(first(ys))\n",
        true, null)]
    public void Cell_InvariantGenericArguments(string label, string source, bool shouldCompile, string? expectedCode)
    {
        var result = CompileAndExecute(source);

        if (shouldCompile)
        {
            result.Success.Should().BeTrue(
                $"[{label}] must compile. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
            result.StandardOutput.Should().Be("1\n", $"[{label}]\n{source}");
        }
        else
        {
            result.Success.Should().BeFalse(
                $"[{label}] must be refused — invariant generic arguments require identity\n{source}");
            result.RawDiagnostics.Should().Contain(
                d => d.Code == expectedCode,
                $"[{label}] must report {expectedCode}. Got: "
                + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
        }
    }

    [Fact]
    public void AssignabilityAllowlist_IsEmpty()
    {
        var repoRoot = Infrastructure.DispatchSiteScan.FindRepoRoot();
        var path = Path.Combine(repoRoot, "src", "Sharpy.Compiler.Tests",
            "Conformance", "assignability-allowlist.txt");
        var lines = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
            .ToList();
        lines.Should().BeEmpty(
            "every assignability-allowlist row was drained by routing through IsAssignable (#1748)");
    }
}
