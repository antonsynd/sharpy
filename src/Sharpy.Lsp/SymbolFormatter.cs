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

    /// <summary>
    /// Formats a symbol as a markdown code block with optional documentation text below.
    /// </summary>
    public static string FormatSymbolWithDocs(Symbol symbol)
    {
        var signature = FormatSymbol(symbol);
        var sb = new StringBuilder();
        sb.Append("```sharpy\n");
        sb.Append(signature);
        sb.Append("\n```");

        if (!string.IsNullOrEmpty(symbol.Documentation))
        {
            sb.Append("\n\n");
            sb.Append(symbol.Documentation);
        }

        return sb.ToString();
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

    public static string FormatPropertyWithDocs(PropertySymbol prop)
    {
        var typeStr = prop.Type?.GetDisplayName() ?? "unknown";
        var sb = new StringBuilder();
        sb.Append("```sharpy\n");
        sb.Append("(property) ");
        sb.Append(prop.Name);
        sb.Append(": ");
        sb.Append(typeStr);
        sb.Append("\n```");
        if (!string.IsNullOrEmpty(prop.Documentation))
        {
            sb.Append("\n\n");
            sb.Append(prop.Documentation);
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
        return $"({keyword}) {t.Name}";
    }
}
