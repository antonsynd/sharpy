using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Diagnostics;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Provides quick fix code actions for diagnostics (unused imports, unused variables, naming conventions).
/// Extracted from the original SharpyCodeActionHandler.
/// </summary>
internal sealed class DiagnosticQuickFixProvider : ICodeActionProvider
{
    public Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(
        CodeActionProviderContext context,
        CancellationToken cancellationToken)
    {
        var actions = new SCG.List<CodeAction>();

        foreach (var diag in context.Diagnostics)
        {
            var code = diag.Code?.String;
            if (code == null)
                continue;

            switch (code)
            {
                case DiagnosticCodes.Validation.UnusedImport:
                    actions.Add(CreateRemoveImportAction(context.DocumentUri, diag, context.SourceText));
                    break;

                case DiagnosticCodes.Validation.UnusedVariable:
                    actions.Add(CreatePrefixUnderscoreAction(context.DocumentUri, diag));
                    break;

                case DiagnosticCodes.Validation.NamingConventionWarning:
                    {
                        var renameAction = CreateNamingFixAction(context.DocumentUri, diag);
                        if (renameAction != null)
                            actions.Add(renameAction);
                        break;
                    }
            }
        }

        return Task.FromResult<IReadOnlyList<CodeAction>>(actions);
    }

    private static CodeAction CreateRemoveImportAction(
        OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri uri,
        Diagnostic diag,
        string? documentText)
    {
        var range = diag.Range;

        LspRange deleteRange;
        if (documentText != null)
        {
            var lines = documentText.Split('\n');
            var lineIndex = range.Start.Line;
            if (lineIndex >= 0 && lineIndex < lines.Length)
            {
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
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [uri] = [new TextEdit { Range = deleteRange, NewText = "" }]
            }
        };

        return new CodeAction
        {
            Title = "Remove unused import",
            Kind = CodeActionKind.QuickFix,
            Diagnostics = new Container<Diagnostic>(diag),
            Edit = edit
        };
    }

    private static CodeAction CreatePrefixUnderscoreAction(
        OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri uri,
        Diagnostic diag)
    {
        var range = diag.Range;

        var edit = new WorkspaceEdit
        {
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [uri] = [new TextEdit { Range = new LspRange(range.Start, range.Start), NewText = "_" }]
            }
        };

        return new CodeAction
        {
            Title = "Prefix with '_' to mark as intentionally unused",
            Kind = CodeActionKind.QuickFix,
            Diagnostics = new Container<Diagnostic>(diag),
            Edit = edit
        };
    }

    private static CodeAction? CreateNamingFixAction(
        OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri uri,
        Diagnostic diag)
    {
        var suggestedName = ExtractSuggestedNameFromData(diag.Data);

        if (string.IsNullOrEmpty(suggestedName))
            return null;

        var edit = new WorkspaceEdit
        {
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [uri] = [new TextEdit { Range = diag.Range, NewText = suggestedName }]
            }
        };

        return new CodeAction
        {
            Title = $"Rename to '{suggestedName}'",
            Kind = CodeActionKind.QuickFix,
            Diagnostics = new Container<Diagnostic>(diag),
            Edit = edit
        };
    }

    private static string? ExtractSuggestedNameFromData(JToken? data)
    {
        if (data is JObject obj && obj.TryGetValue("suggestedName", out var token))
        {
            var value = token.Value<string>();
            if (!string.IsNullOrEmpty(value))
                return value;
        }
        return null;
    }
}
