using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Family guard for dispatch sites over <see cref="ComprehensionClause"/> kinds.
/// The universe is {ForClause, IfClause} — the only concrete subtypes.
/// Every member site's arms are pinned; a new ComprehensionClause kind fails all sites at once.
/// </summary>
public class ComprehensionClauseDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public ComprehensionClauseDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    private static List<string> GetConcreteClauseNames()
    {
        var baseType = typeof(ComprehensionClause);
        return baseType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(baseType)
                        && !t.IsAbstract
                        && t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private static readonly HashSet<string> Universe = new()
    {
        nameof(ForClause),
        nameof(IfClause),
    };

    [Fact]
    public void Universe_MatchesReflection()
    {
        var actual = GetConcreteClauseNames();
        _output.WriteLine($"Concrete ComprehensionClause subtypes: {actual.Count}");
        foreach (var name in actual)
            _output.WriteLine($"  {name}");

        Assert.True(Universe.SetEquals(new HashSet<string>(actual)),
            $"Universe mismatch.\n" +
            $"  Extra in universe: {string.Join(", ", Universe.Except(actual))}\n" +
            $"  Missing from universe: {string.Join(", ", actual.Except(Universe))}");
    }

    // --- CheckComprehensionClauses ---
    [Fact]
    public void CheckComprehensionClauses_Arms_CoverUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Literals.cs",
            "CheckComprehensionClauses");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CheckComprehensionClauses arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(Universe),
            $"Arms differ from universe.\n" +
            $"  Extra: {string.Join(", ", arms.Except(Universe))}\n" +
            $"  Missing: {string.Join(", ", Universe.Except(arms))}");
    }

    // --- QualifiesForProductPreallocation ---
    [Fact]
    public void QualifiesForProductPreallocation_Arms_CoverUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Lowering/Passes/ComprehensionFusionPass.cs",
            "QualifiesForProductPreallocation");
        Assert.NotEmpty(arms);
        _output.WriteLine($"QualifiesForProductPreallocation arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(Universe),
            $"Arms differ from universe.\n" +
            $"  Extra: {string.Join(", ", arms.Except(Universe))}\n" +
            $"  Missing: {string.Join(", ", Universe.Except(arms))}");
    }

    // --- DumpComprehensionClause ---
    [Fact]
    public void DumpComprehensionClause_Arms_CoverUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Parser/AstDumper.cs",
            "DumpComprehensionClause");
        Assert.NotEmpty(arms);
        _output.WriteLine($"DumpComprehensionClause arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(Universe),
            $"Arms differ from universe.\n" +
            $"  Extra: {string.Join(", ", arms.Except(Universe))}\n" +
            $"  Missing: {string.Join(", ", Universe.Except(arms))}");
    }
}
