using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

internal sealed class SharpyTypeDefinitionHandler : TypeDefinitionHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharpyTypeDefinitionHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<LocationOrLocationLinks?> Handle(
        TypeDefinitionParams request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var node = _api.FindNodeAtPosition(analysis.Ast, line, col);

        if (node == null)
            return null;

        var type = ResolveType(node, analysis);
        if (type == null)
            return null;

        var typeSymbol = GetTypeSymbol(type);
        if (typeSymbol == null)
            return null;

        var location = SymbolLocationHelper.GetSymbolLocation(typeSymbol, uri);
        if (location == null)
            return null;

        return new LocationOrLocationLinks(location);
    }

    private static SemanticType? ResolveType(Node node, SemanticResult analysis)
    {
        var query = analysis.SemanticQuery!;

        switch (node)
        {
            case Identifier id:
                {
                    var symbol = query.GetIdentifierSymbol(id);
                    if (symbol is TypeSymbol)
                        return query.GetEffectiveType(id);
                    return symbol switch
                    {
                        VariableSymbol vs => vs.Type,
                        FunctionSymbol fs => fs.ReturnType,
                        _ => query.GetEffectiveType(id)
                    };
                }

            case FunctionCall call:
                return query.GetEffectiveType(call);

            case MemberAccess ma:
                return query.GetEffectiveType(ma);

            case ClassDef cd:
                {
                    var sym = analysis.SymbolTable?.Lookup(cd.Name)
                        ?? analysis.SymbolTable?.LookupInModuleScopes(cd.Name);
                    if (sym is TypeSymbol ts)
                        return new UserDefinedType { Name = ts.Name, Symbol = ts };
                    return null;
                }

            case InterfaceDef ifd:
                {
                    var sym = analysis.SymbolTable?.Lookup(ifd.Name)
                        ?? analysis.SymbolTable?.LookupInModuleScopes(ifd.Name);
                    if (sym is TypeSymbol ts)
                        return new UserDefinedType { Name = ts.Name, Symbol = ts };
                    return null;
                }

            default:
                if (node is Expression expr)
                    return query.GetEffectiveType(expr);
                return null;
        }
    }

    private static TypeSymbol? GetTypeSymbol(SemanticType type)
    {
        return type switch
        {
            UserDefinedType udt => udt.Symbol,
            GenericType gt => GetGenericBaseTypeSymbol(gt),
            NullableType nt => GetTypeSymbol(nt.UnderlyingType),
            OptionalType ot => GetTypeSymbol(ot.UnderlyingType),
            _ => null
        };
    }

    private static TypeSymbol? GetGenericBaseTypeSymbol(GenericType gt)
    {
        return gt.GenericDefinition;
    }

    protected override TypeDefinitionRegistrationOptions CreateRegistrationOptions(
        TypeDefinitionCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TypeDefinitionRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
