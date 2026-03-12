using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles typeHierarchy/subtypes requests.
/// Returns all types that directly extend or implement the given type.
/// </summary>
internal sealed class SharplyTypeHierarchySubtypesHandler : TypeHierarchySubtypesHandlerBase
{
    private readonly LanguageService _languageService;

    public SharplyTypeHierarchySubtypesHandler(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public override async Task<Container<TypeHierarchyItem>?> Handle(
        TypeHierarchySubtypesParams request,
        CancellationToken ct)
    {
        var uri = request.Item.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        // Use the same symbol table for both resolution and index building
        // to ensure reference equality works with ReferenceEqualityComparer.
        var symbolTable = _languageService.ProjectAnalysis?.ProjectModel.GlobalSymbols
            ?? analysis?.SymbolTable;
        if (symbolTable == null)
            return null;

        var typeSymbol = TypeHierarchyHelper.ResolveFromItem(request.Item, symbolTable);
        if (typeSymbol == null)
            return null;

        var index = TypeHierarchyIndex.Build(symbolTable);
        var subtypes = index.GetDirectSubtypes(typeSymbol);
        if (subtypes.Count == 0)
            return null;

        var items = new List<TypeHierarchyItem>();
        foreach (var subtype in subtypes)
        {
            var item = TypeHierarchyHelper.CreateItem(subtype, uri);
            if (item != null)
                items.Add(item);
        }

        return items.Any() ? new Container<TypeHierarchyItem>(items) : null;
    }
}
