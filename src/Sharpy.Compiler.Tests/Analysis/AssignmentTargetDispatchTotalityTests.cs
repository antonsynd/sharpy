using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Analysis;

/// <summary>
/// Family guard for dispatch sites over assignment-target expression kinds.
/// The universe is the arm set of <c>TypeChecker.IsValidAssignmentTarget</c>
/// (the sole authority for what the parser admits as an assignment target):
/// {Identifier, MemberAccess, IndexAccess, TupleLiteral, StarExpression}.
///
/// The PARSER canonicalizes every store target through
/// <c>AstHelper.CanonicalizeStoreTarget</c> (assignment, annotated declaration, for /
/// comprehension target, with-as target), so NO member site — raw-AST analyses included —
/// can ever receive a <c>Parenthesized</c> target; the only surviving wrapper is the refused
/// <c>(*a)</c> shape, which reaches the authority's default arm. A <c>Parenthesized</c> arm in
/// any member site is therefore dead code and <c>NoMemberSite_HasParenthesizedArm</c> fails on it;
/// the parser seam itself is pinned by <c>StoreTargetCanonicalizationTests</c>.
/// <c>ListLiteral</c> appears in two sites defensively (Python admits <c>[a, b] = t</c>;
/// Sharpy's parser may produce it in error recovery).
///
/// A new assignment-target kind must be added to <c>IsValidAssignmentTarget</c>
/// first, which causes <c>Universe_MatchesAuthority</c> to fail, then to every
/// member site whose reason does not already cover it.
///
/// mutation (verify round 2026-09-02): <c>Parenthesized</c> arm re-added to
/// <c>CollectBindingKeysInto</c> → <c>CollectBindingKeysInto_Arms_AreKnown</c> and
/// <c>NoMemberSite_HasParenthesizedArm</c> red; restored → green.
/// </summary>
public class AssignmentTargetDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public AssignmentTargetDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> Universe = new()
    {
        nameof(Identifier),
        nameof(MemberAccess),
        nameof(IndexAccess),
        nameof(TupleLiteral),
        nameof(StarExpression),
    };

    /// <summary>
    /// Every member site's expectation is DERIVED from <see cref="Universe"/>: the kinds it
    /// handles by an explicit arm are the universe minus the kinds it routes through its default
    /// (each named with a reason at the site), plus any deliberately extra arm. A new
    /// assignment-target kind therefore fails every member fact at once (Design Decision 4 of
    /// plan-950124), not only <c>Universe_MatchesAuthority</c>.
    /// </summary>
    private static HashSet<string> Expect(IEnumerable<string> routedThroughDefault, params string[] extraArms)
    {
        var set = new HashSet<string>(Universe);
        set.ExceptWith(routedThroughDefault);
        set.UnionWith(extraArms);
        return set;
    }

    // --- Universe authority: IsValidAssignmentTarget ---

    [Fact]
    public void Universe_MatchesAuthority()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs",
            "IsValidAssignmentTarget");
        Assert.NotEmpty(arms);
        _output.WriteLine($"IsValidAssignmentTarget arms: {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(Universe),
            $"Universe authority arms differ from stated universe.\n" +
            $"  Extra in authority: {string.Join(", ", arms.Except(Universe))}\n" +
            $"  Missing from authority: {string.Join(", ", Universe.Except(arms))}");
    }

    // Every member site whose arms this class pins, as (file, method). Used by
    // NoMemberSite_HasParenthesizedArm: the parser seam guarantees no Parenthesized target
    // reaches any of them, so an arm for it is dead code that would hide a seam regression.
    private static readonly (string File, string Method)[] MemberSites =
    {
        ("src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs", "IsValidAssignmentTarget"),
        ("src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs", "CollectBindingKeysInto"),
        ("src/Sharpy.Compiler/Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs", "CollectAssignedNames"),
        ("src/Sharpy.Compiler/Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs", "CollectTargetReads"),
        ("src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs", "TargetBindsName"),
        ("src/Sharpy.Lsp/Refactoring/ScopeAnalyzer.cs", "CollectAssignmentTargets"),
        ("src/Sharpy.Lsp/Handlers/InlayHintHandler.cs", "MarkTargetBound"),
    };

    [Fact]
    public void NoMemberSite_HasParenthesizedArm()
    {
        var offenders = new List<string>();
        foreach (var (file, method) in MemberSites)
        {
            var arms = SwitchArmScan.CaseTypeNames(file, method);
            Assert.NotEmpty(arms);
            if (arms.Contains(nameof(Parenthesized)))
                offenders.Add($"{file}::{method}");
        }

        Assert.True(offenders.Count == 0,
            "Parenthesized targets are canonicalized by the parser (AstHelper.CanonicalizeStoreTarget); " +
            "these member sites carry a dead Parenthesized arm:\n  " + string.Join("\n  ", offenders));
    }

    // --- ControlFlowGraphBuilder.CollectBindingKeysInto ---
    // Arms: {TupleLiteral}; default delegates to ExtractNarrowingKey which handles
    // Identifier, MemberAccess, IndexAccess. StarExpression falls to the default
    // (ExtractNarrowingKey returns null for it, which is correct — star targets don't
    // produce narrowing keys). Targets are canonical: no Parenthesized arm.

    [Fact]
    public void CollectBindingKeysInto_Arms_AreKnown()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs",
            "CollectBindingKeysInto");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CollectBindingKeysInto arms: {string.Join(", ", arms.OrderBy(a => a))}");

        // Identifier/MemberAccess/IndexAccess → default → ExtractNarrowingKey; StarExpression → default → null.
        var expected = Expect(new[] { nameof(Identifier), nameof(MemberAccess), nameof(IndexAccess), nameof(StarExpression) });
        Assert.True(arms.SetEquals(expected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }

    // --- DefiniteAssignmentAnalysis.CollectAssignedNames ---
    // Arms == universe = {Identifier, TupleLiteral, StarExpression, IndexAccess, MemberAccess}.
    // IndexAccess and MemberAccess are no-op arms (they mutate a container, not rebind a name).
    // Targets are canonical: no Parenthesized arm.

    [Fact]
    public void CollectAssignedNames_Arms_AreKnown()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs",
            "CollectAssignedNames");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CollectAssignedNames arms: {string.Join(", ", arms.OrderBy(a => a))}");

        var expected = Expect(Array.Empty<string>());
        Assert.True(arms.SetEquals(expected),
            $"Arms differ from expected (universe).\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }

    // --- DefiniteAssignmentAnalysis.CollectTargetReads ---
    // Arms: {Identifier, TupleLiteral, StarExpression}; default calls CollectReadsFromExpr
    // for IndexAccess/MemberAccess sub-expression reads. Identifier is a no-op (pure binding,
    // no read). Targets are canonical: no Parenthesized arm.

    [Fact]
    public void CollectTargetReads_Arms_AreKnown()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs",
            "CollectTargetReads");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CollectTargetReads arms: {string.Join(", ", arms.OrderBy(a => a))}");

        // MemberAccess/IndexAccess → default → CollectReadsFromExpr (their sub-expressions are reads).
        var expected = Expect(new[] { nameof(MemberAccess), nameof(IndexAccess) });
        Assert.True(arms.SetEquals(expected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }

    // --- ReassignmentFinder.TargetBindsName ---
    // Arms: {Identifier, StarExpression, TupleLiteral, ListLiteral}; default returns
    // false. MemberAccess/IndexAccess in the default — correct, they don't rebind names.
    // ListLiteral is defensive (Python admits [a,b] = t; parser may produce it).
    // Targets are canonical: no Parenthesized arm.

    [Fact]
    public void TargetBindsName_Arms_AreKnown()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs",
            "TargetBindsName");
        Assert.NotEmpty(arms);
        _output.WriteLine($"TargetBindsName arms: {string.Join(", ", arms.OrderBy(a => a))}");

        // MemberAccess/IndexAccess → default (false: they mutate, not rebind); ListLiteral extra (defensive, #1733).
        var expected = Expect(new[] { nameof(MemberAccess), nameof(IndexAccess) }, nameof(ListLiteral));
        Assert.True(arms.SetEquals(expected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }

    // --- SelectionVisitor.CollectAssignmentTargets (LSP) ---
    // Arms: {Identifier, TupleLiteral, ListLiteral, StarExpression}; default walks
    // sub-expressions as reads. MemberAccess/IndexAccess in the default (they are
    // reads, not local variable assignments). ListLiteral defensive as above.

    [Fact]
    public void CollectAssignmentTargets_Arms_AreKnown()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/Refactoring/ScopeAnalyzer.cs",
            "CollectAssignmentTargets");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CollectAssignmentTargets arms: {string.Join(", ", arms.OrderBy(a => a))}");

        // MemberAccess/IndexAccess → default (walked as reads); ListLiteral extra (defensive, #1733).
        var expected = Expect(new[] { nameof(MemberAccess), nameof(IndexAccess) }, nameof(ListLiteral));
        Assert.True(arms.SetEquals(expected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }

    // --- InlayHintHandler.MarkTargetBound (LSP) ---
    // Arms: {Identifier, TupleLiteral, StarExpression}; no default.
    // Handles only name-binding targets: MemberAccess/IndexAccess don't bind loop
    // variable names, so they are intentionally omitted.

    [Fact]
    public void MarkTargetBound_Arms_AreKnown()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/Handlers/InlayHintHandler.cs",
            "MarkTargetBound");
        Assert.NotEmpty(arms);
        _output.WriteLine($"MarkTargetBound arms: {string.Join(", ", arms.OrderBy(a => a))}");

        // MemberAccess/IndexAccess intentionally omitted: they do not bind loop-variable names.
        var expected = Expect(new[] { nameof(MemberAccess), nameof(IndexAccess) });
        Assert.True(arms.SetEquals(expected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }
}
