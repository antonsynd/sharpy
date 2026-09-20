using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

/// <summary>
/// Totality guard for the ONE binding-form roster of
/// <c>DefiniteAssignmentAnalysis.ScopedChildren</c> — the walk that tells the definite-assignment
/// analysis which names a construct BINDS, and over which sub-tree (#1910, the #1681 follow-up).
///
/// <para>Why this exists: before #1910 the shadow set was seeded from two sources only (a nested
/// def's parameters and its <c>VariableDeclaration</c>s), so every OTHER binding form a deferred
/// body introduced was invisible and a read of the body's own binding was matched BY NAME against
/// an outer never-assigned bare local and refused with SPY0600. A missing arm here reproduces that
/// defect exactly, for whichever construct lost its arm. The behavioural cells live in
/// <c>DefiniteAssignmentMatrixTests</c> (<c>ScopeBindingForm_*</c>); this class pins the roster
/// itself so a NEW binding construct cannot be added to the AST and silently take the default arm.</para>
///
/// <para>The expected sets below are written as literals rather than derived from the production
/// source or from an enum, so they are an independent anchor: a deleted arm makes the scan set
/// smaller than the literal set and fails.</para>
/// </summary>
public class ScopeBindingFormMatrixTests
{
    private readonly ITestOutputHelper _output;

    public ScopeBindingFormMatrixTests(ITestOutputHelper output) => _output = output;

    private const string AnalysisFile = "src/Sharpy.Compiler/Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs";

    /// <summary>
    /// Every construct that introduces a scope-local binding, and therefore needs a
    /// <c>ScopedChildren</c> arm. Each name is an AST node type; the sub-tree each binding covers is
    /// documented at the arm.
    /// </summary>
    private static readonly HashSet<string> SubTreeScopedBinders = new()
    {
        // def: parameters + the body-flat forms, over the body
        nameof(FunctionDef),
        // lambda: parameters, over the body
        nameof(LambdaExpression),
        // for: the target, over target/body/else (NOT the iterator)
        nameof(ForStatement),
        // comprehension / generator clause: the clause target
        nameof(ForClause),
        // the five comprehension/generator kinds: every clause target, over element and if-clauses
        nameof(ListComprehension),
        nameof(SetComprehension),
        nameof(DictComprehension),
        nameof(DictSpreadComprehension),
        nameof(GeneratorExpression),
        // with … as: each item's target, over the later items and the body
        nameof(WithStatement),
        // except … as: the handler name, over that handler only
        nameof(TryStatement),
        // case patterns: the captures, over that case's pattern/guard/body
        nameof(MatchStatement),
        nameof(MatchExpression),
    };

    /// <summary>
    /// The two BODY-FLAT forms, plus the two scope boundaries the body-flat walk must not cross.
    /// A <c>VariableDeclaration</c> and a walrus target bind for the whole enclosing function body,
    /// so they are seeded when <c>ScopedChildren</c> enters a def rather than as traversal reaches
    /// them.
    /// </summary>
    private static readonly HashSet<string> BodyFlatArms = new()
    {
        nameof(VariableDeclaration),
        nameof(WalrusExpression),
        nameof(FunctionDef),
        nameof(LambdaExpression),
    };

    [Fact]
    public void ScopedChildren_SwitchArms_AreTheBindingFormRoster()
    {
        var arms = SwitchArmScan.CaseTypeNames(AnalysisFile, "ScopedChildren");
        _output.WriteLine($"ScopedChildren arms: {string.Join(", ", arms.OrderBy(a => a))}");

        Assert.True(arms.SetEquals(SubTreeScopedBinders),
            "ScopedChildren arms differ from the binding-form roster. A construct that binds a name "
            + "but has no arm makes a deferred body's own binding look like a read of a same-named "
            + "outer local (SPY0600 on a legal program — #1910).\n"
            + $"  Extra in source: {string.Join(", ", arms.Except(SubTreeScopedBinders))}\n"
            + $"  Missing from source: {string.Join(", ", SubTreeScopedBinders.Except(arms))}");
    }

    [Fact]
    public void CollectBodyFlatBindings_SwitchArms_AreTheBodyFlatForms()
    {
        var arms = SwitchArmScan.CaseTypeNames(AnalysisFile, "CollectBodyFlatBindings");
        _output.WriteLine($"CollectBodyFlatBindings arms: {string.Join(", ", arms.OrderBy(a => a))}");

        Assert.True(arms.SetEquals(BodyFlatArms),
            "CollectBodyFlatBindings arms differ from the body-flat roster.\n"
            + $"  Extra in source: {string.Join(", ", arms.Except(BodyFlatArms))}\n"
            + $"  Missing from source: {string.Join(", ", BodyFlatArms.Except(arms))}");
    }

    /// <summary>
    /// Positive control for the two roster facts above: every name they assert is a real, concrete
    /// AST node type. Without it a typo'd literal would sit in both the roster and the source and
    /// the SetEquals would pass while guarding nothing.
    /// </summary>
    [Fact]
    public void EveryRosteredArmName_IsAConcreteAstNodeType()
    {
        var nodeTypes = typeof(Node).Assembly
            .GetTypes()
            .Where(t => typeof(Node).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.Name)
            .ToHashSet();

        var rostered = new HashSet<string>(SubTreeScopedBinders);
        rostered.UnionWith(BodyFlatArms);

        var notAstTypes = rostered.Except(nodeTypes).OrderBy(n => n).ToList();
        Assert.True(notAstTypes.Count == 0,
            $"Rostered names that are not concrete AST node types: {string.Join(", ", notAstTypes)}");
    }

    /// <summary>
    /// The pattern-capture roster is NOT re-derived inside the analysis: <c>ScopedChildren</c>'s
    /// match arms ask <c>ControlFlowGraphBuilder.CollectPatternBindingKeysInto</c>, which
    /// <c>CfgPatternBindingTotalityTests</c> already pins against the concrete <c>Pattern</c>
    /// census. This fact keeps that single-authority claim honest: a second, drifting copy inside
    /// the analysis file would show up as a pattern-typed switch arm there.
    /// </summary>
    [Fact]
    public void PatternCaptures_AreNotReDerivedInTheAnalysis()
    {
        var scopedArms = SwitchArmScan.CaseTypeNames(AnalysisFile, "ScopedChildren");
        var patternKinds = typeof(Pattern).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Pattern)) && !t.IsAbstract)
            .Select(t => t.Name)
            .ToHashSet();

        var copied = scopedArms.Intersect(patternKinds).OrderBy(n => n).ToList();
        Assert.True(copied.Count == 0,
            "ScopedChildren dispatches on pattern kinds directly — that is a second capture roster "
            + "that can drift from ControlFlowGraphBuilder.CollectPatternBindingKeysInto: "
            + string.Join(", ", copied));
    }
}
