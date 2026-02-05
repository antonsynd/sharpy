using System.Diagnostics;

namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Provides recursive validation of AST structural invariants.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="ValidateTree"/> to recursively validate all nodes in an AST.
/// This catches malformed AST nodes early, before they cause cryptic failures
/// deep in semantic analysis or code generation.
/// </para>
/// <para>
/// Validation is DEBUG-only to avoid overhead in Release builds. The <c>#if DEBUG</c>
/// directive ensures the entire validation traversal is compiled out in Release mode.
/// </para>
/// <para>
/// Each node type can override <see cref="Node.ValidateInvariants"/> to add
/// specific checks (e.g., non-null required properties, non-empty names).
/// Invariant failures use <see cref="Debug.Assert(bool, string)"/> which
/// throws in DEBUG builds but is removed in Release.
/// </para>
/// </remarks>
public static class AstValidator
{
    /// <summary>
    /// Recursively validates structural invariants of all nodes in the AST.
    /// </summary>
    /// <param name="root">The root node to validate (typically a <see cref="Module"/>).</param>
    /// <remarks>
    /// <para>
    /// This method traverses the AST depth-first, calling <see cref="Node.ValidateInvariants"/>
    /// on each node. Child nodes are enumerated via <see cref="Node.GetChildNodes"/>.
    /// </para>
    /// <para>
    /// In DEBUG builds, invariant violations trigger <see cref="Debug.Assert(bool, string)"/>
    /// failures with descriptive messages. In Release builds, this method compiles to a no-op.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Call after parsing in DEBUG builds
    /// #if DEBUG
    /// AstValidator.ValidateTree(module);
    /// #endif
    /// </code>
    /// </example>
    [Conditional("DEBUG")]
    public static void ValidateTree(Node root)
    {
        if (root == null)
        {
            Debug.Assert(false, "AstValidator.ValidateTree: root node is null");
            return;
        }

        ValidateNodeRecursive(root);
    }

    /// <summary>
    /// Validates a single node and all its descendants.
    /// </summary>
    private static void ValidateNodeRecursive(Node node)
    {
        // Validate this node's invariants
        node.ValidateInvariants();

        // Recursively validate all child nodes
        foreach (var child in node.GetChildNodes())
        {
            if (child != null)
            {
                ValidateNodeRecursive(child);
            }
            else
            {
                // A null child in GetChildNodes() is suspicious - the implementation
                // should filter out nulls or not yield nullable properties
                Debug.Assert(false,
                    $"AstValidator: node {node.GetType().Name} returned null child from GetChildNodes()");
            }
        }
    }

    /// <summary>
    /// Validates a single node without recursing into children.
    /// Useful for testing individual node invariants.
    /// </summary>
    /// <param name="node">The node to validate.</param>
    [Conditional("DEBUG")]
    public static void ValidateNode(Node node)
    {
        if (node == null)
        {
            Debug.Assert(false, "AstValidator.ValidateNode: node is null");
            return;
        }

        node.ValidateInvariants();
    }

    /// <summary>
    /// Gets the count of all nodes in the AST (for metrics/debugging).
    /// </summary>
    /// <param name="root">The root node.</param>
    /// <returns>The total number of nodes in the tree.</returns>
    public static int CountNodes(Node root)
    {
        if (root == null)
            return 0;

        int count = 1;
        foreach (var child in root.GetChildNodes())
        {
            if (child != null)
            {
                count += CountNodes(child);
            }
        }
        return count;
    }
}
