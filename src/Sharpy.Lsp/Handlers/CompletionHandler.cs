using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Text;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/completion requests.
/// Provides scope-aware, member, and type completions.
/// </summary>
internal sealed class SharpyCompletionHandler : CompletionHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharpyCompletionHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<CompletionList> Handle(CompletionParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);
        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var isMemberRequest = request.Context?.TriggerCharacter == ".";

        if (analysis?.SymbolTable == null)
        {
            // The buffer does not analyse. For a member request that is not an edge case, it is the
            // NORMAL state: an editor sends completion at the instant the user types '.', and a
            // trailing dot is the most common transient syntax error there is (measured: SPY0101,
            // with Ast, SymbolTable and SemanticQuery all null). "No analysis ⇒ no completion" made
            // member completion fire only on text that already compiles, which is when it is least
            // needed (#1360). Answer from the last good analysis instead, but only when the drift
            // check below can prove the stale tree still describes the code being asked about.
            //
            // Scope completion is deliberately NOT served this way: there is no comparable bound on
            // how stale a whole visible-symbol list may be, so it would be a guess wearing an
            // answer's clothes. That remains today's behaviour (empty).
            return isMemberRequest
                ? MemberCompletionsFromLastGoodAnalysis(uri, line, col)
                : new CompletionList();
        }

        var items = new System.Collections.Generic.List<CompletionItem>();

        // Check if this is a member completion (after '.')
        if (isMemberRequest && analysis.Ast != null && analysis.SemanticQuery != null)
        {
            // Return members when the receiver resolves; return EMPTY when it does not.
            // Falling through to scope completion for a dot-triggered request would dump every
            // visible symbol — none of which are reachable through that dot (#1360).
            TryAddMemberCompletions(analysis, line, col, items);
            return new CompletionList(items);
        }

        // Default: scope completion — all visible symbols
        foreach (var symbol in analysis.SymbolTable.GetVisibleSymbols())
        {
            items.Add(SymbolToCompletionItem(symbol));
        }

        return new CompletionList(items);
    }

    /// <summary>
    /// Adds the completions reachable through the dot at <paramref name="col"/>, returning false
    /// when the receiver offers none — in which case the caller falls through to scope completion,
    /// which is the behaviour that predates this extraction.
    /// </summary>
    private bool TryAddMemberCompletions(
        SemanticResult analysis, int line, int col,
        System.Collections.Generic.List<CompletionItem> items)
    {
        // Look at the node just before the dot. `col - 1` is the dot's own column, and a node's
        // ColumnEnd is exclusive, so this lands on the receiver rather than the member access.
        var node = _api.FindNodeAtPosition(analysis.Ast!, line, System.Math.Max(1, col - 1));
        if (node is not Expression expr)
            return false;

        var type = analysis.SemanticQuery!.GetEffectiveType(expr);
        if (type is UserDefinedType udt && udt.Symbol != null)
        {
            AddMemberCompletions(udt.Symbol, items);
            return true;
        }

        // A module receiver offers that module's exports (#851). Without this arm the request fell
        // through to the scope dump below, so `math.` suggested every visible symbol in the file —
        // the local variables, the user's own functions, the builtins — none of which are reachable
        // through that dot. Empty Exports still falls through: an unloaded module is better served
        // by the scope dump than by an empty list.
        if (type is ModuleType moduleType && moduleType.Symbol.Exports.Count > 0)
        {
            foreach (var (_, exported) in moduleType.Symbol.Exports)
            {
                items.Add(SymbolToCompletionItem(exported));
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Answers a member request from the document's last good analysis while the current text does
    /// not parse (#1360). Returns an empty list rather than a guess whenever the snapshot is
    /// missing or the drift check declines.
    /// </summary>
    private CompletionList MemberCompletionsFromLastGoodAnalysis(string uri, int line, int col)
    {
        var lastGood = _languageService.GetLastGoodAnalysis(uri);
        if (lastGood is not { } snapshot
            || snapshot.Analysis.Ast == null
            || snapshot.Analysis.SemanticQuery == null)
        {
            return new CompletionList();
        }

        var currentText = _languageService.GetSourceText(uri);
        if (currentText == null || !PrefixBeforeDotIsUnchanged(currentText, snapshot.SourceText, line, col))
            return new CompletionList();

        var items = new System.Collections.Generic.List<CompletionItem>();
        return TryAddMemberCompletions(snapshot.Analysis, line, col, items)
            ? new CompletionList(items)
            : new CompletionList();
    }

    /// <summary>
    /// The drift policy, stated rather than left implicit: answer from the stale tree only when
    /// every character BEFORE the trigger dot is byte-identical in the two texts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stale tree is addressed by (line, column), so any edit that shifts what precedes the
    /// cursor — a line inserted above, a rename earlier on the same line — makes those coordinates
    /// name a different expression. Without this check the handler would happily read whatever the
    /// old tree had at that position: edit <c>print(foo)</c> into <c>print(bar.</c> and it would
    /// offer <c>foo</c>'s members, confidently and wrongly.
    /// </para>
    /// <para>
    /// Requiring an identical prefix is exactly satisfied by the case this exists for — the user
    /// typed one '.' at the end of otherwise unchanged text — and declines everything else. It is
    /// deliberately conservative: a declined request returns nothing, which is what it returned
    /// before, so the failure direction is unchanged behaviour rather than a wrong answer.
    /// </para>
    /// </remarks>
    private static bool PrefixBeforeDotIsUnchanged(
        SourceText current, SourceText stale, int line, int col)
    {
        if (line < 1 || line > current.LineCount)
            return false;

        var dotColumn = System.Math.Max(1, col - 1);
        var dotOffset = current.GetPosition(line, dotColumn);
        if (dotOffset < 0 || dotOffset > current.Length || dotOffset > stale.Length)
            return false;

        return string.CompareOrdinal(current.ToString(), 0, stale.ToString(), 0, dotOffset) == 0;
    }

    private static void AddMemberCompletions(TypeSymbol typeSymbol, System.Collections.Generic.List<CompletionItem> items)
    {
        foreach (var method in typeSymbol.Methods)
        {
            if (method.Name.StartsWith("__") && method.Name.EndsWith("__"))
                continue; // Skip dunder methods in completion

            var item = new CompletionItem
            {
                Label = method.Name,
                Kind = CompletionItemKind.Method,
                Detail = SymbolFormatter.FormatSymbol(method),
                Documentation = string.IsNullOrEmpty(method.Documentation) ? null : new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = method.Documentation
                },
            };
            items.Add(item);
        }

        foreach (var field in typeSymbol.Fields)
        {
            var item = new CompletionItem
            {
                Label = field.Name,
                Kind = CompletionItemKind.Field,
                Detail = SymbolFormatter.FormatSymbol(field),
                Documentation = string.IsNullOrEmpty(field.Documentation) ? null : new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = field.Documentation
                },
            };
            items.Add(item);
        }

        foreach (var prop in typeSymbol.Properties)
        {
            var item = new CompletionItem
            {
                Label = prop.Name,
                Kind = CompletionItemKind.Property,
                Documentation = string.IsNullOrEmpty(prop.Documentation) ? null : new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = prop.Documentation
                },
            };
            items.Add(item);
        }

        foreach (var evt in typeSymbol.Events)
        {
            var item = new CompletionItem
            {
                Label = evt.Name,
                Kind = CompletionItemKind.Event,
                Documentation = string.IsNullOrEmpty(evt.Documentation) ? null : new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = evt.Documentation
                },
            };
            items.Add(item);
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

        var item = new CompletionItem
        {
            Label = symbol.Name,
            Kind = kind,
            Detail = detail,
            Documentation = string.IsNullOrEmpty(symbol.Documentation) ? null : new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = symbol.Documentation
            },
        };
        return item;
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
