using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles typeHierarchy/supertypes requests.
/// Returns the direct base class and implemented interfaces.
/// </summary>
internal sealed class SharpyTypeHierarchySupertypesHandler : TypeHierarchySupertypesHandlerBase
{
    private readonly LanguageService _languageService;

    public SharpyTypeHierarchySupertypesHandler(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public override async Task<Container<TypeHierarchyItem>?> Handle(
        TypeHierarchySupertypesParams request,
        CancellationToken ct)
    {
        var uri = request.Item.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        // Use the same symbol table for both resolution and supertype lookup
        // to ensure reference equality works with ReferenceEqualityComparer.
        var symbolTable = _languageService.ProjectAnalysis?.ProjectModel.GlobalSymbols
            ?? analysis?.SymbolTable;
        if (symbolTable == null)
            return null;

        var typeSymbol = TypeHierarchyHelper.ResolveFromItem(request.Item, symbolTable);
        if (typeSymbol == null)
            return null;

        // Read supertypes directly from the resolved TypeSymbol (no index needed).
        var items = new List<TypeHierarchyItem>();

        if (typeSymbol.BaseType != null)
        {
            var item = TypeHierarchyHelper.CreateItem(typeSymbol.BaseType, uri);
            if (item != null)
                items.Add(item);
        }

        foreach (var iface in typeSymbol.Interfaces)
        {
            if (iface.Definition != null)
            {
                var item = TypeHierarchyHelper.CreateItem(iface.Definition, uri);
                if (item != null)
                    items.Add(item);
            }
        }

        return items.Any() ? new Container<TypeHierarchyItem>(items) : null;
    }
}
