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
            return SpellName(name);
        return $"{SpellName(name)}: {FormatTypeAnnotation(type)}";
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Spellings = new(StringComparer.Ordinal);

    /// <summary>
    /// A name as generated text must spell it: bare when the lexer reads it as one identifier AND the parser takes
    /// it as an argument (`f(name)`), in backticks otherwise — a keyword (`let`) or a soft keyword the parser reads
    /// as a statement start (`match`) does not parse bare as a generated parameter, argument or binding (P47,
    /// #2232). The lexer and the parser are the judges, so no keyword list is kept here.
    /// </summary>
    public static string SpellName(string name) => Spellings.GetOrAdd(name, static n =>
    {
        try
        {
            var logger = Sharpy.Compiler.Logging.NullLogger.Instance;
            var tokens = new Sharpy.Compiler.Lexer.Lexer(n, logger).TokenizeAll();
            var first = tokens.Count > 0 ? tokens[0] : null;
            if (first is not { Type: Sharpy.Compiler.Lexer.TokenType.Identifier } || first.Value != n || first.IsBacktickEscaped)
                return $"`{n}`";

            var probe = Sharpy.Compiler.FileCompilationPipeline.Lex(new Sharpy.Compiler.Text.SourceText($"f({n})", "<name>"), logger);
            var parse = Sharpy.Compiler.FileCompilationPipeline.Parse(probe.Tokens, logger);
            return probe.HasErrors || parse.HasErrors ? $"`{n}`" : n;
        }
        catch (Exception)
        {
            return $"`{n}`";
        }
    });

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
    /// Generates a property definition stub in Sharpy's function-style property syntax
    /// (<c>docs/language_specification/properties_function_style.md</c>: <c>property get name(self) -&gt; T:</c>,
    /// <c>property set name(self, value: T):</c>) — never Python's <c>@property</c> / <c>@name.setter</c>,
    /// which Sharpy refuses (SPY0148). Every line break it writes is <paramref name="lineBreak"/> — the
    /// document's (<see cref="SourceLines.DocumentLineBreak"/>); no default.
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
            sb.Append(CultureInfo.InvariantCulture, $"property get {name}(self) -> {FormatTypeAnnotation(type)}:");
            sb.Append(lineBreak);
            sb.Append(bodyIndent);
            sb.Append("raise NotImplementedError()");
        }

        if (hasSetter)
        {
            if (hasGetter)
                sb.Append(lineBreak).Append(lineBreak);
            sb.Append(indent);
            sb.Append(CultureInfo.InvariantCulture, $"property set {name}(self, value: {FormatTypeAnnotation(type)}):");
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
