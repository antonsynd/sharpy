using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Warm/cold differential harness: verifies that warm-restored (incremental)
/// compilations produce identical diagnostics to cold compilations for the
/// #1533 absence gate. GenericDefinition is per-analysis state that does not
/// survive the symbol cache; the #1568 fix falls back to ClrOriginTypeName
/// so warm-restored generics behave identically to cold-compiled ones.
/// </summary>
public class WarmColdDifferentialTests
{
    private readonly ITestOutputHelper _output;

    public WarmColdDifferentialTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void AbsenceGate_RefusesBogusGenericMember_WarmEqualsCold()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("WarmColdAbsence")
            .WithIncremental()
            .AddSourceFile("provider.spy", @"
from System.Collections.Generic import HashSet

def make() -> HashSet[int]:
    return HashSet[int]()
")
            .AddSourceFile("consumer.spy", @"
from provider import make

def main() -> None:
    s = make()
    s.no_such_member()
")
            .CreateProjectFile();

        // Cold build: should produce SPY0203 for no_such_member
        var cold = helper.Compile();
        var coldErrors = cold.Diagnostics.GetErrors();
        _output.WriteLine($"Cold errors: {coldErrors.Count}");
        foreach (var e in coldErrors)
            _output.WriteLine($"  [{e.Code}] {e.Message}");

        Assert.Contains(coldErrors, e => e.Code == DiagnosticCodes.Semantic.UndefinedMember);

        // Warm rebuild: change only consumer.spy (provider.spy is cached)
        helper.UpdateSourceFile("consumer.spy", @"
from provider import make

def main() -> None:
    s = make()
    s.no_such_member()
    print('warm rebuild')
");

        var warm = helper.Compile();
        var warmErrors = warm.Diagnostics.GetErrors();
        _output.WriteLine($"Warm errors: {warmErrors.Count}");
        foreach (var e in warmErrors)
            _output.WriteLine($"  [{e.Code}] {e.Message}");

        Assert.Contains(warmErrors, e => e.Code == DiagnosticCodes.Semantic.UndefinedMember);
    }

    [Fact]
    public void AbsenceGate_AcceptsValidGenericMember_WarmEqualsCold()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("WarmColdValid")
            .WithIncremental()
            .AddSourceFile("provider.spy", @"
from System.Collections.Generic import HashSet

def make() -> HashSet[int]:
    return HashSet[int]()
")
            .AddSourceFile("consumer.spy", @"
from provider import make

def main() -> None:
    s = make()
    s.add(42)
")
            .CreateProjectFile();

        // Cold build: absence gate must not fire SPY0203 for valid members
        var cold = helper.Compile();
        var coldUndefined = cold.Diagnostics.GetErrors()
            .Where(e => e.Code == DiagnosticCodes.Semantic.UndefinedMember)
            .ToList();
        _output.WriteLine($"Cold SPY0203 count: {coldUndefined.Count}");
        foreach (var e in coldUndefined)
            _output.WriteLine($"  {e.Message}");
        Assert.Empty(coldUndefined);

        // Warm rebuild: change only consumer.spy — cached provider still has HashSet[int]
        helper.UpdateSourceFile("consumer.spy", @"
from provider import make

def main() -> None:
    s = make()
    s.add(99)
");

        var warm = helper.Compile();
        var warmUndefined = warm.Diagnostics.GetErrors()
            .Where(e => e.Code == DiagnosticCodes.Semantic.UndefinedMember)
            .ToList();
        _output.WriteLine($"Warm SPY0203 count: {warmUndefined.Count}");
        foreach (var e in warmUndefined)
            _output.WriteLine($"  {e.Message}");
        Assert.Empty(warmUndefined);
    }
}
