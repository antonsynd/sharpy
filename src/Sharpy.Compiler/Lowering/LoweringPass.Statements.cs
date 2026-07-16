using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Lowering;

/// <summary>
/// LoweringPass partial: structural statement transforms (E2 #1056). Currently the <c>defer</c>
/// lowering, which turns a <see cref="DeferStatement"/> into an <see cref="IrScopeGuard"/> instead of
/// an opaque wrapper. The suite-split and LIFO nesting stay in the emitter (statement emission is
/// stateful and suite-scoped); this pass makes the scope-exit lowering explicit in the IR.
/// </summary>
internal sealed partial class LoweringPass
{
    /// <summary>
    /// Lowers a <c>defer</c> to an <see cref="IrScopeGuard"/> carrying its lowered deferred body (the
    /// <c>finally</c> statements). The body statements are lowered exactly once — the same statements
    /// the opaque wrapper used to carry — so the tree stays total.
    /// </summary>
    private static IrScopeGuard LowerDefer(DeferStatement defer, SemanticInfo semanticInfo, LoweringState state)
    {
        var deferredBody = ImmutableArray.CreateBuilder<IrStatement>(defer.Body.Length);
        foreach (var statement in defer.Body)
            deferredBody.Add(LowerStatement(statement, semanticInfo, state));

        return new IrScopeGuard(deferredBody.ToImmutable(), SpanOf(defer));
    }
}
