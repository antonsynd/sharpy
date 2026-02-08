using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Service for finding AST nodes at specific source positions.
/// </summary>
/// <remarks>
/// This service enables LSP-style position queries:
/// - Hover: Find what node is under the cursor
/// - Go-to-definition: Find identifier to resolve its symbol
/// - Completions: Find context for completion suggestions
///
/// All position parameters use 1-based line and column numbers,
/// matching editor conventions and AST node properties.
///
/// Example usage:
/// <code>
/// var service = new AstPositionService();
/// var node = service.FindInnermostNode(module, line: 5, column: 10);
/// if (node is Identifier id)
/// {
///     // Found an identifier at position
/// }
/// </code>
/// </remarks>
public sealed class AstPositionService
{
    /// <summary>
    /// Finds the innermost AST node containing the given position.
    /// </summary>
    /// <param name="module">The module to search</param>
    /// <param name="line">1-based line number</param>
    /// <param name="column">1-based column number</param>
    /// <returns>The innermost node, or null if position is outside all nodes</returns>
    public Node? FindInnermostNode(Module module, int line, int column)
    {
        if (module is null)
            throw new ArgumentNullException(nameof(module));
        if (line < 1)
            throw new ArgumentOutOfRangeException(nameof(line), "Line number must be at least 1.");
        if (column < 1)
            throw new ArgumentOutOfRangeException(nameof(column), "Column number must be at least 1.");

        var path = new List<Node>();
        FindNodesAtPosition(module, line, column, path);
        return path.Count > 0 ? path[^1] : null;
    }

    /// <summary>
    /// Finds all AST nodes containing the given position, from outermost to innermost.
    /// </summary>
    /// <param name="module">The module to search</param>
    /// <param name="line">1-based line number</param>
    /// <param name="column">1-based column number</param>
    /// <returns>A list of nodes from outermost to innermost, or empty if position is outside all nodes</returns>
    public IReadOnlyList<Node> FindAllContainingNodes(Module module, int line, int column)
    {
        if (module is null)
            throw new ArgumentNullException(nameof(module));
        if (line < 1)
            throw new ArgumentOutOfRangeException(nameof(line), "Line number must be at least 1.");
        if (column < 1)
            throw new ArgumentOutOfRangeException(nameof(column), "Column number must be at least 1.");

        var path = new List<Node>();
        FindNodesAtPosition(module, line, column, path);
        return path;
    }

    /// <summary>
    /// Finds the node of a specific type at the given position.
    /// Returns the innermost node of that type if multiple match.
    /// </summary>
    /// <typeparam name="T">The type of node to find</typeparam>
    /// <param name="module">The module to search</param>
    /// <param name="line">1-based line number</param>
    /// <param name="column">1-based column number</param>
    /// <returns>The innermost node of type T, or null if not found</returns>
    public T? FindNodeOfType<T>(Module module, int line, int column) where T : Node
    {
        var allNodes = FindAllContainingNodes(module, line, column);

        // Search from innermost to outermost
        for (int i = allNodes.Count - 1; i >= 0; i--)
        {
            if (allNodes[i] is T typed)
                return typed;
        }

        return null;
    }

    /// <summary>
    /// Recursively finds all nodes containing the position and adds them to the path.
    /// </summary>
    private void FindNodesAtPosition(Node node, int line, int column, List<Node> path)
    {
        if (!ContainsPosition(node, line, column))
            return;

        path.Add(node);

        // Check children and recurse into the one that contains the position
        foreach (var child in GetChildren(node))
        {
            if (ContainsPosition(child, line, column))
            {
                FindNodesAtPosition(child, line, column, path);
                return; // Only one child can contain the exact position
            }
        }
    }

    /// <summary>
    /// Checks if a node's span contains the given position.
    /// </summary>
    private static bool ContainsPosition(Node node, int line, int column)
    {
        // Handle nodes with no valid span
        if (node.LineStart == 0 && node.LineEnd == 0)
            return false;

        // Check if position is within the node's span
        if (line < node.LineStart || line > node.LineEnd)
            return false;

        // On the start line, column must be >= start column
        if (line == node.LineStart && column < node.ColumnStart)
            return false;

        // On the end line, column must be <= end column
        if (line == node.LineEnd && column > node.ColumnEnd)
            return false;

        return true;
    }

    /// <summary>
    /// Gets all child nodes of a given node.
    /// Delegates to each node's own <see cref="Node.GetChildNodes"/> implementation.
    /// </summary>
    private static IEnumerable<Node> GetChildren(Node node)
    {
        return node.GetChildNodes();
    }
}
