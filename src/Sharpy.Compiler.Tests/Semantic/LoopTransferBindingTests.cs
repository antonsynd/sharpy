using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Unit tests for the loop-transfer binding fact recorded by
/// <c>LoopTransferBindingValidator</c> (#1816): every break/continue is bound to its innermost
/// enclosing loop, and a match a break crosses is marked as hosting a transfer.
/// </summary>
public class LoopTransferBindingTests
{
    private static CompilationResult Analyze(string source)
    {
        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        return compiler.Analyze(source, "test.spy");
    }

    private static List<T> Collect<T>(Node root) where T : Node
    {
        var result = new List<T>();
        void Walk(Node node)
        {
            if (node is T match)
                result.Add(match);
            foreach (var child in node.GetChildNodes())
                Walk(child);
        }
        Walk(root);
        return result;
    }

    [Fact]
    public void Break_InMatch_InFor_TargetsTheFor_AndMarksTheMatch()
    {
        var source = @"
def f() -> None:
    for i in range(3):
        match i:
            case 1:
                break
            case _:
                print(i)
";
        var result = Analyze(source);
        result.Success.Should().BeTrue();

        var forStmt = Collect<ForStatement>(result.Module!).Single();
        var matchStmt = Collect<MatchStatement>(result.Module!).Single();
        var breakStmt = Collect<BreakStatement>(result.Module!).Single();

        var target = result.SemanticInfo!.GetLoopTransferTarget(breakStmt);
        target.Should().NotBeNull();
        target!.TargetLoop.Should().BeSameAs(forStmt);
        target.CrossesMatch.Should().BeTrue();

        result.SemanticInfo!.GetMatchHostsLoopTransfer(matchStmt).Should().BeTrue();
    }

    [Fact]
    public void Break_InNestedLoop_InsideArm_TargetsInnerLoop_AndDoesNotMarkMatch()
    {
        var source = @"
def f() -> None:
    for i in range(3):
        match i:
            case 1:
                for j in range(2):
                    break
            case _:
                print(i)
";
        var result = Analyze(source);
        result.Success.Should().BeTrue();

        // Document order: [0] is the outer `for i`, [1] is the inner `for j`.
        var fors = Collect<ForStatement>(result.Module!);
        fors.Should().HaveCount(2);
        var innerFor = fors[1];
        var matchStmt = Collect<MatchStatement>(result.Module!).Single();
        var breakStmt = Collect<BreakStatement>(result.Module!).Single();

        var target = result.SemanticInfo!.GetLoopTransferTarget(breakStmt);
        target.Should().NotBeNull();
        target!.TargetLoop.Should().BeSameAs(innerFor);
        target.CrossesMatch.Should().BeFalse();

        // The break stops at the inner loop, so the outer match is NOT a transfer host.
        result.SemanticInfo!.GetMatchHostsLoopTransfer(matchStmt).Should().BeFalse();
    }

    [Fact]
    public void Break_DirectlyInFor_TargetsTheFor_WithNoMatchCrossed()
    {
        var source = @"
def f() -> None:
    for i in range(3):
        if i == 1:
            break
        print(i)
";
        var result = Analyze(source);
        result.Success.Should().BeTrue();

        var forStmt = Collect<ForStatement>(result.Module!).Single();
        var breakStmt = Collect<BreakStatement>(result.Module!).Single();

        var target = result.SemanticInfo!.GetLoopTransferTarget(breakStmt);
        target.Should().NotBeNull();
        target!.TargetLoop.Should().BeSameAs(forStmt);
        target.CrossesMatch.Should().BeFalse();
    }

    [Fact]
    public void Continue_InMatch_InFor_TargetsTheFor_ButDoesNotMarkTheMatch()
    {
        var source = @"
def f() -> None:
    for i in range(3):
        match i:
            case 1:
                continue
            case _:
                print(i)
";
        var result = Analyze(source);
        result.Success.Should().BeTrue();

        var forStmt = Collect<ForStatement>(result.Module!).Single();
        var matchStmt = Collect<MatchStatement>(result.Module!).Single();
        var continueStmt = Collect<ContinueStatement>(result.Module!).Single();

        var target = result.SemanticInfo!.GetLoopTransferTarget(continueStmt);
        target.Should().NotBeNull();
        target!.TargetLoop.Should().BeSameAs(forStmt);

        // A continue in a C# switch already targets the enclosing loop, so a continue-only match is
        // NOT marked (Design Decision 1) — keeping the switch avoids snapshot churn.
        result.SemanticInfo!.GetMatchHostsLoopTransfer(matchStmt).Should().BeFalse();
    }
}
