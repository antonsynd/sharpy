using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Warns on an <c>except</c> handler that an earlier handler of the same <c>try</c> makes unreachable
/// (SPY0488, #2363). The verdict is <see cref="ExceptHandlerReachability"/>'s, the same one the
/// lowering pass hands the emitter, which leaves the handler out of the compiled <c>catch</c> list.
/// </summary>
internal sealed class ExceptHandlerOrderValidator : SemanticValidatorBase
{
    public override string Name => "ExceptHandlerOrderValidator";
    public override int Order => 409;

    public override void Validate(Module module, SemanticContext context)
    {
        var collector = new TryCollector();
        collector.Visit(module);

        foreach (var tryStmt in collector.TryStatements)
        {
            var verdicts = ExceptHandlerReachability.Classify(tryStmt, context.SemanticInfo);
            for (var i = 0; i < verdicts.Length; i++)
            {
                if (!verdicts[i].IsUnreachable)
                    continue;

                var handler = tryStmt.Handlers[i];
                var caughtBy = verdicts[i].CaughtBy?.GetDisplayName() ?? "Exception";
                var what = handler.ExceptionType switch
                {
                    null => "this bare 'except:'",
                    { } annotation when ExceptHandlerReachability.ElementsOf(annotation).Length > 1
                        => "every type of this handler",
                    { } annotation => $"'{context.SemanticInfo.GetTypeTestLowering(annotation)?.TestType.GetDisplayName() ?? annotation.Name}'",
                };
                AddWarning(
                    context,
                    $"This except handler is unreachable: an earlier handler catches '{caughtBy}', which "
                    + $"covers {what}, so it never runs and is left out. Move it before that handler, or delete it",
                    handler.ExceptionType?.LineStart ?? handler.LineStart,
                    handler.ExceptionType?.ColumnStart ?? handler.ColumnStart,
                    code: DiagnosticCodes.Validation.UnreachableArm,
                    span: handler.ExceptionType?.Span ?? handler.Span);
            }
        }
    }

    private sealed class TryCollector : AstVisitor
    {
        public List<TryStatement> TryStatements { get; } = new();

        public override void VisitTryStatement(TryStatement node)
        {
            TryStatements.Add(node);
            DefaultVisit(node);
        }
    }
}
