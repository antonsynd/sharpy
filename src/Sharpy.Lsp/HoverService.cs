using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;

namespace Sharpy.Lsp;

/// <summary>
/// Resolves hover information for AST nodes.
/// Extracted from HoverHandler so it can be used by both the LSP server and the CLI.
/// </summary>
public sealed class HoverService
{
    private readonly CompilerApi _api;

    public HoverService(CompilerApi api)
    {
        _api = api;
    }

    /// <summary>
    /// Result of a hover resolution, containing the markdown content and the AST node
    /// whose range should be highlighted. Optional Highlight* overrides narrow the
    /// highlight to a subrange of the node (e.g. the <c>await</c> keyword inside an
    /// <see cref="AwaitExpression"/>).
    /// </summary>
    public sealed record HoverResult(string Markdown, Node Node)
    {
        public int? HighlightLineStart { get; init; }
        public int? HighlightColumnStart { get; init; }
        public int? HighlightLineEnd { get; init; }
        public int? HighlightColumnEnd { get; init; }
    }

    /// <summary>
    /// Returns hover markdown for the given position in the analysis result,
    /// or null if no hover information is available.
    /// Line and column are 1-based (compiler convention).
    /// </summary>
    public string? GetHoverMarkdown(SemanticResult analysis, int line, int col)
    {
        return GetHoverResult(analysis, line, col)?.Markdown;
    }

    /// <summary>
    /// Returns hover markdown and the resolved AST node for the given position,
    /// or null if no hover information is available.
    /// The node's LineStart/ColumnStart/LineEnd/ColumnEnd can be used to set the hover range.
    /// Line and column are 1-based (compiler convention).
    /// </summary>
    public HoverResult? GetHoverResult(SemanticResult analysis, int line, int col)
    {
        if (analysis.Ast == null || analysis.SemanticQuery == null)
            return null;

        // Suppress hover when the cursor is inside a comment.
        if (IsInsideComment(analysis.CommentSpans, line, col))
            return null;

        // First, check if the cursor is on a source-generator bracket attribute.
        // Decorators are not Nodes (so FindNodeAtPosition won't return them) — we walk
        // the AST manually to look them up.
        var generatorHover = TryResolveSourceGeneratorDecoratorHover(analysis, line, col);
        if (generatorHover != null)
            return generatorHover;

        var node = _api.FindNodeAtPosition(analysis.Ast, line, col);
        if (node == null)
            return null;

        // Keyword/operator nodes delegate to their operand's hover and narrow the
        // highlight to the keyword token. Attempt this first so that hovering `not p`
        // still produces hover text even though builtin UnaryOp hover is suppressed.
        var narrowed = TryNarrowToKeyword(node, analysis);
        if (narrowed != null)
            return AppendGeneratorAttribution(narrowed, analysis, line, col);

        var markdown = GetHoverMarkdownForNode(node, analysis, line, col);
        if (markdown == null)
            return null;

        markdown = AppendGeneratorAttributionMarkdown(markdown, analysis, line, col);

        // Narrow highlight to the name span for definition-site hovers,
        // or to the keyword token for keyword statements like 'return'.
        var highlight = TryNarrowHighlight(node, line, col);
        if (highlight != null)
            return new HoverResult(markdown, node)
            {
                HighlightLineStart = highlight.Value.line,
                HighlightColumnStart = highlight.Value.colStart,
                HighlightLineEnd = highlight.Value.line,
                HighlightColumnEnd = highlight.Value.colEnd,
            };

        return new HoverResult(markdown, node);
    }

    /// <summary>
    /// For compound expressions whose hover target is a leading keyword/operator token
    /// (e.g. <c>await foo()</c>, <c>yield x</c>, <c>not p</c>), delegate the hover text
    /// to the inner operand while narrowing the highlight range to just the keyword
    /// token span. Returns null when the node is not one of the supported kinds or when
    /// the inner delegation yields no markdown (in which case callers should keep the
    /// original result).
    /// </summary>
    private HoverResult? TryNarrowToKeyword(Node node, SemanticResult analysis)
    {
        Expression? operand;
        int keywordLength;
        switch (node)
        {
            case YieldStatement yieldStmt:
                if (yieldStmt.Value == null)
                    return null;
                operand = yieldStmt.Value;
                keywordLength = yieldStmt.IsFrom ? 10 : 5; // "yield from" or "yield"
                break;
            case UnaryOp { Operator: UnaryOperator.Not } unaryOp:
                operand = unaryOp.Operand;
                keywordLength = 3; // "not"
                break;
            default:
                return null;
        }

        var innerMarkdown = GetHoverMarkdownForNode(operand, analysis, operand.LineStart, operand.ColumnStart);
        if (innerMarkdown == null)
            return null;

        return new HoverResult(innerMarkdown, node)
        {
            HighlightLineStart = node.LineStart,
            HighlightColumnStart = node.ColumnStart,
            HighlightLineEnd = node.LineStart,
            HighlightColumnEnd = node.ColumnStart + keywordLength,
        };
    }

    /// <summary>
    /// Narrows the hover highlight range for definition nodes (to the name identifier)
    /// and keyword statements like <c>return</c> (to the keyword token). Returns null
    /// when no narrowing applies.
    /// </summary>
    private static (int line, int colStart, int colEnd)? TryNarrowHighlight(Node node, int cursorLine, int cursorCol)
    {
        return node switch
        {
            FunctionDef f when IsOnHeaderName(cursorLine, cursorCol, f.NameLineStart, f.NameColumnStart, f.Name)
                => (f.NameLineStart, f.NameColumnStart, f.NameColumnStart + f.Name.Length),
            ClassDef c when IsOnHeaderName(cursorLine, cursorCol, c.NameLineStart, c.NameColumnStart, c.Name)
                => (c.NameLineStart, c.NameColumnStart, c.NameColumnStart + c.Name.Length),
            StructDef s when IsOnHeaderName(cursorLine, cursorCol, s.NameLineStart, s.NameColumnStart, s.Name)
                => (s.NameLineStart, s.NameColumnStart, s.NameColumnStart + s.Name.Length),
            InterfaceDef i when IsOnHeaderName(cursorLine, cursorCol, i.NameLineStart, i.NameColumnStart, i.Name)
                => (i.NameLineStart, i.NameColumnStart, i.NameColumnStart + i.Name.Length),
            EnumDef e when IsOnHeaderName(cursorLine, cursorCol, e.NameLineStart, e.NameColumnStart, e.Name)
                => (e.NameLineStart, e.NameColumnStart, e.NameColumnStart + e.Name.Length),
            AwaitExpression aw
                => (aw.LineStart, aw.ColumnStart, aw.ColumnStart + 5), // "await"
            ReturnStatement ret
                => (ret.LineStart, ret.ColumnStart, ret.ColumnStart + 6), // "return"
            _ => null
        };
    }

    private static bool IsInsideComment(IReadOnlyList<CommentSpan> spans, int line, int col)
    {
        if (spans == null || spans.Count == 0)
            return false;
        foreach (var span in spans)
        {
            if (span.Line == line && col >= span.StartColumn && col < span.EndColumn)
                return true;
        }
        return false;
    }

    internal string? GetHoverMarkdownForNode(Node node, SemanticResult analysis, int line, int col)
    {
        var query = analysis.SemanticQuery!;

        switch (node)
        {
            case Identifier id:
                {
                    var symbol = query.GetIdentifierSymbol(id);
                    if (symbol is VariableSymbol vs && vs.Name == PythonNames.Self
                        && (vs.Type is null or UnknownType))
                    {
                        var className = FindEnclosingTypeName(analysis, line, col);
                        if (className != null)
                            return $"```sharpy\n(self) self: {className}\n```";
                    }
                    if (symbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(symbol);

                    // Fall back to type info
                    var type = query.GetEffectiveType(id);
                    if (type != null)
                        return $"```sharpy\n{id.Name}: {SymbolFormatter.FormatTypeInfo(type)}\n```";

                    return null;
                }

            case MemberAccess memberAccess:
                {
                    // Check if TypeChecker recorded a static/const member resolution
                    var resolution = query.GetMemberAccessResolution(memberAccess);
                    if (resolution != null)
                        return SymbolFormatter.FormatSymbolWithDocs(resolution.Value.Member);

                    // Check if this MemberAccess is the Function of an enclosing FunctionCall
                    // (e.g., items.count(2) — cursor on "count" gives MemberAccess, but the
                    // resolved call target is on the FunctionCall node)
                    var enclosingCall = _api.FindNodeOfType<FunctionCall>(analysis.Ast!, line, col);
                    if (enclosingCall != null && ReferenceEquals(enclosingCall.Function, memberAccess))
                    {
                        var target = query.GetCallTarget(enclosingCall);
                        if (target != null)
                            return SymbolFormatter.FormatSymbolWithDocs(target);
                    }

                    // super().method — resolve parent class method
                    if (memberAccess.Object is FunctionCall { Function: SuperExpression })
                    {
                        var superTarget = ResolveSuperMethodCall(analysis, line, col, memberAccess.Member);
                        if (superTarget != null)
                            return SymbolFormatter.FormatSymbolWithDocs(superTarget);
                    }

                    // Try resolving as a property or method on a builtin type
                    var objType = query.GetEffectiveType(memberAccess.Object);
                    if (objType != null && analysis.SymbolTable != null)
                    {
                        var builtinType = objType switch
                        {
                            GenericType gt => analysis.SymbolTable.BuiltinRegistry.GetType(gt.Name),
                            ResultType => analysis.SymbolTable.BuiltinRegistry.GetType(BuiltinNames.Result),
                            OptionalType => analysis.SymbolTable.BuiltinRegistry.GetType(BuiltinNames.Optional),
                            BuiltinType bt => analysis.SymbolTable.BuiltinRegistry.GetType(bt.Name),
                            _ => null
                        };
                        if (builtinType != null)
                        {
                            var prop = builtinType.Properties.FirstOrDefault(p => p.Name == memberAccess.Member);
                            if (prop != null)
                                return SymbolFormatter.FormatPropertyWithDocs(prop);

                            // Check methods (e.g., list.append, dict.get) — shows XML docs from Sharpy.Core
                            var method = builtinType.Methods.FirstOrDefault(m => m.Name == memberAccess.Member);
                            if (method != null)
                                return SymbolFormatter.FormatSymbolWithDocs(method);
                        }
                    }

                    // Fall back to type info for the member access expression
                    var memberType = query.GetEffectiveType(memberAccess);
                    if (memberType != null)
                        return $"```sharpy\n{memberAccess.Member}: {SymbolFormatter.FormatTypeInfo(memberType)}\n```";

                    return null;
                }

            case FunctionCall call:
                {
                    var target = query.GetCallTarget(call);
                    if (target != null)
                        return SymbolFormatter.FormatSymbolWithDocs(target);

                    // super().method() — resolve parent class method
                    if (call.Function is MemberAccess superMember
                        && superMember.Object is FunctionCall { Function: SuperExpression })
                    {
                        var superTarget = ResolveSuperMethodCall(analysis, line, col, superMember.Member);
                        if (superTarget != null)
                            return SymbolFormatter.FormatSymbolWithDocs(superTarget);
                    }
                    break;
                }

            // Definition-site hover: show symbol info when hovering over definition names
            case FunctionDef funcDef:
                {
                    // Check if cursor is on a type annotation (return type or parameter type)
                    var typeAnnotationHover = TryResolveTypeAnnotationHover(analysis, query, funcDef, line, col);
                    if (typeAnnotationHover != null)
                        return typeAnnotationHover;

                    // Check if cursor is on a parameter
                    var param = funcDef.Parameters.FirstOrDefault(p =>
                        IsPositionInRange(line, col, p.LineStart, p.ColumnStart, p.LineEnd, p.ColumnEnd));
                    if (param != null)
                    {
                        var className = FindEnclosingTypeName(analysis, line, col);
                        return SymbolFormatter.FormatParameterWithDocs(param.Name, param.Type != null
                            ? query.GetTypeAnnotation(param.Type) : null, className);
                    }

                    // Only resolve to the function symbol when the cursor is on the
                    // header identifier — not in the body whitespace or elsewhere.
                    if (line != funcDef.NameLineStart ||
                        col < funcDef.NameColumnStart ||
                        col >= funcDef.NameColumnStart + funcDef.Name.Length)
                        break;

                    // Hover on function name — try global scope first, then class scope
                    var funcSymbol = analysis.SymbolTable?.LookupFunction(funcDef.Name);
                    if (funcSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(funcSymbol);

                    funcSymbol = LookupMethodOnType(analysis, line, col, funcDef.Name);
                    if (funcSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(funcSymbol);
                    break;
                }

            case ClassDef classDef:
                {
                    // Check if cursor is on a base class type annotation
                    foreach (var baseClass in classDef.BaseClasses)
                    {
                        var baseHover = TryFormatTypeAnnotation(analysis, query, baseClass, line, col);
                        if (baseHover != null)
                            return baseHover;
                    }

                    if (!IsOnHeaderName(line, col, classDef.NameLineStart, classDef.NameColumnStart, classDef.Name))
                        break;

                    var typeSymbol = analysis.SymbolTable?.LookupType(classDef.Name);
                    if (typeSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(typeSymbol);
                    break;
                }

            case StructDef structDef:
                {
                    if (!IsOnHeaderName(line, col, structDef.NameLineStart, structDef.NameColumnStart, structDef.Name))
                        break;

                    var typeSymbol = analysis.SymbolTable?.LookupType(structDef.Name);
                    if (typeSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(typeSymbol);
                    break;
                }

            case InterfaceDef interfaceDef:
                {
                    if (!IsOnHeaderName(line, col, interfaceDef.NameLineStart, interfaceDef.NameColumnStart, interfaceDef.Name))
                        break;

                    var typeSymbol = analysis.SymbolTable?.LookupType(interfaceDef.Name);
                    if (typeSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(typeSymbol);
                    break;
                }

            case EnumDef enumDef:
                {
                    if (!IsOnHeaderName(line, col, enumDef.NameLineStart, enumDef.NameColumnStart, enumDef.Name))
                        break;

                    var typeSymbol = analysis.SymbolTable?.LookupType(enumDef.Name);
                    if (typeSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(typeSymbol);
                    break;
                }

            case WithStatement withStmt:
                {
                    foreach (var item in withStmt.Items)
                    {
                        if (item.Name != null &&
                            line == item.NameLineStart &&
                            col >= item.NameColumnStart &&
                            col < item.NameColumnStart + item.Name.Length)
                        {
                            var withVarSymbol = analysis.SemanticQuery?.GetWithItemSymbol(item);
                            if (withVarSymbol != null)
                                return SymbolFormatter.FormatSymbolWithDocs(withVarSymbol);
                        }
                    }
                    break;
                }

            case VariableDeclaration varDecl:
                {
                    // Check if cursor is on the variable name
                    if (line == varDecl.NameLineStart &&
                        col >= varDecl.NameColumnStart &&
                        col < varDecl.NameColumnStart + varDecl.Name.Length)
                    {
                        var nameSymbol = analysis.SymbolTable?.LookupVariable(varDecl.Name);
                        if (nameSymbol != null)
                            return SymbolFormatter.FormatSymbolWithDocs(nameSymbol);
                        nameSymbol = LookupFieldOnType(analysis, line, col, varDecl.Name);
                        if (nameSymbol != null)
                            return SymbolFormatter.FormatSymbolWithDocs(nameSymbol);
                    }

                    // Check if cursor is on the type annotation
                    if (varDecl.Type != null)
                    {
                        var typeHover = TryFormatTypeAnnotation(analysis, query, varDecl.Type, line, col);
                        if (typeHover != null)
                            return typeHover;
                    }

                    // Fallback: cursor is on the declaration but not on the name or type annotation
                    // (e.g., on '=' or whitespace within the node span). Show variable info as a
                    // reasonable default rather than returning nothing.
                    var varSymbol = analysis.SymbolTable?.LookupVariable(varDecl.Name);
                    if (varSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(varSymbol);

                    varSymbol = LookupFieldOnType(analysis, line, col, varDecl.Name);
                    if (varSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(varSymbol);
                    break;
                }

            case PropertyDef propDef:
                {
                    // Check parameters first (for function-style properties)
                    var param = propDef.Parameters.FirstOrDefault(p =>
                        IsPositionInRange(line, col, p.LineStart, p.ColumnStart, p.LineEnd, p.ColumnEnd));
                    if (param != null)
                        return SymbolFormatter.FormatParameterWithDocs(param.Name, param.Type != null
                            ? query.GetTypeAnnotation(param.Type) : null);

                    // Property type info
                    var typeStr = propDef.Type != null
                        ? query.GetTypeAnnotation(propDef.Type)?.GetDisplayName() ?? "unknown"
                        : propDef.ReturnType != null
                            ? query.GetTypeAnnotation(propDef.ReturnType)?.GetDisplayName() ?? "unknown"
                            : "unknown";
                    return $"```sharpy\n(property) {propDef.Name}: {typeStr}\n```";
                }

            case TypeAlias typeAlias:
                {
                    Symbol? aliasSymbol = analysis.SymbolTable?.LookupTypeAlias(typeAlias.Name)
                        ?? (Symbol?)analysis.SymbolTable?.LookupType(typeAlias.Name);
                    if (aliasSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(aliasSymbol);
                    // Fallback for scoped aliases not in SymbolTable
                    return FormatTypeAliasFromAst(typeAlias);
                }

            // Keyword expression hover
            case AwaitExpression awaitExpr:
                {
                    var resultType = query.GetEffectiveType(awaitExpr);
                    if (resultType != null)
                        return $"```sharpy\n(await) -> {SymbolFormatter.FormatTypeInfo(resultType)}\n```";
                    return "```sharpy\n(await)\n```";
                }

            case YieldStatement yieldStmt:
                {
                    if (yieldStmt.Value != null)
                    {
                        var yieldType = query.GetEffectiveType(yieldStmt.Value);
                        if (yieldType != null)
                            return $"```sharpy\n(yield) {SymbolFormatter.FormatTypeInfo(yieldType)}\n```";
                    }
                    return "```sharpy\n(yield)\n```";
                }

            case ReturnStatement returnStmt:
                {
                    if (returnStmt.Value != null)
                    {
                        var returnType = query.GetEffectiveType(returnStmt.Value);
                        if (returnType != null)
                            return $"```sharpy\n(return) -> {SymbolFormatter.FormatTypeInfo(returnType)}\n```";
                    }
                    return "```sharpy\n(return) -> None\n```";
                }

            case SuperExpression:
                {
                    var enclosingClassDef = _api.FindNodeOfType<ClassDef>(analysis.Ast!, line, col);
                    if (enclosingClassDef != null)
                    {
                        var classSym = analysis.SymbolTable?.LookupType(enclosingClassDef.Name);
                        if (classSym?.BaseType != null)
                            return $"```sharpy\n(super) {classSym.BaseType.Name}\n```";
                    }
                    return "```sharpy\n(super)\n```";
                }

            // Suppress hover for builtin-type operators; show for user-defined overloads
            case BinaryOp binOp:
                {
                    var leftType = query.GetEffectiveType(binOp.Left);
                    if (leftType is BuiltinType)
                        return null; // int + int is self-evident
                    var resultType = query.GetEffectiveType(binOp);
                    if (resultType != null)
                        return $"```sharpy\n{SymbolFormatter.FormatTypeInfo(resultType)}\n```";
                    break;
                }

            case UnaryOp unaryOp:
                {
                    var operandType = query.GetEffectiveType(unaryOp.Operand);
                    if (operandType is BuiltinType)
                        return null;
                    var resultType = query.GetEffectiveType(unaryOp);
                    if (resultType != null)
                        return $"```sharpy\n{SymbolFormatter.FormatTypeInfo(resultType)}\n```";
                    break;
                }

            case ComparisonChain compChain:
                {
                    if (compChain.Operands.Length > 0)
                    {
                        var firstType = query.GetEffectiveType(compChain.Operands[0]);
                        if (firstType is BuiltinType)
                            return null;
                    }
                    return $"```sharpy\nbool\n```";
                }

            // Import hover
            case ImportStatement importStmt:
                {
                    if (importStmt.Names.Length > 0)
                    {
                        var moduleName = importStmt.Names[0].Name;
                        var modSymbol = analysis.SymbolTable?.Lookup(moduleName) as ModuleSymbol;
                        if (modSymbol != null)
                            return SymbolFormatter.FormatSymbolWithDocs(modSymbol);
                        return $"```sharpy\n(module) {moduleName}\n```";
                    }
                    break;
                }

            case FromImportStatement fromImport:
                {
                    var modSymbol = analysis.SymbolTable?.Lookup(fromImport.Module) as ModuleSymbol;
                    if (modSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(modSymbol);
                    return $"```sharpy\n(module) {fromImport.Module}\n```";
                }

            case LambdaExpression lambda:
                {
                    var lambdaType = query.GetEffectiveType(lambda);
                    if (lambdaType is Compiler.Semantic.FunctionType ft)
                    {
                        var parts = new System.Collections.Generic.List<string>();
                        for (int i = 0; i < lambda.Parameters.Length; i++)
                        {
                            var paramName = lambda.Parameters[i].Name;
                            var paramType = i < ft.ParameterTypes.Count
                                ? SymbolFormatter.FormatTypeInfo(ft.ParameterTypes[i])
                                : "unknown";
                            parts.Add($"{paramName}: {paramType}");
                        }
                        var paramStr = string.Join(", ", parts);
                        var returnStr = ft.ReturnType is not (null or VoidType)
                            ? $" -> {SymbolFormatter.FormatTypeInfo(ft.ReturnType)}"
                            : " -> None";
                        return $"```sharpy\n(lambda) ({paramStr}){returnStr}\n```";
                    }
                    var exprType = query.GetEffectiveType(lambda);
                    if (exprType != null)
                        return $"```sharpy\n{SymbolFormatter.FormatTypeInfo(exprType)}\n```";
                    return null;
                }

            case Expression expr:
                {
                    var type = query.GetEffectiveType(expr);
                    if (type != null)
                        return $"```sharpy\n{SymbolFormatter.FormatTypeInfo(type)}\n```";
                    break;
                }
        }

        return null;
    }

    private static string? TryResolveTypeAnnotationHover(
        SemanticResult analysis, ISemanticQuery query, FunctionDef funcDef, int line, int col)
    {
        // Check return type annotation
        if (funcDef.ReturnType != null)
        {
            var hover = TryFormatTypeAnnotation(analysis, query, funcDef.ReturnType, line, col);
            if (hover != null)
                return hover;
        }

        // Check parameter type annotations
        foreach (var param in funcDef.Parameters)
        {
            if (param.Type != null)
            {
                var hover = TryFormatTypeAnnotation(analysis, query, param.Type, line, col);
                if (hover != null)
                    return hover;
            }
        }

        return null;
    }

    internal static string? TryFormatTypeAnnotation(
        SemanticResult analysis, ISemanticQuery query, TypeAnnotation typeAnnotation, int line, int col)
    {
        if (!IsPositionInRange(line, col,
                typeAnnotation.LineStart, typeAnnotation.ColumnStart,
                typeAnnotation.LineEnd, typeAnnotation.ColumnEnd))
            return null;

        // Check type arguments first (most-specific match wins)
        foreach (var typeArg in typeAnnotation.TypeArguments)
        {
            var argHover = TryFormatTypeAnnotation(analysis, query, typeArg, line, col);
            if (argHover != null)
                return argHover;
        }

        // Check error type for result types (T !E)
        if (typeAnnotation.ErrorType != null)
        {
            var errorHover = TryFormatTypeAnnotation(analysis, query, typeAnnotation.ErrorType, line, col);
            if (errorHover != null)
                return errorHover;
        }

        // Try to resolve as a user-defined type
        var typeSymbol = analysis.SymbolTable?.LookupType(typeAnnotation.Name);
        if (typeSymbol != null)
            return SymbolFormatter.FormatSymbolWithDocs(typeSymbol);

        // Try to resolve the semantic type from the type annotation
        var semanticType = query.GetTypeAnnotation(typeAnnotation);
        if (semanticType != null)
            return $"```sharpy\n(type) {SymbolFormatter.FormatTypeInfo(semanticType)}\n```";

        return null;
    }

    private FunctionSymbol? ResolveSuperMethodCall(SemanticResult analysis, int line, int col, string methodName)
    {
        var classDef = _api.FindNodeOfType<ClassDef>(analysis.Ast!, line, col);
        if (classDef == null)
            return null;
        var classSymbol = analysis.SymbolTable?.LookupType(classDef.Name);
        var baseType = classSymbol?.BaseType;
        if (baseType == null)
            return null;
        return baseType.Methods.FirstOrDefault(m => m.Name == methodName);
    }

    internal string? FindEnclosingTypeName(SemanticResult analysis, int line, int col)
    {
        var classDef = _api.FindNodeOfType<ClassDef>(analysis.Ast!, line, col);
        if (classDef != null)
            return classDef.Name;
        var structDef = _api.FindNodeOfType<StructDef>(analysis.Ast!, line, col);
        if (structDef != null)
            return structDef.Name;
        var interfaceDef = _api.FindNodeOfType<InterfaceDef>(analysis.Ast!, line, col);
        if (interfaceDef != null)
            return interfaceDef.Name;
        return null;
    }

    private FunctionSymbol? LookupMethodOnType(SemanticResult analysis, int line, int col, string methodName)
    {
        var typeName = FindEnclosingTypeName(analysis, line, col);
        if (typeName == null)
            return null;
        var typeSymbol = analysis.SymbolTable?.LookupType(typeName);
        return typeSymbol?.Methods.FirstOrDefault(m => m.Name == methodName);
    }

    private VariableSymbol? LookupFieldOnType(SemanticResult analysis, int line, int col, string fieldName)
    {
        var typeName = FindEnclosingTypeName(analysis, line, col);
        if (typeName == null)
            return null;
        var typeSymbol = analysis.SymbolTable?.LookupType(typeName);
        return typeSymbol?.Fields.FirstOrDefault(f => f.Name == fieldName);
    }

    private static string FormatTypeAliasFromAst(TypeAlias typeAlias)
    {
        var typeName = typeAlias.Type != null
            ? FormatTypeAnnotationName(typeAlias.Type)
            : typeAlias.FunctionType != null
                ? FormatFunctionTypeName(typeAlias.FunctionType)
                : "unknown";
        return $"```sharpy\n(type alias) {typeAlias.Name} = {typeName}\n```";
    }

    private static string FormatTypeAnnotationName(TypeAnnotation type)
    {
        if (type.TypeArguments.Length == 0)
            return type.Name;
        var args = string.Join(", ", type.TypeArguments.Select(FormatTypeAnnotationName));
        return $"{type.Name}[{args}]";
    }

    private static string FormatFunctionTypeName(Compiler.Parser.Ast.FunctionType funcType)
    {
        var paramTypes = string.Join(", ", funcType.ParameterTypes.Select(FormatTypeAnnotationName));
        var returnType = FormatTypeAnnotationName(funcType.ReturnType);
        return $"({paramTypes}) -> {returnType}";
    }

    private static bool IsOnHeaderName(int line, int col, int nameLine, int nameCol, string name)
    {
        return line == nameLine && col >= nameCol && col < nameCol + name.Length;
    }

    internal static bool IsPositionInRange(int line, int col, int startLine, int startCol, int endLine, int endCol)
    {
        if (line < startLine || line > endLine)
            return false;
        if (line == startLine && col < startCol)
            return false;
        if (line == endLine && col > endCol)
            return false;
        return true;
    }

    /// <summary>
    /// If the cursor is on a bracket attribute (<c>@[Name]</c>) whose name resolves to a
    /// <see cref="TypeSymbol"/> with <see cref="TypeSymbol.IsSourceGenerator"/> set, returns
    /// a hover describing the source generator and (when known) how many members it generated.
    /// Returns null otherwise.
    /// </summary>
    private HoverResult? TryResolveSourceGeneratorDecoratorHover(SemanticResult analysis, int line, int col)
    {
        if (analysis.Ast == null || analysis.SymbolTable == null)
            return null;

        var match = FindDecoratorAtPosition(analysis.Ast.Body, line, col);
        if (match == null)
            return null;

        var decorator = match.Value.Decorator;
        var decoratedStmt = match.Value.Decorated;

        if (!decorator.IsBracketAttribute || decorator.Name.Length == 0)
            return null;

        var typeSymbol = analysis.SymbolTable.LookupType(decorator.Name);
        if (typeSymbol is null || !typeSymbol.IsSourceGenerator)
            return null;

        var generatorName = decorator.Name;
        int generatedCount = CountGeneratedMembers(analysis, decoratedStmt, generatorName);

        var sb = new System.Text.StringBuilder();
        sb.Append("```sharpy\n");
        sb.Append("(source generator) @[");
        sb.Append(generatorName);
        sb.Append("]\n```\n\n");
        if (generatedCount > 0)
        {
            sb.Append("Source generator: generates ");
            sb.Append(generatedCount);
            sb.Append(generatedCount == 1 ? " member." : " members.");
        }
        else
        {
            sb.Append("Generates code for the decorated declaration.");
        }

        // Bracket attribute span: from '@' to ']'. For decorators with arguments, ColumnEnd
        // already includes the closing bracket; use the recorded span.
        return new HoverResult(sb.ToString(), decoratedStmt)
        {
            HighlightLineStart = decorator.LineStart,
            HighlightColumnStart = decorator.ColumnStart,
            HighlightLineEnd = decorator.LineEnd,
            HighlightColumnEnd = decorator.ColumnEnd,
        };
    }

    /// <summary>
    /// Walks top-level and class-body statements looking for a decorator that contains the
    /// given (1-based) line/column. Returns the matching decorator and the statement it
    /// decorates, or null if no decorator matches.
    /// </summary>
    private static (Decorator Decorator, Statement Decorated)? FindDecoratorAtPosition(
        IEnumerable<Statement> statements, int line, int col)
    {
        foreach (var stmt in statements)
        {
            // Check the statement's own decorators.
            var decorators = GetDecorators(stmt);
            if (decorators != null)
            {
                foreach (var dec in decorators)
                {
                    if (IsPositionInRange(line, col, dec.LineStart, dec.ColumnStart, dec.LineEnd, dec.ColumnEnd))
                        return (dec, stmt);
                }
            }

            // Recurse into bodies that may contain decorated members.
            var body = GetBody(stmt);
            if (body != null)
            {
                var nested = FindDecoratorAtPosition(body, line, col);
                if (nested != null)
                    return nested;
            }
        }
        return null;
    }

    private static System.Collections.Immutable.ImmutableArray<Decorator>? GetDecorators(Statement stmt) => stmt switch
    {
        FunctionDef f => f.Decorators,
        ClassDef c => c.Decorators,
        StructDef s => s.Decorators,
        InterfaceDef i => i.Decorators,
        EnumDef e => e.Decorators,
        VariableDeclaration v => v.Decorators,
        PropertyDef p => p.Decorators,
        _ => null
    };

    private static System.Collections.Immutable.ImmutableArray<Statement>? GetBody(Statement stmt) => stmt switch
    {
        ClassDef c => c.Body,
        StructDef s => s.Body,
        InterfaceDef i => i.Body,
        PropertyDef p => p.Body,
        _ => null
    };

    /// <summary>
    /// Counts statements within <paramref name="decoratedStmt"/>'s body that were tagged
    /// as generated by the specified generator. Returns 0 when the statement has no body
    /// or when no generated members are recorded.
    /// </summary>
    private static int CountGeneratedMembers(SemanticResult analysis, Statement decoratedStmt, string generatorName)
    {
        if (analysis.SemanticQuery == null)
            return 0;

        var body = GetBody(decoratedStmt);
        if (body == null)
            return 0;

        int count = 0;
        foreach (var stmt in body.Value)
        {
            if (analysis.SemanticQuery.IsGenerated(stmt) &&
                analysis.SemanticQuery.GetGeneratorName(stmt) == generatorName)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// If the enclosing declaration at (line, col) was marked as generated, appends a
    /// "Generated by @[X]" line to the markdown. Returns the original markdown unchanged
    /// when no generator attribution applies.
    /// </summary>
    internal string AppendGeneratorAttributionMarkdown(string markdown, SemanticResult analysis, int line, int col)
    {
        if (analysis.SemanticQuery == null || analysis.Ast == null)
            return markdown;

        var enclosing = FindEnclosingTrackedStatement(analysis, line, col);
        if (enclosing == null)
            return markdown;

        if (!analysis.SemanticQuery.IsGenerated(enclosing))
            return markdown;

        var generatorName = analysis.SemanticQuery.GetGeneratorName(enclosing);
        if (string.IsNullOrEmpty(generatorName))
            return markdown;

        return markdown + $"\n\n_Generated by `@[{generatorName}]`_";
    }

    private HoverResult AppendGeneratorAttribution(HoverResult result, SemanticResult analysis, int line, int col)
    {
        var augmented = AppendGeneratorAttributionMarkdown(result.Markdown, analysis, line, col);
        if (ReferenceEquals(augmented, result.Markdown))
            return result;
        return result with { Markdown = augmented };
    }

    /// <summary>
    /// Finds the innermost declaration statement (FunctionDef, ClassDef, StructDef,
    /// VariableDeclaration, PropertyDef, InterfaceDef, EnumDef) containing the position.
    /// Used to attribute hover content to a containing generated declaration.
    /// </summary>
    private Statement? FindEnclosingTrackedStatement(SemanticResult analysis, int line, int col)
    {
        if (analysis.Ast == null)
            return null;

        // Prefer the deepest matching declaration. Check several types in order from
        // "innermost typical" to "outermost".
        Statement? best = null;
        int bestDepth = -1;

        SearchStatements(analysis.Ast.Body, line, col, ref best, ref bestDepth, depth: 0);
        return best;
    }

    private static void SearchStatements(
        IEnumerable<Statement> statements,
        int line, int col,
        ref Statement? best,
        ref int bestDepth,
        int depth)
    {
        foreach (var stmt in statements)
        {
            if (!StatementContainsPosition(stmt, line, col))
                continue;

            if (IsTrackedDeclaration(stmt) && depth > bestDepth)
            {
                best = stmt;
                bestDepth = depth;
            }

            var body = GetBody(stmt);
            if (body != null)
            {
                SearchStatements(body.Value, line, col, ref best, ref bestDepth, depth + 1);
            }
            else if (stmt is FunctionDef f)
            {
                SearchStatements(f.Body, line, col, ref best, ref bestDepth, depth + 1);
            }
        }
    }

    private static bool IsTrackedDeclaration(Statement stmt) => stmt is
        FunctionDef or ClassDef or StructDef or InterfaceDef or EnumDef or VariableDeclaration or PropertyDef;

    private static bool StatementContainsPosition(Statement stmt, int line, int col)
    {
        return IsPositionInRange(line, col, stmt.LineStart, stmt.ColumnStart, stmt.LineEnd, stmt.ColumnEnd);
    }
}
