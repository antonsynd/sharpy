using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles callHierarchy/incomingCalls requests.
/// Finds all callers of the given function and returns them as incoming call items.
/// </summary>
internal sealed class SharplyCallHierarchyIncomingHandler : CallHierarchyIncomingHandlerBase
{
    private readonly SharplyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharplyCallHierarchyIncomingHandler(
        SharplyWorkspace workspace,
        LanguageService languageService,
        CompilerApi api)
    {
        _workspace = workspace;
        _languageService = languageService;
        _api = api;
    }

    public override async Task<Container<CallHierarchyIncomingCall>?> Handle(
        CallHierarchyIncomingCallsParams request,
        CancellationToken ct)
    {
        var data = request.Item.Data as JObject;
        var symbolName = data?["name"]?.Value<string>() ?? request.Item.Name;
        var filePath = data?["filePath"]?.Value<string>();

        var results = new List<CallHierarchyIncomingCall>();

        // Search all workspace files for references to this function
        var allUris = _workspace.GetAllDocumentUris();

        foreach (var uri in allUris)
        {
            try
            {
                var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);
                if (analysis?.Ast == null || analysis.SemanticQuery == null)
                    continue;

                var references = analysis.SemanticQuery.FindReferencesBySymbolIdentity(symbolName, filePath);
                foreach (var refLoc in references)
                {
                    // Find the containing function at the reference location
                    var containingFunc = _api.FindNodeOfType<FunctionDef>(analysis.Ast, refLoc.Line, refLoc.Column);
                    if (containingFunc == null)
                        continue;

                    // Resolve the containing function's symbol
                    var containingSymbol = analysis.SymbolTable?.Lookup(containingFunc.Name) as FunctionSymbol;
                    if (containingSymbol == null)
                        continue;

                    var callerItem = SharplyCallHierarchyPrepareHandler.CreateCallHierarchyItem(containingSymbol, uri);
                    if (callerItem == null)
                        continue;

                    // The fromRanges indicate where in the caller the call occurs
                    var callLine = System.Math.Max(0, refLoc.Line - 1);
                    var callCol = System.Math.Max(0, refLoc.Column - 1);
                    var callEnd = callCol + symbolName.Length;
                    var fromRange = new LspRange(
                        new Position(callLine, callCol),
                        new Position(callLine, callEnd));

                    results.Add(new CallHierarchyIncomingCall
                    {
                        From = callerItem,
                        FromRanges = new Container<LspRange>(fromRange)
                    });
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Skip files that fail to analyze
            }
        }

        return new Container<CallHierarchyIncomingCall>(results);
    }

}
