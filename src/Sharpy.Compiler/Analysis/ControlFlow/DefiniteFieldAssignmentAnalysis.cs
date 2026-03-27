using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// Determines which struct fields are definitely assigned in a constructor
/// using forward dataflow analysis over the control flow graph.
/// </summary>
/// <remarks>
/// The analysis uses intersection-based forward dataflow. A field is
/// "definitely assigned" only if it is assigned on ALL paths from the
/// entry block to the exit block. Exception edges are treated conservatively
/// (contribute empty set) because an exception can interrupt a block mid-way
/// through its statements.
/// </remarks>
internal static class DefiniteFieldAssignmentAnalysis
{
    /// <summary>
    /// Finds all fields that are definitely assigned (initialized via self.field = value)
    /// on ALL paths through the constructor CFG.
    /// </summary>
    public static IReadOnlySet<string> FindDefinitelyAssignedFields(ControlFlowGraph cfg)
    {
        // Step 1: Collect the universe of all field names mentioned in self.field = value
        // assignments across ALL blocks.
        var allFields = new HashSet<string>();
        var assignedInBlock = new Dictionary<BasicBlock, HashSet<string>>();

        foreach (var block in cfg.Blocks)
        {
            var blockAssigned = new HashSet<string>();
            foreach (var stmt in block.Statements)
            {
                if (IsSelfFieldAssignment(stmt, out var fieldName))
                {
                    allFields.Add(fieldName);
                    blockAssigned.Add(fieldName);
                }
            }
            assignedInBlock[block] = blockAssigned;
        }

        if (allFields.Count == 0)
        {
            return new HashSet<string>();
        }

        // Step 2: Initialize dataflow state
        // out[entry] = empty set
        // out[all other blocks] = allFields (optimistic initialization)
        var outSets = new Dictionary<BasicBlock, HashSet<string>>();
        foreach (var block in cfg.Blocks)
        {
            outSets[block] = block == cfg.Entry
                ? new HashSet<string>()
                : new HashSet<string>(allFields);
        }

        // Step 3: Fixed-point iteration in reverse post-order
        var rpo = cfg.GetReversePostOrder();
        bool changed = true;

        while (changed)
        {
            changed = false;

            foreach (var block in rpo)
            {
                if (block == cfg.Entry)
                    continue;

                // Compute in[B] from predecessors
                HashSet<string> inSet;

                bool hasNormalPreds = block.Predecessors.Count > 0;
                bool hasExceptionPreds = block.ExceptionPredecessors.Count > 0;

                if (hasExceptionPreds)
                {
                    // Any exception predecessor contributes empty set.
                    // Since we intersect all predecessor out-sets, the result is empty.
                    inSet = new HashSet<string>();
                }
                else if (hasNormalPreds)
                {
                    // Intersect out-sets of all normal predecessors
                    inSet = new HashSet<string>(allFields);
                    foreach (var pred in block.Predecessors)
                    {
                        inSet.IntersectWith(outSets[pred]);
                    }
                }
                else
                {
                    // No predecessors at all (unreachable block) — skip
                    continue;
                }

                // out[B] = in[B] ∪ assignedInBlock[B]
                var newOut = new HashSet<string>(inSet);
                newOut.UnionWith(assignedInBlock[block]);

                if (!newOut.SetEquals(outSets[block]))
                {
                    outSets[block] = newOut;
                    changed = true;
                }
            }
        }

        // Step 4: Result — find the exit block's normal predecessors and
        // intersect their out-sets.
        var exitNormalPreds = cfg.Exit.Predecessors;
        if (exitNormalPreds.Count == 0)
        {
            // No paths reach exit (all paths throw/raise)
            return new HashSet<string>();
        }

        var result = new HashSet<string>(allFields);
        foreach (var pred in exitNormalPreds)
        {
            result.IntersectWith(outSets[pred]);
        }

        return result;
    }

    /// <summary>
    /// Checks if a statement is a simple assignment of the form self.field = value.
    /// Augmented assignments (self.field += value) do NOT count as initialization.
    /// </summary>
    private static bool IsSelfFieldAssignment(Statement stmt, out string fieldName)
    {
        if (stmt is Assignment { Operator: AssignmentOperator.Assign } assignment &&
            assignment.Target is MemberAccess memberAccess &&
            memberAccess.Object is Identifier { Name: PythonNames.Self })
        {
            fieldName = memberAccess.Member;
            return true;
        }

        fieldName = "";
        return false;
    }
}
