using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Records, for every <see cref="BreakStatement"/>/<see cref="ContinueStatement"/>, the loop it
/// binds to, and marks each <see cref="MatchStatement"/> that a <c>break</c> crosses on its way to
/// that loop (#1816). The emitter reads these facts: a match that hosts a loop transfer lowers to
/// the <c>is</c>-chain (so a plain C# <c>break</c> escapes to the loop rather than the manufactured
/// <c>switch</c>), and <c>GenerateBreak</c> clears the recorded loop's <c>else</c> flag.
/// </summary>
/// <remarks>
/// This validator does not emit diagnostics — <see cref="ControlFlowValidator"/> (Order 400) already
/// refuses a transfer with no enclosing loop, so a null target here is simply not recorded. A
/// <c>break</c>/<c>continue</c> in a nested loop inside a match arm targets that nested loop and does
/// NOT mark the outer match (the frame walk stops at the first loop). Function and lambda boundaries
/// reset the frame stack: a nested <c>def</c>'s loops are independent of the enclosing ones.
///
/// <para>Only a <c>break</c> marks a crossed match as hosting a transfer: a <c>continue</c> inside a
/// C# <c>switch</c> already targets the enclosing loop (Python agrees), so a continue-only match
/// gains nothing from the <c>is</c>-chain and keeping the <c>switch</c> avoids snapshot churn
/// (Design Decision 1).</para>
/// </remarks>
internal sealed class LoopTransferBindingValidator : ValidatingAstWalker
{
    public override string Name => "LoopTransferBindingValidator";

    // After ControlFlowValidator (400, refuses transfers with no enclosing loop) and before
    // DefiniteAssignmentValidator (402).
    public override int Order => 401;

    // A unified stack of enclosing loop/match statements, innermost on top. Each frame is a
    // ForStatement, WhileStatement, or MatchStatement.
    private Stack<Statement> _frames = new();

    public override void Validate(Module module, SemanticContext context)
    {
        _frames = new Stack<Statement>();
        base.Validate(module, context);
    }

    public override void VisitForStatement(ForStatement node)
    {
        // Target/iterator are evaluated outside the loop body; a transfer there (illegal) targets the
        // enclosing loop, so visit them with the loop not yet pushed.
        Visit(node.Target);
        Visit(node.Iterator);

        _frames.Push(node);
        foreach (var stmt in node.Body)
            Visit(stmt);
        _frames.Pop();

        // The else clause runs after the loop; a transfer there targets the OUTER loop.
        foreach (var stmt in node.ElseBody)
            Visit(stmt);
    }

    public override void VisitWhileStatement(WhileStatement node)
    {
        Visit(node.Test);

        _frames.Push(node);
        foreach (var stmt in node.Body)
            Visit(stmt);
        _frames.Pop();

        foreach (var stmt in node.ElseBody)
            Visit(stmt);
    }

    public override void VisitMatchStatement(MatchStatement node)
    {
        Visit(node.Scrutinee);
        foreach (var matchCase in node.Cases)
        {
            Visit(matchCase.Pattern);
            if (matchCase.Guard != null)
                Visit(matchCase.Guard);

            _frames.Push(node);
            foreach (var stmt in matchCase.Body)
                Visit(stmt);
            _frames.Pop();
        }
    }

    public override void VisitFunctionDef(FunctionDef node)
    {
        // A nested def's loops are independent of the enclosing ones (a transfer in the def targets
        // no outer loop — ControlFlowValidator refuses it).
        var saved = _frames;
        _frames = new Stack<Statement>();
        DefaultVisit(node);
        _frames = saved;
    }

    public override void VisitLambdaExpression(LambdaExpression node)
    {
        var saved = _frames;
        _frames = new Stack<Statement>();
        DefaultVisit(node);
        _frames = saved;
    }

    public override void VisitBreakStatement(BreakStatement node)
        => RecordTransfer(node, isBreak: true);

    public override void VisitContinueStatement(ContinueStatement node)
        => RecordTransfer(node, isBreak: false);

    private void RecordTransfer(Node transferNode, bool isBreak)
    {
        Statement? targetLoop = null;
        List<MatchStatement>? crossedMatches = null;

        foreach (var frame in _frames) // top (innermost) to bottom
        {
            if (frame is MatchStatement matchStmt)
            {
                (crossedMatches ??= new List<MatchStatement>()).Add(matchStmt);
            }
            else // ForStatement or WhileStatement — the innermost enclosing loop
            {
                targetLoop = frame;
                break;
            }
        }

        if (targetLoop == null)
            return; // transfer with no enclosing loop — ControlFlowValidator emits the error

        bool crossesMatch = crossedMatches is { Count: > 0 };
        Context.SemanticInfo.SetLoopTransferTarget(
            transferNode, new LoopTransferTarget(targetLoop, crossesMatch));

        if (isBreak && crossedMatches != null)
        {
            foreach (var matchStmt in crossedMatches)
                Context.SemanticInfo.SetMatchHostsLoopTransfer(matchStmt);
        }
    }
}
