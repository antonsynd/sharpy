using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Lowering;

/// <summary>
/// LoweringPass partial: comprehension lowering (E2 #1056). Turns a list/set/dict comprehension into
/// an <see cref="IrLoweredLoop"/> that carries the imperative-loop <b>decisions</b> the emitter used
/// to compute inline — the single-<c>for</c> detection and the D4 sized-source capacity decision.
/// The stateful C# emission stays in <c>RoslynEmitter.Expressions.Comprehensions.cs</c>; the decision
/// helpers here are the single source of truth, called both by this pass and (for null-IR codegen
/// paths such as the REPL and the source-generator sub-pipeline) by the emitter's fallback.
/// </summary>
internal sealed partial class LoweringPass
{
    /// <summary>
    /// Lowers a list/set/dict comprehension to an <see cref="IrLoweredLoop"/>. The element/key/value
    /// and clause sub-expressions are lowered exactly once via <see cref="LowerChildren"/> (preserving
    /// totality); the single-<c>for</c> and sized-source decisions are computed here and folded onto
    /// the node. When D4 preallocation applies, <see cref="IrLoweredLoop.Capacity"/> references the
    /// <em>same</em> lowered source-iterator node already present in the children, so the tree carries
    /// no duplicate node and its structural dump stays construction-order-independent.
    /// </summary>
    private static IrLoweredLoop LowerComprehension(
        Expression comprehension,
        string collectionKind,
        ImmutableArray<ComprehensionClause> clauses,
        bool elementIsSpread,
        SemanticInfo semanticInfo,
        LoweringState state)
    {
        var children = LowerChildren(comprehension, semanticInfo, state);

        var soleForClause = SingleForClause(clauses);
        IrExpression? capacity = null;
        if (soleForClause is not null
            && IsSizedComprehensionSource(semanticInfo.GetExpressionType(soleForClause.Iterator))
            && state.Index.TryGetValue(soleForClause.Iterator, out var loweredIterator))
        {
            // Reference the already-lowered source iterator (also in `children`) rather than
            // re-lowering it: its Type is the source's semantic type, which the emitter reads to
            // choose the hoisted-source declaration type. E3's fusion pass reads it to size from.
            capacity = loweredIterator as IrExpression;
        }

        return new IrLoweredLoop(
            collectionKind,
            clauses,
            soleForClause,
            capacity,
            elementIsSpread,
            comprehension,
            semanticInfo.GetExpressionType(comprehension),
            SpanOf(comprehension),
            children);
    }

    /// <summary>
    /// Returns the single <see cref="ForClause"/> in <paramref name="clauses"/>, or <c>null</c> when
    /// there is not exactly one. Capacity preallocation only applies to single-<c>for</c>
    /// comprehensions: with nested for-clauses the result size is the product of the sources, which no
    /// single source <c>Count</c> bounds. Single source of truth for both the lowering pass and the
    /// emitter's null-IR fallback.
    /// </summary>
    internal static ForClause? SingleForClause(ImmutableArray<ComprehensionClause> clauses)
    {
        ForClause? found = null;
        foreach (var clause in clauses)
        {
            if (clause is ForClause forClause)
            {
                if (found is not null)
                    return null;
                found = forClause;
            }
        }

        return found;
    }

    /// <summary>
    /// True when a comprehension source's already-materialized <see cref="SemanticType"/> maps to a
    /// C# collection that implements <c>Sharpy.ISized</c>, so its element count can be read cheaply
    /// (via an <c>ISized</c> cast) for capacity preallocation: the Sharpy collections
    /// (<c>list</c>/<c>set</c>/<c>dict</c>/<c>frozendict</c>) and <c>range(...)</c> (whose
    /// <c>RangeIterator</c> is <c>ISized</c>). The dict views are excluded as an optimization not yet
    /// taken, NOT because the cast would fail: they implement <c>ISized</c> as of #1497, so adding
    /// them here is a behavior-preserving change whenever someone wants the preallocation (it is
    /// deliberately not bundled into #1497, which is about <c>len()</c> recognition). Pure: reads
    /// only the type recorded during semantic analysis. Single source
    /// of truth for both the lowering pass and the emitter's null-IR fallback.
    /// </summary>
    internal static bool IsSizedComprehensionSource(SemanticType? sourceType) => sourceType switch
    {
        GenericType g => g.Name is BuiltinNames.List
            or BuiltinNames.Set
            or BuiltinNames.Dict
            or BuiltinNames.FrozenDict
            or BuiltinNames.FrozenSet,
        BuiltinType b => b.Name == "RangeIterator",
        _ => false,
    };
}
