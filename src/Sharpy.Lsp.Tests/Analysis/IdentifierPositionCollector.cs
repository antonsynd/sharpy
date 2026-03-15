using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Lsp.Tests.Analysis;

/// <summary>
/// Walks an AST and collects all Identifier and MemberAccess nodes with their positions.
/// Used by fuzz tests to enumerate positions for hover/completion probing.
/// </summary>
internal static class IdentifierPositionCollector
{
    public record PositionInfo(int Line, int Column, string Name, string NodeType);

    public static List<PositionInfo> CollectPositions(Module module)
    {
        var positions = new List<PositionInfo>();
        WalkNode(module, positions);
        return positions;
    }

    private static void WalkNode(Node node, List<PositionInfo> positions)
    {
        switch (node)
        {
            case Identifier id when id.LineStart > 0:
                positions.Add(new PositionInfo(id.LineStart, id.ColumnStart, id.Name, "Identifier"));
                break;
            case MemberAccess ma when ma.LineStart > 0:
                positions.Add(new PositionInfo(ma.LineStart, ma.ColumnStart, ma.Member, "MemberAccess"));
                break;
        }

        foreach (var child in node.GetChildNodes())
        {
            WalkNode(child, positions);
        }
    }
}
