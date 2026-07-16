using System.Collections.Immutable;
using Sharpy.Compiler.Lowering.Passes;
using Sharpy.Compiler.Parser.Ast;
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
    /// The default, ordered pass registry. Const folding runs first so later passes see literals;
    /// E3 Phases 7–9 append the comprehension-fusion, devirtualization, and stack-collection passes.
    /// Ordering is the pipeline order in which enabled passes compose.
    /// </summary>
    public static IrPassManager Default { get; } =
        new(ImmutableArray.Create<IIrPass>(new ConstFoldPass(), new ComprehensionFusionPass()));

    public IrPassManager(ImmutableArray<IIrPass> passes) => _passes = passes;

    /// <summary>
    /// Collects the const-folding pass's reductions from an already-rewritten <paramref name="module"/>
    /// into <paramref name="into"/>, keyed by the AST expression the emitter will generate (only
    /// <see cref="IrConstant"/>s that carry a <see cref="IrConstant.SourceAst"/> — i.e. operation folds,
    /// not lone literals). This is how the tree-rewriting passes surface their results to the
    /// AST-driven emitter (via <see cref="IrCompilation.FoldedConstants"/>).
    /// </summary>
    public static void CollectFoldedConstants(IrModule module, IDictionary<Expression, IrConstant> into)
    {
        foreach (var statement in module.Body)
            Collect(statement, into);
    }

    private static void Collect(IrNode node, IDictionary<Expression, IrConstant> into)
    {
        if (node is IrConstant { SourceAst: { } ast } constant)
            into[ast] = constant;
        foreach (var child in node.Children)
            Collect(child, into);
    }

    /// <summary>
    /// Collects the comprehension pass's optimized <see cref="IrLoweredLoop"/>s from an already-rewritten
    /// <paramref name="module"/> into <paramref name="into"/>, keyed by the comprehension AST node the
    /// emitter will generate (only loops the pass actually marked — currently
    /// <see cref="IrLoweredLoop.ProductPreallocation"/>). Surfaces the pass's results to the AST-driven
    /// emitter via <see cref="IrCompilation.OptimizedComprehensions"/>.
    /// </summary>
    public static void CollectOptimizedComprehensions(IrModule module, IDictionary<Expression, IrLoweredLoop> into)
    {
        foreach (var statement in module.Body)
            CollectLoops(statement, into);
    }

    private static void CollectLoops(IrNode node, IDictionary<Expression, IrLoweredLoop> into)
    {
        if (node is IrLoweredLoop { ProductPreallocation: true } loop)
            into[loop.Comprehension] = loop;
        foreach (var child in node.Children)
            CollectLoops(child, into);
    }

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
