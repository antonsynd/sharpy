using System.Text;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp;

/// <summary>
/// Formats symbols and types as markdown hover text.
/// </summary>
internal static class SymbolFormatter
{
    public static string FormatSymbol(Symbol symbol)
    {
        return symbol switch
        {
            VariableSymbol v => FormatVariable(v),
            FunctionSymbol f => FormatFunction(f),
            TypeSymbol t => FormatType(t),
            ModuleSymbol m => $"(module) {m.Name}",
            TypeAliasSymbol a => $"(type alias) {a.Name}",
            _ => symbol.Name
        };
    }

    public static string FormatTypeInfo(SemanticType type)
    {
        return type.GetDisplayName();
    }

    private static string FormatVariable(VariableSymbol v)
    {
        var typeStr = v.Type?.GetDisplayName() ?? "unknown";
        var prefix = v.IsConstant ? "(constant)" : "(variable)";
        return $"{prefix} {v.Name}: {typeStr}";
    }

    private static string FormatFunction(FunctionSymbol f)
    {
        var sb = new StringBuilder();
        sb.Append("(function) def ");
        sb.Append(f.Name);
        sb.Append('(');

        for (var i = 0; i < f.Parameters.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            var p = f.Parameters[i];
            sb.Append(p.Name);
            if (p.Type != null)
            {
                sb.Append(": ");
                sb.Append(p.Type.GetDisplayName());
            }
        }

        sb.Append(')');

        if (f.ReturnType != null && f.ReturnType is not VoidType)
        {
            sb.Append(" -> ");
            sb.Append(f.ReturnType.GetDisplayName());
        }

        return sb.ToString();
    }

    private static string FormatType(TypeSymbol t)
    {
        var keyword = t.TypeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Union => "union",
            TypeKind.Delegate => "delegate",
            _ => "type"
        };
        return $"({keyword}) {keyword} {t.Name}";
    }
}
