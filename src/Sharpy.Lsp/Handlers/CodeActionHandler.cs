using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Refactoring;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/codeAction requests.
/// Delegates to registered ICodeActionProvider instances for extensible code action support.
/// </summary>
internal sealed class SharpyCodeActionHandler : CodeActionHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly IEnumerable<ICodeActionProvider> _providers;

    public SharpyCodeActionHandler(
        LanguageService languageService,
        IEnumerable<ICodeActionProvider> providers)
    {
        _languageService = languageService;
        _providers = providers;
    }

    public override async Task<CommandOrCodeActionContainer?> Handle(
        CodeActionParams request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri;
        var uriString = uri.ToString();

        // Get document text via LanguageService
        var text = _languageService.GetDocumentText(uriString);

        // Get semantic analysis (project-aware if available)
        SemanticResult? analysis = null;
        if (_languageService.IsReady)
        {
            analysis = await _languageService.GetAnalysisAsync(uriString, ct).ConfigureAwait(false);
        }
        analysis ??= _languageService.GetCachedAnalysis(uriString);

        // Build provider context
        var context = new Refactoring.CodeActionProviderContext(
            uri,
            request.Range,
            request.Context.Diagnostics,
            analysis,
            text);

        // Collect actions from all providers
        var actions = new List<CommandOrCodeAction>();
        foreach (var provider in _providers)
        {
            var providerActions = await provider.GetCodeActionsAsync(context, ct).ConfigureAwait(false);
            foreach (var action in providerActions)
            {
                actions.Add(new CommandOrCodeAction(action));
            }
        }

        return new CommandOrCodeActionContainer(actions);
    }

    public override Task<CodeAction> Handle(CodeAction request, CancellationToken ct)
    {
        // Code action resolve — return as-is since our actions are fully resolved on creation
        return Task.FromResult(request);
    }

    protected override CodeActionRegistrationOptions CreateRegistrationOptions(
        CodeActionCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new CodeActionRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy"),
            CodeActionKinds = new Container<CodeActionKind>(
                CodeActionKind.QuickFix,
                CodeActionKind.Refactor,
                CodeActionKind.RefactorExtract,
                CodeActionKind.RefactorInline,
                CodeActionKind.Source,
                CodeActionKind.SourceOrganizeImports),
            ResolveProvider = true
        };
    }
}
