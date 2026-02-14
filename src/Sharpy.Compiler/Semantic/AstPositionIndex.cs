using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Indexes AST nodes by their source position for efficient position-to-node lookup.
/// Built eagerly during construction by walking the AST and collecting all nodes
/// with non-empty <see cref="TextSpan"/>. Supports binary search for O(log n) lookups.
/// </summary>
public sealed class AstPositionIndex
{
    private readonly List<Node> _nodes;

    /// <summary>
    /// Creates a new position index by walking the given AST node and all its descendants.
    /// </summary>
    /// <param name="root">The root AST node to index. Typically a <see cref="Module"/>.</param>
    public AstPositionIndex(Node root)
    {
        _nodes = new List<Node>();
        CollectNodes(root);
        // Sort by Start ascending, then Length descending (parents before children at same start)
        _nodes.Sort((a, b) =>
        {
            int cmp = a.Span!.Value.Start.CompareTo(b.Span!.Value.Start);
            if (cmp != 0)
                return cmp;
            return b.Span!.Value.Length.CompareTo(a.Span!.Value.Length);
        });
    }

    /// <summary>
    /// Returns the total number of indexed nodes.
    /// </summary>
    public int Count => _nodes.Count;

    /// <summary>
    /// Finds the deepest (most specific) AST node whose span contains the given position.
    /// </summary>
    /// <param name="position">The zero-based character offset in the source text.</param>
    /// <returns>The innermost node containing the position, or null if no node contains it.</returns>
    public Node? FindNodeAtPosition(int position)
    {
        Node? best = null;
        int bestLength = int.MaxValue;

        // Binary search to find the first node whose Start <= position
        int lo = 0, hi = _nodes.Count - 1;
        int insertionPoint = _nodes.Count;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (_nodes[mid].Span!.Value.Start <= position)
            {
                lo = mid + 1;
            }
            else
            {
                insertionPoint = mid;
                hi = mid - 1;
            }
        }

        // Check all nodes before the insertion point that could contain the position.
        // Walk backwards from insertionPoint - 1: nodes are sorted by Start ascending,
        // so we only need to check nodes whose Start <= position.
        for (int i = insertionPoint - 1; i >= 0; i--)
        {
            var span = _nodes[i].Span!.Value;
            if (span.Start > position)
                continue;
            // Since sorted by Start ascending, once Start + largest possible Length
            // can't reach position, we can stop. But spans can vary, so we check Contains.
            if (span.Contains(position) && span.Length < bestLength)
            {
                best = _nodes[i];
                bestLength = span.Length;
            }
        }

        return best;
    }

    /// <summary>
    /// Finds all AST nodes whose span contains the given position, ordered from
    /// outermost (largest span) to innermost (smallest span).
    /// </summary>
    /// <param name="position">The zero-based character offset in the source text.</param>
    /// <returns>A list of nodes from outermost to innermost, or empty if none contain the position.</returns>
    public IReadOnlyList<Node> FindNodesAtPosition(int position)
    {
        var result = new List<Node>();

        for (int i = 0; i < _nodes.Count; i++)
        {
            var span = _nodes[i].Span!.Value;
            if (span.Contains(position))
            {
                result.Add(_nodes[i]);
            }
        }

        // Sort by span length descending (outermost first)
        result.Sort((a, b) => b.Span!.Value.Length.CompareTo(a.Span!.Value.Length));
        return result;
    }

    private void CollectNodes(Node node)
    {
        if (node.Span is { IsEmpty: false })
        {
            _nodes.Add(node);
        }

        foreach (var child in node.GetChildNodes())
        {
            CollectNodes(child);
        }
    }
}
