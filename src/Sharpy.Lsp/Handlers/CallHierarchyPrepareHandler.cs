using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using LspSymbolKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/prepareCallHierarchy requests.
/// Resolves the function/method at the cursor position and returns a CallHierarchyItem.
/// </summary>
internal sealed class SharplyCallHierarchyPrepareHandler : CallHierarchyPrepareHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharplyCallHierarchyPrepareHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<Container<CallHierarchyItem>?> Handle(
        CallHierarchyPrepareParams request,
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

        var symbol = ResolveSymbol(node, analysis);
        if (symbol is not FunctionSymbol funcSymbol)
            return null;

        var item = CreateCallHierarchyItem(funcSymbol, uri);
        if (item == null)
            return null;

        return new Container<CallHierarchyItem>(item);
    }

    private static Symbol? ResolveSymbol(Node node, SemanticResult analysis)
    {
        var query = analysis.SemanticQuery!;

        return node switch
        {
            Identifier id => query.GetIdentifierSymbol(id),
            FunctionCall call => query.GetCallTarget(call),
            _ => null
        };
    }

    internal static CallHierarchyItem? CreateCallHierarchyItem(FunctionSymbol symbol, string fallbackUri)
    {
        if (symbol.DeclarationLine == null || symbol.DeclarationColumn == null)
            return null;

        var startLine = System.Math.Max(0, symbol.DeclarationLine.Value - 1);
        var startCol = System.Math.Max(0, symbol.DeclarationColumn.Value - 1);
        var endCol = startCol + symbol.Name.Length;

        var filePath = symbol.DeclaringFilePath ?? fallbackUri;
        var docUri = filePath.StartsWith("file://", StringComparison.Ordinal)
            ? DocumentUri.From(filePath)
            : DocumentUri.FromFileSystemPath(filePath);

        var selectionRange = new LspRange(
            new Position(startLine, startCol),
            new Position(startLine, endCol));

        // Use a broader range for the full function if DeclarationSpan is available
        var range = selectionRange;

        var data = new JObject
        {
            ["name"] = symbol.Name,
            ["filePath"] = filePath
        };

        return new CallHierarchyItem
        {
            Name = symbol.Name,
            Kind = LspSymbolKind.Function,
            Uri = docUri,
            Range = range,
            SelectionRange = selectionRange,
            Data = data
        };
    }

    protected override CallHierarchyRegistrationOptions CreateRegistrationOptions(
        CallHierarchyCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new CallHierarchyRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
