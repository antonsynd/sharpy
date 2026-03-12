using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/prepareTypeHierarchy requests.
/// Resolves the type symbol at the cursor position and returns a TypeHierarchyItem.
/// </summary>
internal sealed class SharplyTypeHierarchyPrepareHandler : TypeHierarchyPrepareHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharplyTypeHierarchyPrepareHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<Container<TypeHierarchyItem>?> Handle(
        TypeHierarchyPrepareParams request,
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

        var typeSymbol = ResolveTypeSymbol(node, analysis);
        if (typeSymbol == null)
            return null;

        var item = TypeHierarchyHelper.CreateItem(typeSymbol, uri);
        if (item == null)
            return null;

        return new Container<TypeHierarchyItem>(item);
    }

    private static TypeSymbol? ResolveTypeSymbol(Node node, SemanticResult analysis)
    {
        var query = analysis.SemanticQuery!;

        // Direct type definition nodes
        if (node is ClassDef or StructDef or InterfaceDef)
        {
            var name = node switch
            {
                ClassDef c => c.Name,
                StructDef s => s.Name,
                InterfaceDef i => i.Name,
                _ => null
            };

            if (name != null)
                return analysis.SymbolTable?.Lookup(name) as TypeSymbol;
        }

        // Identifier that refers to a type
        if (node is Identifier id)
        {
            var symbol = query.GetIdentifierSymbol(id);
            if (symbol is TypeSymbol ts)
                return ts;
        }

        return null;
    }

    protected override TypeHierarchyRegistrationOptions CreateRegistrationOptions(
        TypeHierarchyCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TypeHierarchyRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
