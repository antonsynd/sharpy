using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Warm/cold differential harness for the #1533 absence gate (#1568): a diagnostic the cold build
/// gives must be given identically by a warm build whose dependency symbols are cache-served.
///
/// <para><c>GenericDefinition</c> is per-analysis state that does not survive the symbol cache — the
/// serializer carries identity in <c>ClrOriginTypeName</c> instead — so a gate keyed on the definition
/// pointer alone was silently off for a cross-file receiver on a warm build. The fix falls back to
/// <c>ClrOriginTypeName → ResolveOriginDefinition</c>; these cells are the class guard.</para>
///
/// <para>Three things make a cell a guard rather than a ritual, each measured by deleting the fix and
/// watching an earlier draft of this harness stay green. The receiver is a CLR generic that does NOT
/// collapse onto a Sharpy container (<c>Stack[T]</c>, <c>Queue[T]</c>): <c>HashSet[int]</c> maps to
/// <c>set[int]</c> and reaches the gate through the builtin-container arm, which never consults
/// <c>GenericDefinition</c>. The warm arm starts from a build that SUCCEEDED, because a failed build
/// writes no symbol cache — a "warm" rebuild after a refused cold build is a second cold build. And
/// every warm arm proves through <see cref="ProjectCompilationHelper.AssertWarmBuildSkipped"/> that
/// the provider was actually served from the cache.</para>
/// </summary>
public class WarmColdDifferentialTests
{
    private readonly ITestOutputHelper _output;

    public WarmColdDifferentialTests(ITestOutputHelper output) => _output = output;

    private const string ProviderSource = @"
from System.Collections.Generic import Stack

def make() -> Stack[int]:
    return Stack[int]()
";

    private const string ValidMain = @"
from provider import make

def main() -> None:
    s = make()
    s.push(42)
";

    private const string BogusMain = @"
from provider import make

def main() -> None:
    s = make()
    s.no_such_member()
";

    private const string NestedProviderSource = @"
from System.Collections.Generic import Queue

def make() -> list[Queue[int]]:
    return [Queue[int]()]
";

    private const string NestedValidMain = @"
from provider import make

def main() -> None:
    xs = make()
    xs[0].enqueue(1)
";

    private const string NestedBogusMain = @"
from provider import make

def main() -> None:
    xs = make()
    xs[0].no_such_member()
";

    [Fact]
    public void AbsenceGate_RefusesBogusGenericMember_WarmEqualsCold()
        => AssertRefusalIsWarmColdIdentical("WarmColdAbsence", ProviderSource, ValidMain, BogusMain);

    /// <summary>
    /// The receiver is a type ARGUMENT of the cache-served signature (<c>list[Queue[int]]</c> →
    /// <c>xs[0]</c> → <c>Queue[int]</c>), so the fallback must hold for a generic restored one level
    /// down, not only for the signature's outer type.
    /// </summary>
    [Fact]
    public void AbsenceGate_RefusesBogusMemberOnNestedGeneric_WarmEqualsCold()
        => AssertRefusalIsWarmColdIdentical("WarmColdNested", NestedProviderSource, NestedValidMain, NestedBogusMain);

    [Fact]
    public void AbsenceGate_AcceptsValidGenericMember_WarmEqualsCold()
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper
            .WithRootNamespace("WarmColdValid")
            .WithIncremental()
            .AddSourceFile("provider.spy", ProviderSource)
            .AddSourceFile("main.spy", ValidMain)
            .CreateProjectFile();

        var cold = helper.Compile();
        LogErrors("Cold", cold);
        helper.AssertCompilationSucceeded(cold);

        // A real content edit to main.spy only: provider.spy must be served from the cache.
        helper.UpdateSourceFile("main.spy", ValidMain.Replace("push(42)", "push(99)"));
        var warm = helper.Compile();
        LogErrors("Warm", warm);
        helper.AssertIncrementalSkipped(warm, "provider.spy");
        Assert.DoesNotContain(warm.Diagnostics.GetErrors(), e => e.Code == DiagnosticCodes.Semantic.UndefinedMember);
    }

    /// <summary>
    /// Cold arm: the bogus program compiled fresh refuses SPY0203. Warm arm: the VALID program is
    /// built first (populating the symbol cache), then only <c>main.spy</c> is edited to the bogus
    /// program and rebuilt — <c>provider.spy</c> is proven cache-served, and the refusal must be the
    /// same SPY0203.
    /// </summary>
    private void AssertRefusalIsWarmColdIdentical(
        string rootNamespace, string provider, string validMain, string bogusMain)
    {
        using (var coldHelper = new ProjectCompilationHelper(_output))
        {
            coldHelper
                .WithRootNamespace(rootNamespace + "Cold")
                .AddSourceFile("provider.spy", provider)
                .AddSourceFile("main.spy", bogusMain)
                .CreateProjectFile();

            var cold = coldHelper.Compile();
            LogErrors("Cold", cold);
            Assert.Contains(cold.Diagnostics.GetErrors(), e => e.Code == DiagnosticCodes.Semantic.UndefinedMember);
        }

        using var warmHelper = new ProjectCompilationHelper(_output);
        warmHelper
            .WithRootNamespace(rootNamespace + "Warm")
            .WithIncremental()
            .AddSourceFile("provider.spy", provider)
            .AddSourceFile("main.spy", validMain)
            .CreateProjectFile();

        var seed = warmHelper.Compile();
        LogErrors("Seed", seed);
        warmHelper.AssertCompilationSucceeded(seed);

        warmHelper.UpdateSourceFile("main.spy", bogusMain);
        var warm = warmHelper.Compile();
        LogErrors("Warm", warm);
        warmHelper.AssertWarmBuildSkipped(warm, "provider.spy");
        Assert.Contains(warm.Diagnostics.GetErrors(), e => e.Code == DiagnosticCodes.Semantic.UndefinedMember);
    }

    private void LogErrors(string arm, ProjectCompilationResult result)
    {
        var errors = result.Diagnostics.GetErrors();
        _output.WriteLine($"{arm} errors: {errors.Count}");
        foreach (var e in errors)
            _output.WriteLine($"  [{e.Code}] {e.Message}");
    }
}
