using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Enforces dunder method invocation rules:
/// - Dunder methods cannot be called directly from non-dunder code (SPY0460)
/// - Inside a dunder, dunder calls must be on self or super() (SPY0461)
/// - Dunder method references cannot be captured (SPY0462)
///
/// See docs/language_specification/dunder_invocation_rules.md for the full spec.
/// </summary>
internal class DunderInvocationValidator : ValidatingAstWalker
{
    public override string Name => "DunderInvocationValidator";
    public override int Order => 460; // After AccessValidator (450), before ProtocolValidator (500)

    private bool _inDunderMethod;

    public override void Validate(Module module, SemanticContext context)
    {
        _inDunderMethod = false;
        base.Validate(module, context);
    }

    public override void VisitFunctionDef(FunctionDef node)
    {
        var wasDunder = _inDunderMethod;
        _inDunderMethod = DunderDetector.IsDunderMethod(node.Name);

        base.VisitFunctionDef(node);

        _inDunderMethod = wasDunder;
    }

    public override void VisitFunctionCall(FunctionCall node)
    {
        if (node.Function is MemberAccess memberAccess
            && DunderDetector.IsDunderMethod(memberAccess.Member)
            && !DunderDetector.IsDunderProperty(memberAccess.Member))
        {
            // This is a dunder method call (e.g., self.__eq__(other))
            var dunderName = memberAccess.Member;

            if (!_inDunderMethod)
            {
                // SPY0460: Calling a dunder from outside a dunder method
                AddError(
                    $"Cannot invoke dunder method '{dunderName}' directly. Use the corresponding operator or built-in function.",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Validation.DunderDirectInvocation,
                    span: memberAccess.Span);
            }
            else if (!IsSelfOrSuper(memberAccess.Object))
            {
                // SPY0461: Dunder call on wrong receiver
                AddError(
                    $"Dunder method '{dunderName}' can only be called on 'self' or 'super()' within another dunder method.",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Validation.DunderWrongReceiver,
                    span: memberAccess.Span);
            }

            // Recurse into the receiver object (but not the MemberAccess itself — we handled it)
            Visit(memberAccess.Object);

            // Always validate arguments
            foreach (var arg in node.Arguments)
                Visit(arg);
            foreach (var kwArg in node.KeywordArguments)
                Visit(kwArg.Value);
        }
        else
        {
            // Not a dunder call — let the base traversal handle all children normally
            base.VisitFunctionCall(node);
        }
    }

    public override void VisitMemberAccess(MemberAccess node)
    {
        // Dunder properties (e.g., __name__, __doc__) are attributes, not methods
        if (!DunderDetector.IsDunderProperty(node.Member)
            && DunderDetector.IsDunderMethod(node.Member))
        {
            // A dunder MemberAccess that is NOT a direct call target is a capture.
            // (Dunder MemberAccess nodes that ARE call targets are handled in VisitFunctionCall
            // and never reach here because we skip the base traversal for those.)
            AddError(
                $"Cannot capture dunder method reference '{node.Member}'. Dunder methods must be called immediately.",
                node.LineStart, node.ColumnStart,
                code: DiagnosticCodes.Validation.DunderCapture,
                span: node.Span);
        }

        base.VisitMemberAccess(node);
    }

    private static bool IsSelfOrSuper(Expression expr)
    {
        return expr is Identifier { Name: PythonNames.Self } || expr is SuperExpression;
    }
}
