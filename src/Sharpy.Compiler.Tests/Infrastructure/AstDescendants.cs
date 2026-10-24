using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// Walks a parsed AST through <see cref="Node.GetChildNodes"/> — the parser's own child
/// enumeration, so a new node kind is reached without editing this walker. It replaced the fifteen
/// private recursive <c>Descendants(Node)</c> copies the test project held (#2324); the copies that
/// skipped the root were only ever applied to a <see cref="Module"/> under a filter no module passes,
/// so yielding the root changes no result. Two walkers differ and stay: <c>AstRewriter.Descendants</c>
/// (mutation sites — it also reaches the type annotations this walk hides) and
/// <c>MasterRegistryImmutabilityTests.Descendants</c> (children in reverse order). Iterative, so a
/// deep generated tree cannot overflow the stack.
/// Facts held on non-node records (<see cref="Decorator"/>, <see cref="TypeParameterDef"/>,
/// <see cref="ExceptHandler"/>, <see cref="FStringPart"/>, <see cref="MatchCase"/>) are read from
/// their owning node, not visited.
/// </summary>
public static class AstDescendants
{
    /// <summary><paramref name="root"/> followed by every node below it, pre-order.</summary>
    public static IEnumerable<Node> SelfAndDescendants(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            var children = node.GetChildNodes().Where(c => c != null).ToList();
            for (int i = children.Count - 1; i >= 0; i--)
                stack.Push(children[i]);
        }
    }
}
