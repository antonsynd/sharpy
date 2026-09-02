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
/// <c>IsValidAssignmentTarget</c> calls <c>UnwrapParenthesized</c> at its seam,
/// so downstream sites receive canonical (unwrapped) targets. Sites that operate
/// on raw AST (before the checker's unwrap) may include <c>Parenthesized</c> as
/// a recursion arm — that is a superset, not a universe change, and is documented
/// per site. <c>ListLiteral</c> appears in two sites defensively (Python admits
/// <c>[a, b] = t</c>; Sharpy's parser may produce it in error recovery).
///
/// A new assignment-target kind must be added to <c>IsValidAssignmentTarget</c>
/// first, which causes <c>Universe_MatchesAuthority</c> to fail, then to every
/// member site whose reason does not already cover it.
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

    // --- ControlFlowGraphBuilder.CollectBindingKeysInto ---
    // Arms: {Parenthesized, TupleLiteral}; default delegates to ExtractNarrowingKey
    // which handles Identifier, MemberAccess, IndexAccess. Parenthesized is explicit
    // because this site operates on raw targets (before the checker's unwrap seam).
    // StarExpression falls to the default (ExtractNarrowingKey returns null for it,
    // which is correct — star targets don't produce narrowing keys).

    [Fact]
    public void CollectBindingKeysInto_Arms_AreKnown()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs",
            "CollectBindingKeysInto");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CollectBindingKeysInto arms: {string.Join(", ", arms.OrderBy(a => a))}");

        var expected = new HashSet<string> { nameof(Parenthesized), nameof(TupleLiteral) };
        Assert.True(arms.SetEquals(expected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }

    // --- DefiniteAssignmentAnalysis.CollectAssignedNames ---
    // Arms: universe + Parenthesized = {Identifier, TupleLiteral, StarExpression,
    // Parenthesized, IndexAccess, MemberAccess}. IndexAccess and MemberAccess are
    // no-op arms (they mutate a container, not rebind a name). Parenthesized recurses.
    // Operates on raw targets (before the checker's unwrap).

    [Fact]
    public void CollectAssignedNames_Arms_AreKnown()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs",
            "CollectAssignedNames");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CollectAssignedNames arms: {string.Join(", ", arms.OrderBy(a => a))}");

        var expected = new HashSet<string>(Universe) { nameof(Parenthesized) };
        Assert.True(arms.SetEquals(expected),
            $"Arms differ from expected (universe + Parenthesized).\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }

    // --- DefiniteAssignmentAnalysis.CollectTargetReads ---
    // Arms: {Identifier, TupleLiteral, StarExpression, Parenthesized}; default
    // calls CollectReadsFromExpr for IndexAccess/MemberAccess sub-expression reads.
    // Identifier is a no-op (pure binding, no read). Parenthesized recurses.

    [Fact]
    public void CollectTargetReads_Arms_AreKnown()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs",
            "CollectTargetReads");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CollectTargetReads arms: {string.Join(", ", arms.OrderBy(a => a))}");

        var expected = new HashSet<string>
        {
            nameof(Identifier), nameof(TupleLiteral),
            nameof(StarExpression), nameof(Parenthesized),
        };
        Assert.True(arms.SetEquals(expected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }

    // --- ReassignmentFinder.TargetBindsName ---
    // Arms: {Identifier, Parenthesized, StarExpression, TupleLiteral, ListLiteral};
    // default returns false. MemberAccess/IndexAccess in the default — correct,
    // they don't rebind names. ListLiteral is defensive (Python admits [a,b] = t;
    // parser may produce it). Parenthesized recurses.

    [Fact]
    public void TargetBindsName_Arms_AreKnown()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs",
            "TargetBindsName");
        Assert.NotEmpty(arms);
        _output.WriteLine($"TargetBindsName arms: {string.Join(", ", arms.OrderBy(a => a))}");

        var expected = new HashSet<string>
        {
            nameof(Identifier), nameof(Parenthesized),
            nameof(StarExpression), nameof(TupleLiteral), nameof(ListLiteral),
        };
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

        var expected = new HashSet<string>
        {
            nameof(Identifier), nameof(TupleLiteral),
            nameof(ListLiteral), nameof(StarExpression),
        };
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

        var expected = new HashSet<string>
        {
            nameof(Identifier), nameof(TupleLiteral), nameof(StarExpression),
        };
        Assert.True(arms.SetEquals(expected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }
}
