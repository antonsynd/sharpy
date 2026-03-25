using System.Text;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

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
    // TODO(#441): Source docstrings flow to Symbol.Documentation via NameResolver, but stdlib
    // module symbols (registered by BuiltinRegistry/ModuleLoader) may lack Documentation.
    // DunderDocumentation and ModuleDocumentation serve as LSP-side fallbacks until
    // all symbol sources consistently populate Documentation.
    public static string FormatSymbolWithDocs(Symbol symbol)
    {
        var signature = FormatSymbol(symbol);
        var sb = new StringBuilder();
        sb.Append("```sharpy\n");
        sb.Append(signature);
        sb.Append("\n```");

        var documentation = symbol.Documentation;
        // Fall back to dunder documentation if no explicit docs
        if (string.IsNullOrEmpty(documentation) && symbol is FunctionSymbol fs
            && fs.Name.StartsWith("__") && fs.Name.EndsWith("__"))
        {
            documentation = DunderDocumentation.GetDocumentation(fs.Name);
        }
        // Fall back to module summaries if no explicit docs
        if (string.IsNullOrEmpty(documentation) && symbol is ModuleSymbol ms)
        {
            documentation = ModuleDocumentation.GetSummary(ms.Name);
        }

        if (!string.IsNullOrEmpty(documentation))
        {
            sb.Append("\n\n");
            sb.Append(documentation);
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
        string prefix;
        if (v.Name == PythonNames.Self)
            prefix = "(self)";
        else if (v.IsConstant)
            prefix = "(constant)";
        else
            prefix = "(variable)";
        return $"{prefix} {v.Name}: {typeStr}";
    }

    /// <summary>
    /// Formats a parameter as hover text (for parameters at declaration sites).
    /// </summary>
    public static string FormatParameter(string name, SemanticType? type, string? className = null)
    {
        if (name == "self" && className != null)
            return $"(self) self: {className}";
        if (name == "self")
            return "(self) self";
        if (name == "cls" && className != null)
            return $"(cls) cls: type[{className}]";

        var typeStr = type?.GetDisplayName() ?? "unknown";
        return $"(parameter) {name}: {typeStr}";
    }

    /// <summary>
    /// Formats a parameter with markdown code block wrapper.
    /// </summary>
    public static string FormatParameterWithDocs(string name, SemanticType? type, string? className = null)
    {
        var sig = FormatParameter(name, type, className);
        return $"```sharpy\n{sig}\n```";
    }

    private static string FormatFunction(FunctionSymbol f)
    {
        var sb = new StringBuilder();
        var isMethod = f.DeclaringFilePath != null && f.Parameters.Count > 0
            && f.Parameters[0].Name == "self";
        sb.Append(isMethod ? "(method) " : "(function) ");
        if (f.IsAsync)
            sb.Append("async ");
        sb.Append("def ");
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
