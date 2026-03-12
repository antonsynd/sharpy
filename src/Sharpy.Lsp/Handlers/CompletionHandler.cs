using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/completion requests.
/// Provides scope-aware, member, and type completions.
/// </summary>
internal sealed class SharplyCompletionHandler : CompletionHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharplyCompletionHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<CompletionList> Handle(CompletionParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.SymbolTable == null)
            return new CompletionList();

        var items = new System.Collections.Generic.List<CompletionItem>();

        // Check if this is a member completion (after '.')
        if (request.Context?.TriggerCharacter == "." && analysis.Ast != null && analysis.SemanticQuery != null)
        {
            var (line, col) = PositionConverter.ToCompiler(request.Position);
            // Look at the node just before the dot
            var node = _api.FindNodeAtPosition(analysis.Ast, line, System.Math.Max(1, col - 1));
            if (node is Expression expr)
            {
                var type = analysis.SemanticQuery.GetEffectiveType(expr);
                if (type is UserDefinedType udt && udt.Symbol != null)
                {
                    AddMemberCompletions(udt.Symbol, items);
                    return new CompletionList(items);
                }
            }
        }

        // Default: scope completion — all visible symbols
        foreach (var symbol in analysis.SymbolTable.GetVisibleSymbols())
        {
            items.Add(SymbolToCompletionItem(symbol));
        }

        return new CompletionList(items);
    }

    private static void AddMemberCompletions(TypeSymbol typeSymbol, System.Collections.Generic.List<CompletionItem> items)
    {
        foreach (var method in typeSymbol.Methods)
        {
            if (method.Name.StartsWith("__") && method.Name.EndsWith("__"))
                continue; // Skip dunder methods in completion

            items.Add(new CompletionItem
            {
                Label = method.Name,
                Kind = CompletionItemKind.Method,
                Detail = SymbolFormatter.FormatSymbol(method),
            });
        }

        foreach (var field in typeSymbol.Fields)
        {
            items.Add(new CompletionItem
            {
                Label = field.Name,
                Kind = CompletionItemKind.Field,
                Detail = SymbolFormatter.FormatSymbol(field),
            });
        }

        foreach (var prop in typeSymbol.Properties)
        {
            items.Add(new CompletionItem
            {
                Label = prop.Name,
                Kind = CompletionItemKind.Property,
            });
        }

        foreach (var evt in typeSymbol.Events)
        {
            items.Add(new CompletionItem
            {
                Label = evt.Name,
                Kind = CompletionItemKind.Event,
            });
        }
    }

    private static CompletionItem SymbolToCompletionItem(Symbol symbol)
    {
        var (kind, detail) = symbol switch
        {
            VariableSymbol v => (CompletionItemKind.Variable, SymbolFormatter.FormatSymbol(v)),
            FunctionSymbol f => (CompletionItemKind.Function, SymbolFormatter.FormatSymbol(f)),
            TypeSymbol t => (TypeKindToCompletionKind(t.TypeKind), SymbolFormatter.FormatSymbol(t)),
            ModuleSymbol m => (CompletionItemKind.Module, SymbolFormatter.FormatSymbol(m)),
            TypeAliasSymbol a => (CompletionItemKind.TypeParameter, SymbolFormatter.FormatSymbol(a)),
            _ => (CompletionItemKind.Text, symbol.Name)
        };

        return new CompletionItem
        {
            Label = symbol.Name,
            Kind = kind,
            Detail = detail,
        };
    }

    private static CompletionItemKind TypeKindToCompletionKind(TypeKind typeKind)
    {
        return typeKind switch
        {
            TypeKind.Class => CompletionItemKind.Class,
            TypeKind.Struct => CompletionItemKind.Struct,
            TypeKind.Interface => CompletionItemKind.Interface,
            TypeKind.Enum => CompletionItemKind.Enum,
            TypeKind.Union => CompletionItemKind.Enum,
            TypeKind.Delegate => CompletionItemKind.Function,
            _ => CompletionItemKind.Class
        };
    }

    public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
    {
        // No resolve step needed for now
        return Task.FromResult(request);
    }

    protected override CompletionRegistrationOptions CreateRegistrationOptions(
        CompletionCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new CompletionRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy"),
            TriggerCharacters = new Container<string>(".", ":"),
            ResolveProvider = false
        };
    }
}
