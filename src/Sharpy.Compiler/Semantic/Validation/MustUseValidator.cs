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
        var type = Context.SemanticInfo.GetEffectiveType(node.Expression);
        if (type is ResultType or OptionalType)
        {
            AddWarning(
                $"result of type '{type.GetDisplayName()}' is silently discarded; bind it, " +
                "propagate with '?', or discard explicitly with '_ = ...'",
                node.Expression.LineStart,
                node.Expression.ColumnStart,
                code: DiagnosticCodes.Validation.MustUseValueDiscarded,
                span: node.Expression.Span);
        }

        base.VisitExpressionStatement(node);
    }
}
