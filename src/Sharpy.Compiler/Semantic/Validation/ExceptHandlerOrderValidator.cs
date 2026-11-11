using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Reports an <c>except</c> handler that an earlier handler of the same <c>try</c> makes unreachable
/// (#2363). The verdict is <see cref="ExceptHandlerReachability"/>'s, the same one the lowering pass
/// hands the emitter, which leaves the handler out of the compiled <c>catch</c> list.
/// <list type="bullet">
/// <item>Dead in CPython too: a SPY0488 warning; the program runs first-match-wins as CPython does.</item>
/// <item>Dead only because Sharpy has no <c>BaseException</c>, for a handler that NAMES a type CPython
/// roots there (<c>except Exception:</c> then <c>except SystemExit:</c>): refused, SPY0709. The handler
/// was written to run and Sharpy cannot run it where it stands; moving it first keeps CPython's
/// behaviour.</item>
/// <item>A bare <c>except:</c> after <c>except Exception:</c> that CPython would still run for
/// <c>SystemExit</c>: a SPY0488 warning that says so.</item>
/// </list>
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
                var verdict = verdicts[i];
                if (!verdict.IsUnreachable)
                    continue;

                var handler = tryStmt.Handlers[i];
                var caughtBy = verdict.CaughtBy?.GetDisplayName() ?? "Exception";
                var line = handler.ExceptionType?.LineStart ?? handler.LineStart;
                var column = handler.ExceptionType?.ColumnStart ?? handler.ColumnStart;
                var span = handler.ExceptionType?.Span ?? handler.Span;

                if (!verdict.PythonLiveTypes.IsEmpty)
                {
                    var live = string.Join(", ", verdict.PythonLiveTypes.Select(t => $"'{t.GetDisplayName()}'"));
                    AddError(
                        context,
                        $"This except handler can never run in Sharpy: the earlier handler for '{caughtBy}' catches "
                        + $"{live} too, because Sharpy has no BaseException; CPython would run this handler for "
                        + $"{live}. Move this handler before the '{caughtBy}' handler",
                        line, column,
                        code: DiagnosticCodes.ValidationOverflow.BaseExceptionHandlerAfterException,
                        span: span);
                    continue;
                }

                var what = DescribeHandler(handler.ExceptionType, verdict);
                var message = verdict.BareStillRunsInPython
                    ? $"This bare 'except:' never runs in Sharpy: the earlier handler for '{caughtBy}' catches every "
                      + "exception because Sharpy has no BaseException (CPython would still run it for SystemExit). "
                      + "It is left out. Put an 'except SystemExit:' handler before that handler to handle an exit"
                    : $"This except handler is unreachable: an earlier handler catches '{caughtBy}', which "
                      + $"covers {what}, so it never runs and is left out. Move it before that handler, or delete it";
                AddWarning(context, message, line, column,
                    code: DiagnosticCodes.Validation.UnreachableArm, span: span);
            }
        }
    }

    /// <summary>
    /// How the warning names the covered handler: a bare handler, a tuple of two or more types, or its
    /// one type (a one-element tuple names its element, not the tuple type).
    /// </summary>
    private static string DescribeHandler(TypeAnnotation? annotation, ExceptHandlerVerdict verdict) => annotation switch
    {
        null => "this bare 'except:'",
        { } when verdict.ElementTypes.Length > 1 => "every type of this handler",
        { } => $"'{verdict.ElementTypes.FirstOrDefault()?.GetDisplayName() ?? ExceptHandlerReachability.ElementsOf(annotation)[0].Name}'",
    };

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
