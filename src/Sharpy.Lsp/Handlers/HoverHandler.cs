using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/hover requests.
/// Returns type information and symbol documentation for the node at the cursor position.
/// </summary>
internal sealed class SharpyHoverHandler : HoverHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharpyHoverHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<Hover?> Handle(HoverParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var node = _api.FindNodeAtPosition(analysis.Ast, line, col);

        if (node == null)
            return null;

        var hoverMarkdown = GetHoverMarkdown(node, analysis, line, col);
        if (hoverMarkdown == null)
            return null;

        return new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(
                new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = hoverMarkdown
                }
            )
        };
    }

    private string? GetHoverMarkdown(Node node, SemanticResult analysis, int line, int col)
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

                    var typeSymbol = analysis.SymbolTable?.LookupType(classDef.Name);
                    if (typeSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(typeSymbol);
                    break;
                }

            case StructDef structDef:
                {
                    var typeSymbol = analysis.SymbolTable?.LookupType(structDef.Name);
                    if (typeSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(typeSymbol);
                    break;
                }

            case InterfaceDef interfaceDef:
                {
                    var typeSymbol = analysis.SymbolTable?.LookupType(interfaceDef.Name);
                    if (typeSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(typeSymbol);
                    break;
                }

            case EnumDef enumDef:
                {
                    var typeSymbol = analysis.SymbolTable?.LookupType(enumDef.Name);
                    if (typeSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(typeSymbol);
                    break;
                }

            case VariableDeclaration varDecl:
                {
                    // Check if cursor is on the type annotation
                    if (varDecl.Type != null)
                    {
                        var typeHover = TryFormatTypeAnnotation(analysis, query, varDecl.Type, line, col);
                        if (typeHover != null)
                            return typeHover;
                    }

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
                    break;
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

            // String literals: suppress hover (self-evident type)
            case StringLiteral:
            case FStringLiteral:
                return null;

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

    private static string? TryFormatTypeAnnotation(
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

    private string? FindEnclosingTypeName(SemanticResult analysis, int line, int col)
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

    private static bool IsPositionInRange(int line, int col, int startLine, int startCol, int endLine, int endCol)
    {
        if (line < startLine || line > endLine)
            return false;
        if (line == startLine && col < startCol)
            return false;
        if (line == endLine && col > endCol)
            return false;
        return true;
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(
        HoverCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new HoverRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
