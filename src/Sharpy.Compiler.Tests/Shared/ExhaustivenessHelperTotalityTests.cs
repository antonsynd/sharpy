using Sharpy.Compiler.Parser.Ast;
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
    /// Patterns handled by CollectCoveredCases — they contribute a case name to the covered set.
    /// </summary>
    private static readonly HashSet<string> CoverageContributing = new()
    {
        nameof(LiteralPattern),
        nameof(BindingPattern),
        nameof(MemberAccessPattern),
        nameof(PositionalPattern),
        nameof(TypePattern),
        nameof(AsPattern),
        nameof(OrPattern),
    };

    /// <summary>
    /// Patterns handled by IsIrrefutable but NOT by CollectCoveredCases — they match everything
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
        nameof(UnionCasePattern),
        nameof(TuplePattern),
        nameof(RelationalPattern),
        nameof(ListPattern),
        nameof(StarPattern),
        nameof(PropertyPattern),
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
        allClassified.UnionWith(IrrefutableOnly);
        allClassified.UnionWith(NoCoverage);

        var unclassified = concretePatterns.Where(n => !allClassified.Contains(n)).ToList();
        var phantom = allClassified.Where(n => !concretePatterns.Contains(n)).ToList();

        _output.WriteLine($"Concrete Pattern subtypes: {concretePatterns.Count}");
        foreach (var name in concretePatterns)
        {
            var group = CoverageContributing.Contains(name) ? "COVERAGE"
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
    public void ClassificationSets_AreDisjoint()
    {
        var covAndIrr = CoverageContributing.Intersect(IrrefutableOnly).ToList();
        var covAndNo = CoverageContributing.Intersect(NoCoverage).ToList();
        var irrAndNo = IrrefutableOnly.Intersect(NoCoverage).ToList();

        Assert.Empty(covAndIrr);
        Assert.Empty(covAndNo);
        Assert.Empty(irrAndNo);
    }
}
