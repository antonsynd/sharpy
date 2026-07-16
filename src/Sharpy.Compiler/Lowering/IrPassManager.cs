using System.Collections.Immutable;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Lowering;

/// <summary>
/// Runs the registered IR optimization passes (E3, #1057) in a fixed order over a single
/// <see cref="IrModule"/>, skipping any pass whose <c>FeatureScope.CodeGen</c> flag is not enabled for
/// that module. Passes are pure IR→IR rewrites (<see cref="IIrPass"/>); the manager threads the module
/// through the enabled ones in registration order. With no passes registered — or with every flag off,
/// the default path — it returns the module unchanged.
/// </summary>
/// <remarks>
/// The manager runs inside the <c>Lowering</c> phase bracket, once per module, over that module's
/// effective feature set (compilation-wide flags unioned with the file's <c>from __future__ import</c>
/// features). Because the passes are keyed per-flag and each file carries its own flags, optimization
/// is opt-in per file, matching the CodeGen-scoped behavioral-flag contract.
/// </remarks>
internal sealed class IrPassManager
{
    private readonly ImmutableArray<IIrPass> _passes;

    /// <summary>
    /// The default, ordered pass registry. Empty until E3 Phases 6–9 register the const-folding,
    /// comprehension-fusion, devirtualization, and stack-collection passes; ordering is the pipeline
    /// order in which enabled passes compose.
    /// </summary>
    public static IrPassManager Default { get; } = new(ImmutableArray<IIrPass>.Empty);

    public IrPassManager(ImmutableArray<IIrPass> passes) => _passes = passes;

    /// <summary>
    /// Applies each enabled pass to <paramref name="module"/> in registration order and returns the
    /// result. A pass runs iff <paramref name="features"/> enables its <see cref="IIrPass.FlagName"/>.
    /// Returns the same instance when no enabled pass changed the module.
    /// </summary>
    public IrModule Run(IrModule module, FeatureFlags features, IrRewriteContext context)
    {
        var current = module;
        foreach (var pass in _passes)
        {
            if (features.IsEnabled(pass.FlagName))
                current = pass.Rewrite(current, context);
        }

        return current;
    }
}
