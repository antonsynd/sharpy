using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles typeHierarchy/supertypes requests.
/// Returns the direct base class and implemented interfaces.
/// </summary>
internal sealed class SharplyTypeHierarchySupertypesHandler : TypeHierarchySupertypesHandlerBase
{
    private readonly LanguageService _languageService;

    public SharplyTypeHierarchySupertypesHandler(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public override async Task<Container<TypeHierarchyItem>?> Handle(
        TypeHierarchySupertypesParams request,
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

        var supertypes = index.GetDirectSupertypes(typeSymbol);
        if (supertypes.Count == 0)
            return null;

        var filePath = request.Item.Uri.ToString();
        var items = new List<TypeHierarchyItem>();
        foreach (var supertype in supertypes)
        {
            var item = TypeHierarchyHelper.CreateItem(supertype, filePath);
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
