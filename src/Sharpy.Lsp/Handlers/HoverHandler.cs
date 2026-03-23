using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
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

                    // Try resolving as a property on a builtin type (is_ok, is_err, is_some, etc.)
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
                    break;
                }

            // Definition-site hover: show symbol info when hovering over definition names
            case FunctionDef funcDef:
                {
                    // Check if cursor is on a parameter
                    var param = funcDef.Parameters.FirstOrDefault(p =>
                        IsPositionInRange(line, col, p.LineStart, p.ColumnStart, p.LineEnd, p.ColumnEnd));
                    if (param != null)
                    {
                        string? className = null;
                        var enclosingClass = _api.FindNodeOfType<ClassDef>(analysis.Ast!, line, col);
                        if (enclosingClass != null)
                            className = enclosingClass.Name;
                        else
                        {
                            var enclosingStruct = _api.FindNodeOfType<StructDef>(analysis.Ast!, line, col);
                            if (enclosingStruct != null)
                                className = enclosingStruct.Name;
                        }
                        return SymbolFormatter.FormatParameterWithDocs(param.Name, param.Type != null
                            ? query.GetTypeAnnotation(param.Type) : null, className);
                    }

                    // Hover on function name
                    var funcSymbol = analysis.SymbolTable?.LookupFunction(funcDef.Name);
                    if (funcSymbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(funcSymbol);
                    break;
                }

            case ClassDef classDef:
                {
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
                    var varSymbol = analysis.SymbolTable?.LookupVariable(varDecl.Name);
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
