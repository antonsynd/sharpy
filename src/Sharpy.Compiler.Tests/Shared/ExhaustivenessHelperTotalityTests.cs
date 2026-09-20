using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Shared;

/// <summary>
/// Totality guard for <see cref="Sharpy.Compiler.Shared.ExhaustivenessHelper.CollectCoveredCases"/>
/// and <see cref="Sharpy.Compiler.Shared.ExhaustivenessHelper.IsIrrefutable"/>:
/// every concrete <see cref="Pattern"/> subtype must be classified into one of the sets below.
/// A new Pattern subtype that is not listed here fails this test, forcing deliberate classification.
/// </summary>
public class ExhaustivenessHelperTotalityTests
{
    private readonly ITestOutputHelper _output;

    public ExhaustivenessHelperTotalityTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Non-head patterns handled directly by CollectCoveredCases — they contribute a case name to
    /// the covered set. The three class-pattern HEADS (Type/Positional/Property) route through
    /// <see cref="Sharpy.Compiler.Shared.PatternHead"/> instead (see <see cref="HeadContributing"/>).
    /// </summary>
    private static readonly HashSet<string> CoverageContributing = new()
    {
        nameof(LiteralPattern),
        nameof(BindingPattern),
        nameof(MemberAccessPattern),
        nameof(AsPattern),
        nameof(OrPattern),
    };

    /// <summary>
    /// The class-pattern head kinds classified by <c>PatternHead.TryGet</c> — <c>case C():</c>,
    /// <c>case C(v):</c> and <c>case C(f=v):</c>. CollectCoveredCases, IsTotal, DescribeIrrefutable
    /// and the subsumption validator all read the head through this ONE classifier (P13 DD8); the
    /// property form used to be uncounted, which drew a spurious SPY0463 (#1890).
    /// </summary>
    private static readonly HashSet<string> HeadContributing = new()
    {
        nameof(TypePattern),
        nameof(PositionalPattern),
        nameof(PropertyPattern),
    };

    /// <summary>
    /// Patterns handled by IsTotal but NOT by CollectCoveredCases — they match everything
    /// or are structural wrappers that don't name a specific case.
    /// </summary>
    private static readonly HashSet<string> IrrefutableOnly = new()
    {
        nameof(WildcardPattern),
        nameof(GuardPattern),
    };

    /// <summary>
    /// Patterns that don't contribute case names and are not irrefutable by themselves.
    /// They either are value constraints (relational) or structural patterns whose
    /// coverage contribution comes from their sub-patterns, not from the pattern itself.
    /// </summary>
    private static readonly HashSet<string> NoCoverage = new()
    {
        nameof(TuplePattern),
        nameof(RelationalPattern),
        nameof(ListPattern),
        nameof(StarPattern),
        nameof(AndPattern),
    };

    [Fact]
    public void AllConcretePatternSubtypes_AreClassified()
    {
        var patternBaseType = typeof(Pattern);
        var assembly = patternBaseType.Assembly;

        var concretePatterns = assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(patternBaseType)
                        && !t.IsAbstract
                        && t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var allClassified = new HashSet<string>(CoverageContributing);
        allClassified.UnionWith(HeadContributing);
        allClassified.UnionWith(IrrefutableOnly);
        allClassified.UnionWith(NoCoverage);

        var unclassified = concretePatterns.Where(n => !allClassified.Contains(n)).ToList();
        var phantom = allClassified.Where(n => !concretePatterns.Contains(n)).ToList();

        _output.WriteLine($"Concrete Pattern subtypes: {concretePatterns.Count}");
        foreach (var name in concretePatterns)
        {
            var group = CoverageContributing.Contains(name) ? "COVERAGE"
                : HeadContributing.Contains(name) ? "HEAD"
                : IrrefutableOnly.Contains(name) ? "IRREFUTABLE-ONLY"
                : NoCoverage.Contains(name) ? "NO-COVERAGE"
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
    public void SwitchArms_MatchCoverageContributing()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Shared/ExhaustivenessHelper.cs",
            "CollectCoveredCases");

        Assert.NotEmpty(switchArms);

        // SetEquals, not subset: an arm added for a pattern rostered elsewhere is drift.
        Assert.True(switchArms.SetEquals(CoverageContributing),
            $"CollectCoveredCases switch arms differ from CoverageContributing roster.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(CoverageContributing))}\n" +
            $"  Missing from switch: {string.Join(", ", CoverageContributing.Except(switchArms))}");
    }

    /// <summary>
    /// The class-pattern head kinds PatternHead.TryGet classifies — its switch arms are the three
    /// head kinds plus the AsPattern unwrap. This is the ONE switch on the head kinds (P13 DD8);
    /// dropping the Property arm here is the Phase 9 mutation that reddens D1 (exhaustiveness counts
    /// the property form) and D4 (property-form subsumption) together.
    /// </summary>
    private static readonly HashSet<string> PatternHeadArms = new()
    {
        nameof(AsPattern),
        nameof(TypePattern),
        nameof(PositionalPattern),
        nameof(PropertyPattern),
    };

    [Fact]
    public void SwitchArms_MatchPatternHead()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Shared/PatternHead.cs",
            "TryGet");

        Assert.NotEmpty(switchArms);

        Assert.True(switchArms.SetEquals(PatternHeadArms),
            $"PatternHead.TryGet switch arms differ from the pinned roster.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(PatternHeadArms))}\n" +
            $"  Missing from switch: {string.Join(", ", PatternHeadArms.Except(switchArms))}");
    }

    /// <summary>
    /// The patterns IsTotal dispatches on by TYPE (its named switch-expression arms). The three
    /// class-pattern heads are reached through the <c>_ when PatternHead.TryGet(...)</c> guard arm,
    /// which names no type; every other pattern falls to the `_ => false` discard by design (DD9).
    /// </summary>
    private static readonly HashSet<string> IsTotalArms = new()
    {
        nameof(WildcardPattern),
        nameof(BindingPattern),
        nameof(AsPattern),
        nameof(OrPattern),
        nameof(GuardPattern),
    };

    [Fact]
    public void SwitchArms_MatchIsTotal()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Shared/ExhaustivenessHelper.cs",
            "IsTotal");

        Assert.NotEmpty(switchArms);

        // Pinned arm set, SetEquals: the previous subset assertion could not fail on
        // arm REMOVAL (every remaining arm stayed classified). The heads route through the
        // PatternHead.TryGet guard arm; everything else falls to the `_ => false` discard.
        Assert.True(switchArms.SetEquals(IsTotalArms),
            $"IsTotal switch arms differ from the pinned roster.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(IsTotalArms))}\n" +
            $"  Missing from switch: {string.Join(", ", IsTotalArms.Except(switchArms))}");
    }

    [Fact]
    public void DescribeIrrefutable_Arms_MirrorIsTotal_MinusGuardPattern()
    {
        var describeArms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Shared/ExhaustivenessHelper.cs",
            "DescribeIrrefutable");

        var expectedArms = new HashSet<string>(IsTotalArms);
        expectedArms.Remove(nameof(GuardPattern));

        Assert.True(describeArms.SetEquals(expectedArms),
            $"DescribeIrrefutable arms must equal IsTotal arms minus GuardPattern.\n" +
            $"  Extra in DescribeIrrefutable: {string.Join(", ", describeArms.Except(expectedArms))}\n" +
            $"  Missing from DescribeIrrefutable: {string.Join(", ", expectedArms.Except(describeArms))}");
    }

    [Fact]
    public void ClassificationSets_AreDisjoint()
    {
        var covAndHead = CoverageContributing.Intersect(HeadContributing).ToList();
        var covAndIrr = CoverageContributing.Intersect(IrrefutableOnly).ToList();
        var covAndNo = CoverageContributing.Intersect(NoCoverage).ToList();
        var headAndIrr = HeadContributing.Intersect(IrrefutableOnly).ToList();
        var headAndNo = HeadContributing.Intersect(NoCoverage).ToList();
        var irrAndNo = IrrefutableOnly.Intersect(NoCoverage).ToList();

        Assert.Empty(covAndHead);
        Assert.Empty(covAndIrr);
        Assert.Empty(covAndNo);
        Assert.Empty(headAndIrr);
        Assert.Empty(headAndNo);
        Assert.Empty(irrAndNo);
    }
}
