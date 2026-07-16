using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Lowering;

/// <summary>
/// A single toggleable IR optimization pass (E3, #1057): a pure IR→IR immutable rewrite keyed to a
/// <c>FeatureScope.CodeGen</c> feature flag. Passes never mutate their input — a rewrite produces a new
/// <see cref="IrModule"/> — which keeps the pass pipeline order-independent to reason about and
/// preserves the immutable-tree discipline the IR is built on. The <see cref="IrPassManager"/> runs a
/// pass only when its <see cref="FlagName"/> is enabled for the module being lowered.
/// </summary>
/// <remarks>
/// These are <b>behavioral</b> flags (docs/design/feature-lifecycle.md): a disabled flag means the pass
/// does not run, and the un-optimized program is valid on its own — so there is no gated construct and
/// no SPY0331 rejection, only the presence or absence of the rewrite.
/// </remarks>
internal interface IIrPass
{
    /// <summary>
    /// The CodeGen-scoped feature-flag name that enables this pass (e.g. <c>opt_const_fold</c>),
    /// registered in <see cref="Sharpy.Compiler.Shared.FeatureFlags.KnownFeatures"/>.
    /// </summary>
    string FlagName { get; }

    /// <summary>
    /// Returns a rewritten copy of <paramref name="module"/>. Must be pure: it may read
    /// <paramref name="context"/> but must not mutate the input module or the context, and must return
    /// the same instance when it makes no change (so the manager can detect a no-op cheaply).
    /// </summary>
    IrModule Rewrite(IrModule module, IrRewriteContext context);
}

/// <summary>
/// Read-only context handed to each <see cref="IIrPass"/>: the merged, project-level semantic facts a
/// pass may consult while rewriting (types are already carried on the IR nodes; these cover the rest).
/// A pass reads from it and never mutates it.
/// </summary>
/// <param name="SymbolTable">The global symbol table.</param>
/// <param name="SemanticInfo">The merged, project-level semantic facts.</param>
internal sealed record IrRewriteContext(SymbolTable SymbolTable, SemanticInfo SemanticInfo);
