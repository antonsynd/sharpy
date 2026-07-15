using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Diagnostics;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Provides quick fix code actions for diagnostics (unused imports, unused variables,
/// naming conventions, and undefined-identifier "did you mean?" renames).
/// Extracted from the original SharpyCodeActionHandler.
/// </summary>
/// <remarks>
/// Fixes are registered in <see cref="FixFactories"/>, a diagnostic-code → fix-factory map,
/// so pairing a new diagnostic with a fix is a single registry entry rather than a switch
/// arm. A factory returns null when it cannot produce a fix for the specific diagnostic
/// (e.g. no <c>suggestedName</c> in the diagnostic data).
/// </remarks>
internal sealed class DiagnosticQuickFixProvider : ICodeActionProvider
{
    /// <summary>
    /// Maps a diagnostic code to the factory that builds its quick fix. Factories may return
    /// null to signal "no applicable fix" for a particular diagnostic instance.
    /// </summary>
    private static readonly SCG.IReadOnlyDictionary<string, Func<CodeActionProviderContext, Diagnostic, CodeAction?>> FixFactories =
        new SCG.Dictionary<string, Func<CodeActionProviderContext, Diagnostic, CodeAction?>>(StringComparer.Ordinal)
        {
            [DiagnosticCodes.Validation.UnusedImport] =
                static (ctx, diag) => CreateRemoveImportAction(ctx.DocumentUri, diag, ctx.SourceText),
            [DiagnosticCodes.Validation.UnusedVariable] =
                static (ctx, diag) => CreatePrefixUnderscoreAction(ctx.DocumentUri, diag),
            [DiagnosticCodes.Validation.NamingConventionWarning] =
                static (ctx, diag) => CreateRenameToSuggestionAction(ctx.DocumentUri, diag),
            [DiagnosticCodes.Semantic.UndefinedVariable] =
                static (ctx, diag) => CreateRenameToSuggestionAction(ctx.DocumentUri, diag),
        };

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

            if (FixFactories.TryGetValue(code, out var factory))
            {
                var action = factory(context, diag);
                if (action != null)
                    actions.Add(action);
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

    /// <summary>
    /// Builds a "Rename to '&lt;suggestedName&gt;'" fix that replaces the diagnostic's range with
    /// the <c>suggestedName</c> carried in the diagnostic data. Shared by naming-convention
    /// warnings and undefined-identifier "did you mean?" errors — in both the diagnostic range
    /// is exactly the identifier to rename. Returns null when no suggestion is present.
    /// </summary>
    private static CodeAction? CreateRenameToSuggestionAction(
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
