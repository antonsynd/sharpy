using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/codeAction requests.
/// Provides quick fixes for common diagnostics (unused imports, unused variables, naming conventions).
/// </summary>
internal sealed class SharplyCodeActionHandler : CodeActionHandlerBase
{
    private readonly SharplyWorkspace _workspace;

    public SharplyCodeActionHandler(SharplyWorkspace workspace)
    {
        _workspace = workspace;
    }

    public override async Task<CommandOrCodeActionContainer?> Handle(
        CodeActionParams request,
        CancellationToken ct)
    {
        var actions = new List<CommandOrCodeAction>();
        var uri = request.TextDocument.Uri;

        // Get document text for computing edits
        var doc = _workspace.GetDocument(uri.ToString());
        var text = doc?.Text;

        foreach (var diag in request.Context.Diagnostics)
        {
            var code = diag.Code?.String;
            if (code == null)
                continue;

            switch (code)
            {
                case "SPY0452": // Unused import
                    actions.Add(CreateRemoveImportAction(uri, diag, text));
                    break;

                case "SPY0451": // Unused variable
                    actions.Add(CreatePrefixUnderscoreAction(uri, diag));
                    break;

                case "SPY0453": // Naming convention warning
                    {
                        var renameAction = CreateNamingFixAction(uri, diag);
                        if (renameAction != null)
                            actions.Add(renameAction);
                        break;
                    }
            }
        }

        return new CommandOrCodeActionContainer(actions);
    }

    private static CommandOrCodeAction CreateRemoveImportAction(
        DocumentUri uri,
        Diagnostic diag,
        string? documentText)
    {
        // Remove the entire line containing the unused import
        var range = diag.Range;

        // Extend range to cover the full line including the newline
        LspRange deleteRange;
        if (documentText != null)
        {
            var lines = documentText.Split('\n');
            var lineIndex = range.Start.Line;
            if (lineIndex >= 0 && lineIndex < lines.Length)
            {
                // Delete from start of line to start of next line (including newline)
                deleteRange = new LspRange(
                    new Position(lineIndex, 0),
                    new Position(lineIndex + 1, 0));
            }
            else
            {
                deleteRange = range;
            }
        }
        else
        {
            deleteRange = range;
        }

        var edit = new WorkspaceEdit
        {
            Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
            {
                [uri] = [new TextEdit { Range = deleteRange, NewText = "" }]
            }
        };

        return new CommandOrCodeAction(new CodeAction
        {
            Title = "Remove unused import",
            Kind = CodeActionKind.QuickFix,
            Diagnostics = new Container<Diagnostic>(diag),
            Edit = edit
        });
    }

    private static CommandOrCodeAction CreatePrefixUnderscoreAction(
        DocumentUri uri,
        Diagnostic diag)
    {
        // Extract the variable name from the diagnostic range and prefix with _
        var range = diag.Range;

        // We prefix the variable name at the diagnostic range with _
        var edit = new WorkspaceEdit
        {
            Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
            {
                [uri] = [new TextEdit { Range = new LspRange(range.Start, range.Start), NewText = "_" }]
            }
        };

        return new CommandOrCodeAction(new CodeAction
        {
            Title = "Prefix with '_' to mark as intentionally unused",
            Kind = CodeActionKind.QuickFix,
            Diagnostics = new Container<Diagnostic>(diag),
            Edit = edit
        });
    }

    private static CommandOrCodeAction? CreateNamingFixAction(
        DocumentUri uri,
        Diagnostic diag)
    {
        // Try to extract a suggested name from the diagnostic message.
        // Typical message: "Variable 'myVar' should use snake_case naming convention; consider 'my_var'"
        var message = diag.Message ?? "";
        var considerIndex = message.IndexOf("consider '", StringComparison.Ordinal);
        if (considerIndex < 0)
            return null;

        var nameStart = considerIndex + "consider '".Length;
        var nameEnd = message.IndexOf('\'', nameStart);
        if (nameEnd < 0)
            return null;

        var suggestedName = message[nameStart..nameEnd];
        if (string.IsNullOrEmpty(suggestedName))
            return null;

        var edit = new WorkspaceEdit
        {
            Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
            {
                [uri] = [new TextEdit { Range = diag.Range, NewText = suggestedName }]
            }
        };

        return new CommandOrCodeAction(new CodeAction
        {
            Title = $"Rename to '{suggestedName}'",
            Kind = CodeActionKind.QuickFix,
            Diagnostics = new Container<Diagnostic>(diag),
            Edit = edit
        });
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
            CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.QuickFix)
        };
    }
}
