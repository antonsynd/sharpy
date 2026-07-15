using System.Text;
using Sharpy.Compiler.Lowering;

namespace Sharpy.Compiler.Tests.Lowering;

/// <summary>
/// Produces a deterministic, human-readable structural dump of an <see cref="IrCompilation"/> for
/// the totality and determinism guards. The dump walks the IR tree (modules → statements →
/// <see cref="IrNode.Children"/>) and never enumerates the reference-keyed index, so its output is a
/// pure function of tree shape, node kinds, and spans — it cannot leak construction order.
/// </summary>
internal static class IrStructuralDump
{
    public static string Dump(IrCompilation? ir)
    {
        if (ir is null)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var module in ir.Modules)
        {
            // Module order is fixed by the lowering pass (source-path ordered); use the file name
            // so the dump is stable across compiles of the same on-disk files.
            sb.Append("module ").Append(Path.GetFileName(module.FilePath)).Append('\n');
            foreach (var statement in module.Body)
                DumpNode(statement, 1, sb);
        }

        return sb.ToString();
    }

    private static void DumpNode(IrNode node, int depth, StringBuilder sb)
    {
        sb.Append(' ', depth * 2);
        sb.Append(node.GetType().Name);
        sb.Append(' ').Append(node.Span.ToString());

        // Distinguish opaque wrappers by the AST type they carry, so a shape change (e.g. a
        // migration replacing an opaque node with a typed one) is visible in the dump.
        switch (node)
        {
            case IrOpaqueExpression opaqueExpression:
                sb.Append(" ast=").Append(opaqueExpression.Ast.GetType().Name);
                break;
            case IrOpaqueStatement opaqueStatement:
                sb.Append(" ast=").Append(opaqueStatement.Ast.GetType().Name);
                break;
        }

        sb.Append('\n');
        foreach (var child in node.Children)
            DumpNode(child, depth + 1, sb);
    }
}
