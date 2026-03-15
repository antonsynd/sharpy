using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Lsp.Tests.Analysis;

/// <summary>
/// Walks an AST and collects all distinct node type names with occurrence counts.
/// </summary>
internal static class AstNodeTypeCollector
{
    public static Dictionary<string, int> CollectNodeTypes(Module module)
    {
        var counts = new Dictionary<string, int>();
        WalkNode(module, counts);
        return counts;
    }

    private static void WalkNode(Node node, Dictionary<string, int> counts)
    {
        var typeName = node.GetType().Name;
        if (counts.TryGetValue(typeName, out var count))
            counts[typeName] = count + 1;
        else
            counts[typeName] = 1;

        foreach (var child in node.GetChildNodes())
        {
            WalkNode(child, counts);
        }
    }
}
