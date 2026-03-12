using System.Globalization;
using System.Text;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Generates Sharpy source text fragments for use in code action edits.
/// Produces Python/Sharpy syntax (not C#).
/// </summary>
internal static class SharplySourceGenerator
{
    /// <summary>
    /// Formats a SemanticType as a Sharpy type annotation string.
    /// </summary>
    public static string FormatTypeAnnotation(SemanticType type)
    {
        return type switch
        {
            BuiltinType bt => bt.Name,
            GenericType gt => FormatGenericType(gt),
            UserDefinedType udt => udt.Name,
            OptionalType opt => $"{FormatTypeAnnotation(opt.UnderlyingType)}?",
            NullableType nt => $"{FormatTypeAnnotation(nt.UnderlyingType)}?",
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
    /// </summary>
    public static string FormatParameter(string name, SemanticType type)
    {
        return $"{name}: {FormatTypeAnnotation(type)}";
    }

    /// <summary>
    /// Generates a function definition stub with a pass body.
    /// </summary>
    public static string FormatFunctionDef(
        string name,
        IReadOnlyList<(string Name, SemanticType Type)> parameters,
        SemanticType? returnType,
        int indentLevel,
        string? body = null)
    {
        var indent = new string(' ', indentLevel * 4);
        var bodyIndent = new string(' ', (indentLevel + 1) * 4);
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

        if (returnType != null && returnType is not VoidType)
        {
            sb.Append(" -> ");
            sb.Append(FormatTypeAnnotation(returnType));
        }

        sb.Append(':');
        sb.AppendLine();
        sb.Append(bodyIndent);
        sb.Append(body ?? "pass");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a property definition stub.
    /// </summary>
    public static string FormatPropertyDef(
        string name,
        SemanticType type,
        bool hasGetter,
        bool hasSetter,
        int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);
        var bodyIndent = new string(' ', (indentLevel + 1) * 4);
        var sb = new StringBuilder();

        if (hasGetter)
        {
            sb.Append(indent);
            sb.Append("@property");
            sb.AppendLine();
            sb.Append(indent);
            sb.Append(CultureInfo.InvariantCulture, $"def {name}(self) -> {FormatTypeAnnotation(type)}:");
            sb.AppendLine();
            sb.Append(bodyIndent);
            sb.Append("raise NotImplementedError()");
        }

        if (hasSetter)
        {
            if (hasGetter)
                sb.AppendLine().AppendLine();
            sb.Append(indent);
            sb.Append(CultureInfo.InvariantCulture, $"@{name}.setter");
            sb.AppendLine();
            sb.Append(indent);
            sb.Append(CultureInfo.InvariantCulture, $"def {name}(self, value: {FormatTypeAnnotation(type)}):");
            sb.AppendLine();
            sb.Append(bodyIndent);
            sb.Append("raise NotImplementedError()");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the indentation string (leading whitespace) of a specific line.
    /// </summary>
    public static string GetIndentation(string sourceText, int lineIndex)
    {
        var lines = sourceText.Split('\n');
        if (lineIndex < 0 || lineIndex >= lines.Length)
            return "";

        var line = lines[lineIndex];
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            i++;

        return line[..i];
    }

    /// <summary>
    /// Detects the indent unit used in the source (defaults to 4 spaces).
    /// </summary>
    public static string GetIndentUnit(string sourceText)
    {
        var lines = sourceText.Split('\n');
        foreach (var line in lines)
        {
            if (line.Length > 0 && line[0] == '\t')
                return "\t";

            var spaces = 0;
            foreach (var ch in line)
            {
                if (ch == ' ')
                    spaces++;
                else
                    break;
            }

            if (spaces > 0 && spaces <= 8)
                return new string(' ', spaces);
        }

        return "    ";
    }
}
