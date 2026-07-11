using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// Per-compilation cache of <see cref="ControlFlowGraph"/>s keyed by AST node identity.
/// Lets the CFG-consuming validators (<c>ControlFlowValidator</c>, <c>StructRulesValidator</c>,
/// <c>PropertyValidator</c>) — and, later, the narrowing dataflow engine on the TypeChecker
/// side — share one graph per body instead of each rebuilding it.
/// </summary>
/// <remarks>
/// <para>
/// Keys use reference (identity) equality: AST nodes are records with value equality, but two
/// structurally-equal bodies at different source locations must not collide, so we key on node
/// identity (the same convention as <c>SemanticInfo</c>).
/// </para>
/// <para>
/// The cache serves <em>pure</em> graphs — built with no exhaustive-match knowledge. Exhaustive-match
/// pruning is an injected concern of <c>ControlFlowValidator</c> alone: the pruned variant is stored
/// in a separate table (<see cref="GetOrBuildPruned"/>) so it never becomes the graph handed to the
/// other validators. Unconditional (wildcard/binding) match exhaustiveness is handled by the builder
/// itself and is therefore already reflected in the pure graph; only <em>semantic</em> exhaustiveness
/// (supplied via the exhaustive-match set) produces a distinct pruned graph.
/// </para>
/// <para>
/// A <see cref="ControlFlowGraph"/> is effectively immutable and every analysis over it is read-only,
/// so a single cached instance is safe to share across validators. The cache is not thread-safe; it is
/// scoped to a single compilation and used from the single-threaded validation pipeline.
/// </para>
/// </remarks>
internal sealed class ControlFlowGraphCache
{
    private readonly Dictionary<FunctionDef, ControlFlowGraph> _functionGraphs =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IReadOnlyList<Statement>, ControlFlowGraph> _statementGraphs =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<FunctionDef, ControlFlowGraph> _prunedFunctionGraphs =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Returns the pure CFG for a function body, building and caching it on first request.
    /// </summary>
    public ControlFlowGraph GetOrBuild(FunctionDef function)
    {
        if (_functionGraphs.TryGetValue(function, out var cached))
            return cached;

        var cfg = new ControlFlowGraphBuilder().Build(function);
        _functionGraphs[function] = cfg;
        return cfg;
    }

    /// <summary>
    /// Returns the pure CFG for a raw statement list (e.g. a module body), building and caching it
    /// on first request. Cached by the list's reference identity.
    /// </summary>
    public ControlFlowGraph GetOrBuild(IReadOnlyList<Statement> statements)
    {
        if (_statementGraphs.TryGetValue(statements, out var cached))
            return cached;

        var cfg = new ControlFlowGraphBuilder().Build(statements);
        _statementGraphs[statements] = cfg;
        return cfg;
    }

    /// <summary>
    /// Returns the CFG for a function with exhaustive-match pruning applied. Only
    /// <c>ControlFlowValidator</c> needs this variant.
    /// </summary>
    /// <remarks>
    /// When pruning cannot change the graph for this function — no exhaustive-match set was supplied,
    /// or none of the function's own match statements are in it — the shared <em>pure</em> graph is
    /// returned instead, so the build is amortized with the other validators. Only functions that
    /// actually contain a semantically-exhaustive match get a distinct, separately-cached pruned graph.
    /// </remarks>
    public ControlFlowGraph GetOrBuildPruned(FunctionDef function, HashSet<MatchStatement>? exhaustiveMatches)
    {
        // No semantic exhaustiveness in play for this function → the pruned graph is identical to the
        // pure one, so hand back the shared pure graph and keep the caches unified.
        if (exhaustiveMatches == null
            || exhaustiveMatches.Count == 0
            || !ContainsExhaustiveMatch(function, exhaustiveMatches))
        {
            return GetOrBuild(function);
        }

        if (_prunedFunctionGraphs.TryGetValue(function, out var cached))
            return cached;

        var cfg = new ControlFlowGraphBuilder(exhaustiveMatches).Build(function);
        _prunedFunctionGraphs[function] = cfg;
        return cfg;
    }

    /// <summary>
    /// Returns true if any match statement reachable from <paramref name="node"/> is in the
    /// exhaustive-match set. Deliberately over-approximates by walking the whole subtree (including
    /// nested definitions the CFG builder itself skips): a false positive only costs a redundant,
    /// structurally-identical pruned entry, whereas a false negative would hand a pure graph to a
    /// consumer that needs the pruned one — so over-reporting is the safe direction.
    /// </summary>
    private static bool ContainsExhaustiveMatch(Node node, HashSet<MatchStatement> exhaustiveMatches)
    {
        if (node is MatchStatement match && exhaustiveMatches.Contains(match))
            return true;

        foreach (var child in node.GetChildNodes())
        {
            if (ContainsExhaustiveMatch(child, exhaustiveMatches))
                return true;
        }

        return false;
    }
}
