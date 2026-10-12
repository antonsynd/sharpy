using System.Globalization;
using System.Text;
using Sharpy.Compiler.Semantic;
using AstNode = Sharpy.Compiler.Parser.Ast.Node;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Generates Sharpy source text fragments for use in code action edits.
/// Produces Python/Sharpy syntax (not C#).
/// </summary>
internal static class SharpySourceGenerator
{
    internal const int DefaultIndentWidth = 4;

    /// <summary>
    /// Formats a SemanticType as a Sharpy type annotation string.
    /// </summary>
    public static string FormatTypeAnnotation(SemanticType type)
    {
        return type switch
        {
            UnknownType => "",
            BuiltinType bt => bt.Name,
            GenericType gt => FormatGenericType(gt),
            UserDefinedType udt => udt.Name,
            OptionalType opt => $"{FormatTypeAnnotation(opt.UnderlyingType)}?",
            NullableType nt => $"{FormatTypeAnnotation(nt.UnderlyingType)} | None",
            TupleType tt => FormatTupleType(tt),
            FunctionType ft => FormatFunctionType(ft),
            VoidType => "None",
            TypeParameterType tpt => tpt.Name,
            _ => type.GetDisplayName()
        };
    }

    private static string FormatGenericType(GenericType gt)
    {
        var sb = new StringBuilder();
        sb.Append(gt.Name);
        sb.Append('[');
        for (var i = 0; i < gt.TypeArguments.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(FormatTypeAnnotation(gt.TypeArguments[i]));
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string FormatTupleType(TupleType tt)
    {
        var sb = new StringBuilder();
        sb.Append("tuple[");
        for (var i = 0; i < tt.ElementTypes.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(FormatTypeAnnotation(tt.ElementTypes[i]));
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string FormatFunctionType(FunctionType ft)
    {
        var sb = new StringBuilder();
        sb.Append("Callable[[");
        for (var i = 0; i < ft.ParameterTypes.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(FormatTypeAnnotation(ft.ParameterTypes[i]));
        }
        sb.Append("], ");
        sb.Append(FormatTypeAnnotation(ft.ReturnType));
        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// Formats a parameter as a Sharpy parameter string.
    /// Omits the type annotation when type is unknown.
    /// </summary>
    public static string FormatParameter(string name, SemanticType type)
    {
        if (type is UnknownType)
            return name;
        return $"{name}: {FormatTypeAnnotation(type)}";
    }

    /// <summary>
    /// Generates a function definition stub with a pass body. Every line break it writes is
    /// <paramref name="lineBreak"/> — the document's (<see cref="SourceLines.DocumentLineBreak"/>); no default.
    /// </summary>
    public static string FormatFunctionDef(
        string name,
        IReadOnlyList<(string Name, SemanticType Type)> parameters,
        SemanticType? returnType,
        int indentLevel,
        string lineBreak,
        string? body = null)
    {
        var indent = new string(' ', indentLevel * DefaultIndentWidth);
        var bodyIndent = new string(' ', (indentLevel + 1) * DefaultIndentWidth);
        var sb = new StringBuilder();

        sb.Append(indent);
        sb.Append("def ");
        sb.Append(name);
        sb.Append('(');

        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(FormatParameter(parameters[i].Name, parameters[i].Type));
        }

        sb.Append(')');

        if (returnType is not null and not VoidType and not UnknownType)
        {
            sb.Append(" -> ");
            sb.Append(FormatTypeAnnotation(returnType));
        }

        sb.Append(':');
        sb.Append(lineBreak);
        sb.Append(bodyIndent);
        sb.Append(body ?? "pass");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a property definition stub. Every line break it writes is <paramref name="lineBreak"/> —
    /// the document's (<see cref="SourceLines.DocumentLineBreak"/>); no default.
    /// </summary>
    public static string FormatPropertyDef(
        string name,
        SemanticType type,
        bool hasGetter,
        bool hasSetter,
        int indentLevel,
        string lineBreak)
    {
        var indent = new string(' ', indentLevel * DefaultIndentWidth);
        var bodyIndent = new string(' ', (indentLevel + 1) * DefaultIndentWidth);
        var sb = new StringBuilder();

        if (hasGetter)
        {
            sb.Append(indent);
            sb.Append("@property");
            sb.Append(lineBreak);
            sb.Append(indent);
            sb.Append(CultureInfo.InvariantCulture, $"def {name}(self) -> {FormatTypeAnnotation(type)}:");
            sb.Append(lineBreak);
            sb.Append(bodyIndent);
            sb.Append("raise NotImplementedError()");
        }

        if (hasSetter)
        {
            if (hasGetter)
                sb.Append(lineBreak).Append(lineBreak);
            sb.Append(indent);
            sb.Append(CultureInfo.InvariantCulture, $"@{name}.setter");
            sb.Append(lineBreak);
            sb.Append(indent);
            sb.Append(CultureInfo.InvariantCulture, $"def {name}(self, value: {FormatTypeAnnotation(type)}):");
            sb.Append(lineBreak);
            sb.Append(bodyIndent);
            sb.Append("raise NotImplementedError()");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the indentation string (leading whitespace) of a specific line (0-based
    /// <paramref name="lineIndex"/>, the LSP line) — <see cref="SourceLines.Indentation"/>.
    /// </summary>
    public static string GetIndentation(string sourceText, int lineIndex) =>
        SourceLines.Of(sourceText).Indentation(lineIndex + 1);

    /// <summary>
    /// Extracts the source text for a given AST node using its line/column positions, verbatim —
    /// <see cref="SourceLines.NodeText"/>.
    /// </summary>
    internal static string? GetNodeSourceText(string sourceText, AstNode node) =>
        SourceLines.Of(sourceText).NodeText(node);

    /// <summary>
    /// Detects the indent unit used in the source (defaults to 4 spaces) — <see cref="SourceLines.IndentUnit"/>.
    /// </summary>
    public static string GetIndentUnit(string sourceText) => SourceLines.Of(sourceText).IndentUnit;
}
