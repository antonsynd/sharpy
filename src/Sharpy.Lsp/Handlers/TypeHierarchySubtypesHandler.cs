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

        var typeSymbol = TypeHierarchyHelper.ResolveFromItem(request.Item, analysis?.SymbolTable);
        if (typeSymbol == null)
            return null;

        var index = BuildIndex(analysis?.SymbolTable);
        if (index == null)
            return null;

        var subtypes = index.GetDirectSubtypes(typeSymbol);
        if (subtypes.Count == 0)
            return null;

        var filePath = uri;
        var items = new List<TypeHierarchyItem>();
        foreach (var subtype in subtypes)
        {
            var item = TypeHierarchyHelper.CreateItem(subtype, filePath);
            if (item != null)
                items.Add(item);
        }

        return items.Any() ? new Container<TypeHierarchyItem>(items) : null;
    }

    private TypeHierarchyIndex? BuildIndex(SymbolTable? singleFileTable)
    {
        var projectAnalysis = _languageService.ProjectAnalysis;
        var symbolTable = projectAnalysis?.ProjectModel.GlobalSymbols ?? singleFileTable;
        if (symbolTable == null)
            return null;

        return TypeHierarchyIndex.Build(symbolTable);
    }
}
