using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles callHierarchy/outgoingCalls requests.
/// Finds all functions called from the given function and returns them as outgoing call items.
/// </summary>
internal sealed class SharplyCallHierarchyOutgoingHandler : CallHierarchyOutgoingHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharplyCallHierarchyOutgoingHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<Container<CallHierarchyOutgoingCall>?> Handle(
        CallHierarchyOutgoingCallsParams request,
        CancellationToken ct)
    {
        var data = request.Item.Data as JObject;
        var filePath = data?["filePath"]?.Value<string>();

        if (filePath == null)
            return null;

        var uri = filePath.StartsWith("file://", StringComparison.Ordinal)
            ? filePath
            : DocumentUri.FromFileSystemPath(filePath).ToString();

        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);
        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        // Find the function definition in the AST
        var symbolName = data?["name"]?.Value<string>() ?? request.Item.Name;
        var funcDef = FindFunctionDef(analysis.Ast, symbolName);
        if (funcDef == null)
            return null;

        // Walk the function body to find all function calls
        var results = new List<CallHierarchyOutgoingCall>();
        var callNodes = CollectFunctionCalls(funcDef);

        foreach (var call in callNodes)
        {
            var targetSymbol = analysis.SemanticQuery.GetCallTarget(call);
            if (targetSymbol == null)
                continue;

            var targetItem = SharplyCallHierarchyPrepareHandler.CreateCallHierarchyItem(targetSymbol, uri);
            if (targetItem == null)
                continue;

            // The fromRanges indicate where in the source function the call occurs
            var callLine = System.Math.Max(0, call.LineStart - 1);
            var callCol = System.Math.Max(0, call.ColumnStart - 1);
            var callEndLine = System.Math.Max(0, call.LineEnd - 1);
            var callEndCol = System.Math.Max(0, call.ColumnEnd - 1);
            var fromRange = new LspRange(
                new Position(callLine, callCol),
                new Position(callEndLine, callEndCol));

            results.Add(new CallHierarchyOutgoingCall
            {
                To = targetItem,
                FromRanges = new Container<LspRange>(fromRange)
            });
        }

        return new Container<CallHierarchyOutgoingCall>(results);
    }

    private static FunctionDef? FindFunctionDef(Module module, string name)
    {
        foreach (var stmt in module.Body)
        {
            if (stmt is FunctionDef f && string.Equals(f.Name, name, StringComparison.Ordinal))
                return f;

            // Also search within class bodies
            if (stmt is ClassDef c)
            {
                foreach (var member in c.Body)
                {
                    if (member is FunctionDef method && string.Equals(method.Name, name, StringComparison.Ordinal))
                        return method;
                }
            }
        }

        return null;
    }

    private static List<FunctionCall> CollectFunctionCalls(FunctionDef funcDef)
    {
        var calls = new List<FunctionCall>();
        CollectFunctionCallsRecursive(funcDef, calls);
        return calls;
    }

    private static void CollectFunctionCallsRecursive(Node node, List<FunctionCall> calls)
    {
        if (node is FunctionCall call)
            calls.Add(call);

        foreach (var child in node.GetChildNodes())
        {
            CollectFunctionCallsRecursive(child, calls);
        }
    }

}
