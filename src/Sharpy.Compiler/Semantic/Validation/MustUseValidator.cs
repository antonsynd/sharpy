using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Warns (SPY0480) when a "must-use" value is produced as a bare expression statement and thrown
/// away (#1022). The carriers are <see cref="ResultType"/> and <see cref="OptionalType"/> — types
/// whose whole purpose is to make failure/absence explicit — so discarding one usually hides an
/// unhandled error. The escapes are clean by construction: <c>_ = expr</c> parses as an
/// <see cref="Assignment"/> (never an <see cref="ExpressionStatement"/>), and <c>expr?</c> records
/// the unwrapped inner type, so neither reaches this walker as a carrier.
///
/// <para><see cref="NullableType"/> (the loose C#-interop <c>T | None</c>) is intentionally NOT
/// flagged — it is the ordinary shape of .NET calls and warning on it would punish plain interop
/// (Axiom 1). <see cref="UnknownType"/> (error recovery) is ignored too.</para>
/// </summary>
internal sealed class MustUseValidator : ValidatingAstWalker
{
    public override string Name => "MustUseValidator";

    // After UnusedImport (430); 435 is otherwise unused.
    public override int Order => 435;

    public override void VisitExpressionStatement(ExpressionStatement node)
    {
        var message = DiscardMessage(node.Expression) ?? ElidedMethodGroupMessage(node);
        if (message != null)
        {
            AddWarning(
                message,
                node.Expression.LineStart,
                node.Expression.ColumnStart,
                code: DiagnosticCodes.Validation.MustUseValueDiscarded,
                span: node.Expression.Span);
        }

        base.VisitExpressionStatement(node);
    }

    /// <summary>
    /// Returns the SPY0480 message if <paramref name="expression"/>, discarded as a bare statement,
    /// is a must-use value — a Result/Optional carrier, a call to a <c>@must_use</c> function, or a
    /// value of a <c>@must_use</c> type — otherwise null. Carrier checks come first so the message
    /// names the carrier; the explicit '_ =' and '?' escapes never reach here as carriers.
    /// </summary>
    private string? DiscardMessage(Expression expression)
    {
        var type = Context.SemanticInfo.GetEffectiveType(expression);

        if (type is ResultType or OptionalType)
        {
            return $"result of type '{type.GetDisplayName()}' is silently discarded; bind it, " +
                "propagate with '?', or discard explicitly with '_ = ...'";
        }

        if (expression is FunctionCall call
            && Context.SemanticInfo.GetCallTarget(call) is { IsMustUse: true } target)
        {
            return $"the result of '@must_use' function '{target.Name}' is silently discarded; " +
                "bind it or discard explicitly with '_ = ...'";
        }

        if (type is UserDefinedType { Symbol.IsMustUse: true } udt)
        {
            return $"value of '@must_use' type '{udt.GetDisplayName()}' is silently discarded; " +
                "bind it or discard explicitly with '_ = ...'";
        }

        return null;
    }

    /// <summary>
    /// Returns the SPY0480 message when the statement is a bare method-group reference the
    /// TypeChecker elided to a no-op (#1617). The statement has no effect at all, so the
    /// reference is almost certainly a missing call. Reads the recorded
    /// <see cref="StatementLowering"/> fact rather than re-deriving the method-group class.
    /// </summary>
    private string? ElidedMethodGroupMessage(ExpressionStatement node)
    {
        if (Context.SemanticInfo.GetStatementLowering(node)
            is not { Kind: StatementLoweringKind.ElideMethodGroupStatement })
        {
            return null;
        }

        var name = Shared.AstHelper.UnwrapParenthesized(node.Expression) switch
        {
            MemberAccess member => member.Member,
            Identifier id => id.Name,
            _ => null,
        };

        return name != null
            ? $"method reference '{name}' as a statement has no effect; did you mean to call it? '{name}()'"
            : "method reference as a statement has no effect; did you mean to call it? 'expr()'";
    }
}
