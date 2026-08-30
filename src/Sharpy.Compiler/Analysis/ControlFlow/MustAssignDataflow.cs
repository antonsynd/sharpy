namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// The one in-set rule shared by the must-assign (intersection) analyses —
/// <see cref="DefiniteAssignmentAnalysis"/> and <see cref="DefiniteFieldAssignmentAnalysis"/>.
/// </summary>
/// <remarks>
/// <para>
/// A normal predecessor contributes its OUT-set. An exception predecessor contributes its
/// IN-set: the exception may be raised at any point in that block, so the only thing
/// guaranteed on the exception edge is what was definitely assigned when the block was
/// <em>entered</em>. Anything assigned before the <c>try</c> statement therefore reaches the
/// handler; anything assigned inside the try body does not (#1664). The previous rule — an
/// exception edge contributes the empty set — reported SPY0600 for
/// <c>x: int; x = 1; try: pass; except Exception: print(x)</c>, a program that cannot read an
/// unassigned <c>x</c> on any path.
/// </para>
/// <para>
/// Assignment is monotone (an assigned local stays assigned), which is what makes the entry
/// state a sound lower bound on every exception edge. <c>NarrowingFlowAnalysis</c> deliberately
/// keeps the empty-set rule: a narrowing fact established before the try can be <em>killed</em>
/// by an assignment inside it before the exception is raised, so the entry state is not a lower
/// bound there.
/// </para>
/// </remarks>
internal static class MustAssignDataflow
{
    /// <summary>
    /// Computes in[<paramref name="block"/>] from its predecessors, or returns <c>null</c> when
    /// the block has no predecessors of either kind (unreachable — the caller skips it).
    /// </summary>
    /// <param name="universe">The full name set (the lattice top for intersection).</param>
    /// <param name="inSets">Current in-sets; read for exception predecessors.</param>
    /// <param name="outSets">Current out-sets; read for normal predecessors.</param>
    public static HashSet<string>? ComputeInSet(
        BasicBlock block,
        HashSet<string> universe,
        IReadOnlyDictionary<BasicBlock, HashSet<string>> inSets,
        IReadOnlyDictionary<BasicBlock, HashSet<string>> outSets)
    {
        if (block.Predecessors.Count == 0 && block.ExceptionPredecessors.Count == 0)
            return null;

        var inSet = new HashSet<string>(universe);
        foreach (var pred in block.Predecessors)
            inSet.IntersectWith(outSets[pred]);
        foreach (var pred in block.ExceptionPredecessors)
            inSet.IntersectWith(inSets[pred]);

        // One or more block-scoped binders (for-target, with-as, except-as) end here: for those
        // names, the state is whatever the binder's block was entered with — the binder itself
        // never assigned the outer name (BasicBlock.RebindScopeEntries).
        if (block.RebindScopeEntries.Count > 0)
            ApplyScopeExits(block, inSet, inSets);

        return inSet;
    }

    /// <summary>
    /// Restores every name bound by a binder block that goes out of scope at
    /// <paramref name="block"/> to the state the binder was ENTERED with. When several binders end
    /// at the same block (sibling <c>except</c> handlers reaching one merge/finally block) a name
    /// bound by more than one of them is restored as assigned only if EVERY such binder was entered
    /// with it assigned — the must-assign lattice meets, it never joins.
    /// </summary>
    private static void ApplyScopeExits(
        BasicBlock block,
        HashSet<string> inSet,
        IReadOnlyDictionary<BasicBlock, HashSet<string>> inSets)
    {
        // Only sibling binders can disagree about a name, so the bookkeeping set is allocated
        // solely for the multi-binder case.
        HashSet<string>? unassignedByASibling =
            block.RebindScopeEntries.Count > 1 ? new HashSet<string>() : null;

        foreach (var scopeEntry in block.RebindScopeEntries)
        {
            var entryState = inSets[scopeEntry];
            foreach (var name in scopeEntry.EntryRebinds)
            {
                if (entryState.Contains(name) && unassignedByASibling?.Contains(name) != true)
                {
                    inSet.Add(name);
                }
                else
                {
                    inSet.Remove(name);
                    unassignedByASibling?.Add(name);
                }
            }
        }
    }

    /// <summary>
    /// The optimistic starting state for a must-assign fixpoint: the entry block holds nothing;
    /// every other block starts at the lattice top and only ever shrinks.
    /// </summary>
    public static Dictionary<BasicBlock, HashSet<string>> InitializeSets(
        ControlFlowGraph cfg, HashSet<string> universe)
    {
        var sets = new Dictionary<BasicBlock, HashSet<string>>(cfg.Blocks.Count);
        foreach (var block in cfg.Blocks)
        {
            sets[block] = block == cfg.Entry
                ? new HashSet<string>()
                : new HashSet<string>(universe);
        }
        return sets;
    }
}
