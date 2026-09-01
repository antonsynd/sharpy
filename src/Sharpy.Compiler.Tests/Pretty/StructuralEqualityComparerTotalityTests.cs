using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.PrettyTests;

/// <summary>
/// Totality guard for <c>StructuralEqualityComparer.Equals</c>: every concrete <see cref="Node"/>
/// kind has a switch arm, so a new kind fails this test.
/// The existing <c>UnparserExhaustivenessTests.AllConcreteNodeTypesHaveComparerArms</c> covers
/// the behavioral contract; this test pins the scan-vs-reflection equivalence.
/// </summary>
public class StructuralEqualityComparerTotalityTests
{
    private const string SourceFile = "src/Sharpy.Compiler/Pretty/StructuralEqualityComparer.cs";

    private readonly ITestOutputHelper _output;

    public StructuralEqualityComparerTotalityTests(ITestOutputHelper output) => _output = output;

    private static HashSet<string> GetConcreteNodeNames()
    {
        var baseType = typeof(Node);
        return baseType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(baseType)
                        && !t.IsAbstract
                        && t.IsPublic)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void SwitchArms_CoverAllConcreteNodeTypes()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "Equals");
        Assert.NotEmpty(switchArms);

        var allNodes = GetConcreteNodeNames();
        _output.WriteLine($"Switch arms: {switchArms.Count}, Concrete Node types: {allNodes.Count}");

        var missing = allNodes.Where(n => !switchArms.Contains(n)).OrderBy(n => n).ToList();
        var phantom = switchArms.Where(n => !allNodes.Contains(n)).OrderBy(n => n).ToList();

        foreach (var m in missing)
            _output.WriteLine($"  MISSING: {m}");
        foreach (var p in phantom)
            _output.WriteLine($"  PHANTOM: {p}");

        Assert.Empty(missing);
        Assert.Empty(phantom);
    }
}
