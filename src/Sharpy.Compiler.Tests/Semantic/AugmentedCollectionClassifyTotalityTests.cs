using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for <c>AugmentedCollectionAssignment.Classify</c>: the tuple-pattern
/// switch <c>(node.Operator, targetType) switch</c> dispatches on (AssignmentOperator, GenericType)
/// pairs. Every arm pattern text must appear in the roster of documented operator-collection
/// pairs or the discard catch-all. A new arm that is not listed here will fail this test (#1694).
/// </summary>
public class AugmentedCollectionClassifyTotalityTests
{
    private const string SourceFile =
        "src/Sharpy.Compiler/Semantic/AugmentedCollectionAssignment.cs";

    private readonly ITestOutputHelper _output;

    public AugmentedCollectionClassifyTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> MutationArms = new()
    {
        "(AssignmentOperator.PlusAssign, GenericType { Name: \"list\" })",
        "(AssignmentOperator.StarAssign, GenericType { Name: \"list\" })",
        "(AssignmentOperator.OrAssign, GenericType { Name: \"set\" })",
        "(AssignmentOperator.AndAssign, GenericType { Name: \"set\" })",
        "(AssignmentOperator.MinusAssign, GenericType { Name: \"set\" })",
        "(AssignmentOperator.XorAssign, GenericType { Name: \"set\" })",
        "(AssignmentOperator.OrAssign, GenericType { Name: \"dict\" })",
    };

    /// <summary>
    /// The discard arm is the documented refusal for every non-mutating pair — including all
    /// frozenset rows: frozenset is excluded by design because augmented assignment REBINDS
    /// there (verified with python3, <c>f |= {3}</c> rebinds — see the remark on
    /// <c>AugmentedCollectionAssignment.Classify</c>, AugmentedCollectionAssignment.cs:41),
    /// so Sharpy already agrees with CPython without a mutation lowering.
    /// </summary>
    private static readonly HashSet<string> RefusalArms = new()
    {
        "_",
    };

    [Fact]
    public void ArmPatterns_MatchRoster()
    {
        var armTexts = SwitchArmScan.ArmPatternTexts(SourceFile, "Classify");
        Assert.NotEmpty(armTexts);

        var allExpected = new HashSet<string>(MutationArms);
        allExpected.UnionWith(RefusalArms);

        var armSet = new HashSet<string>(armTexts);
        var unexpected = armSet.Where(a => !allExpected.Contains(a)).ToList();
        var missing = allExpected.Where(a => !armSet.Contains(a)).ToList();

        _output.WriteLine($"Classify arm patterns: {armTexts.Count}");
        foreach (var text in armTexts)
        {
            var group = MutationArms.Contains(text) ? "MUTATION"
                : RefusalArms.Contains(text) ? "REFUSAL"
                : "*** UNEXPECTED ***";
            _output.WriteLine($"  {text,-70} {group}");
        }

        if (unexpected.Count > 0)
            _output.WriteLine($"\nUnexpected: {string.Join(", ", unexpected)}");
        if (missing.Count > 0)
            _output.WriteLine($"\nMissing: {string.Join(", ", missing)}");

        Assert.Empty(unexpected);
        Assert.Empty(missing);
    }

    [Fact]
    public void MutationAndRefusal_AreDisjoint()
    {
        var overlap = MutationArms.Intersect(RefusalArms).ToList();
        Assert.Empty(overlap);
    }
}
