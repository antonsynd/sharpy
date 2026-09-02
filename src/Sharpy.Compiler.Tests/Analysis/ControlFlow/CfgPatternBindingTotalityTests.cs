using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

public class CfgPatternBindingTotalityTests
{
    private readonly ITestOutputHelper _output;

    public CfgPatternBindingTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> Binding = new()
    {
        nameof(BindingPattern),
        nameof(AsPattern),
        nameof(OrPattern),
        nameof(AndPattern),
        nameof(TuplePattern),
        nameof(ListPattern),
        nameof(StarPattern),
        nameof(PositionalPattern),
        nameof(PropertyPattern),
        nameof(GuardPattern),
    };

    private static readonly HashSet<string> NonBinding = new()
    {
        nameof(LiteralPattern),
        nameof(MemberAccessPattern),
        nameof(RelationalPattern),
        nameof(TypePattern),
        nameof(WildcardPattern),
    };

    [Fact]
    public void AllConcretePatternSubtypes_AreClassified()
    {
        var patternBaseType = typeof(Pattern);
        var concrete = patternBaseType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(patternBaseType) && !t.IsAbstract && t.IsPublic)
            .Select(t => t.Name)
            .ToHashSet();

        var allClassified = new HashSet<string>(Binding);
        allClassified.UnionWith(NonBinding);

        var unclassified = concrete.Except(allClassified).OrderBy(n => n).ToList();
        var phantom = allClassified.Except(concrete).OrderBy(n => n).ToList();

        if (unclassified.Count > 0)
            _output.WriteLine($"Unclassified: {string.Join(", ", unclassified)}");
        if (phantom.Count > 0)
            _output.WriteLine($"Phantom: {string.Join(", ", phantom)}");

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void CollectPatternBindingKeysInto_SwitchArms_MatchBindingClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs",
            "CollectPatternBindingKeysInto");

        Assert.True(switchArms.SetEquals(Binding),
            $"CollectPatternBindingKeysInto switch arms differ from Binding.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(Binding))}\n" +
            $"  Missing from switch: {string.Join(", ", Binding.Except(switchArms))}");
    }

    [Fact]
    public void ClassificationSets_AreDisjoint()
    {
        var overlap = Binding.Intersect(NonBinding).ToList();
        Assert.Empty(overlap);
    }

    [Fact]
    public void PropertyPatternField_IsNotInPatternUniverse()
    {
        var patternBaseType = typeof(Pattern);
        var concrete = patternBaseType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(patternBaseType) && !t.IsAbstract)
            .Select(t => t.Name)
            .ToHashSet();

        Assert.DoesNotContain(nameof(PropertyPatternField), concrete);
    }
}
