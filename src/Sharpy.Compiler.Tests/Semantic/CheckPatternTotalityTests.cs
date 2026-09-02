using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

public class CheckPatternTotalityTests
{
    private const string SourceFile =
        "src/Sharpy.Compiler/Semantic/TypeChecker.Statements.Patterns.cs";

    private readonly ITestOutputHelper _output;

    public CheckPatternTotalityTests(ITestOutputHelper output) => _output = output;

    private static List<string> GetConcretePatternNames()
    {
        var patternBaseType = typeof(Pattern);
        return patternBaseType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(patternBaseType)
                        && !t.IsAbstract
                        && t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private static readonly HashSet<string> HandledArms = new()
    {
        nameof(WildcardPattern),
        nameof(BindingPattern),
        nameof(LiteralPattern),
        nameof(TuplePattern),
        nameof(TypePattern),
        nameof(RelationalPattern),
        nameof(OrPattern),
        nameof(GuardPattern),
        nameof(PropertyPattern),
        nameof(PositionalPattern),
        nameof(MemberAccessPattern),
        nameof(ListPattern),
        nameof(AsPattern),
        nameof(AndPattern),
        nameof(StarPattern),
    };

    // UnionCasePattern is the only kind outside the arms, so it is the only kind that COULD reach
    // the loud UnsupportedFeature default — but it has no construction site in the parser
    // (measured @ 277f54543: `case Circle(r):` over a union and `case Some(v):` over an Optional
    // both parse as PositionalPattern and run correctly), so no program can reach the default
    // through it and this entry is unfalsifiable by execution. Filed as a dead AST kind: #1730.
    // Until it is deleted or wired, the roster keeps it here so the universe assertion stays exact.
    private static readonly HashSet<string> LoudDefault = new()
    {
        nameof(UnionCasePattern),
    };

    [Fact]
    public void AllConcretePatternSubtypes_AreClassified()
    {
        var all = GetConcretePatternNames();
        var classified = new HashSet<string>(HandledArms);
        classified.UnionWith(LoudDefault);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        _output.WriteLine($"Concrete Pattern subtypes: {all.Count}");
        foreach (var name in all)
        {
            var group = HandledArms.Contains(name) ? "HANDLED"
                : LoudDefault.Contains(name) ? "LOUD DEFAULT"
                : "*** UNCLASSIFIED ***";
            _output.WriteLine($"  {name,-30} {group}");
        }

        if (unclassified.Count > 0)
            _output.WriteLine($"\nUnclassified: {string.Join(", ", unclassified)}");
        if (phantom.Count > 0)
            _output.WriteLine($"\nPhantom (listed but not found): {string.Join(", ", phantom)}");

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void SwitchArms_MatchHandledArms()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "CheckPattern");
        Assert.NotEmpty(switchArms);

        _output.WriteLine($"Switch arms found: {switchArms.Count}");
        foreach (var arm in switchArms.OrderBy(a => a, StringComparer.Ordinal))
            _output.WriteLine($"  {arm}");

        Assert.True(switchArms.SetEquals(HandledArms),
            $"CheckPattern switch arms differ from HandledArms.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(HandledArms))}\n" +
            $"  Missing from switch: {string.Join(", ", HandledArms.Except(switchArms))}");
    }

    [Fact]
    public void ClassificationSets_AreDisjoint()
    {
        var overlap = HandledArms.Intersect(LoudDefault).ToList();
        Assert.Empty(overlap);
    }
}
