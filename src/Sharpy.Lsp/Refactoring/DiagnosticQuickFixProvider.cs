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
                static (ctx, diag) => CreateRemoveImportAction(ctx, diag),
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

    private static CodeAction CreateRemoveImportAction(CodeActionProviderContext context, Diagnostic diag)
    {
        var uri = context.DocumentUri;
        var documentText = context.SourceText;
        var range = diag.Range;

        TextEdit deleteEdit;
        if (documentText != null)
        {
            var source = SourceLines.Of(documentText);
            var lineIndex = range.Start.Line;
            if (RemoveImportEdit(context, source, diag) is { } importEdit)
            {
                deleteEdit = importEdit;
            }
            else if (source.Contains(lineIndex + 1))
            {
                deleteEdit = source.WholeLineEdit(lineIndex + 1, lineIndex + 1, Array.Empty<string>());
            }
            else
            {
                deleteEdit = new TextEdit { Range = range, NewText = "" };
            }
        }
        else
        {
            deleteEdit = new TextEdit { Range = range, NewText = "" };
        }

        var edit = new WorkspaceEdit
        {
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [uri] = [deleteEdit]
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

    /// <summary>
    /// The deletion for an SPY0452 diagnostic, which names ONE imported name (an alias's position): when every
    /// name of the import statement holding it is unused, the statement's whole lines through its TRUE last
    /// line (a parenthesized import's closing line included, D2), its trailing comment with it (D5); when a
    /// name of that statement is still used, only this name — its own line inside a parenthesized import, else
    /// its text and one separating comma. Null when no import statement holds the diagnostic's position.
    /// </summary>
    private static TextEdit? RemoveImportEdit(CodeActionProviderContext context, SourceLines source, Diagnostic diag)
    {
        if (context.Analysis?.Ast is not { } ast)
            return null;

        var line = diag.Range.Start.Line + 1;
        var column = diag.Range.Start.Character + 1;
        foreach (var stmt in ast.Body)
        {
            // A decorated (suppressing) import keeps today's line deletion: its decorator lines are not this
            // fix's (ImportNames.Of sees no names through the decorator wrapper).
            var names = ImportNames.Of(stmt);
            if (names.IsEmpty || line < stmt.LineStart || line > source.EndLine(stmt))
                continue;

            var index = -1;
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i].LineStart == line && names[i].ColumnStart == column)
                    index = i;
            }

            if (index < 0)
                return null;

            var unused = new UnusedImportNames(context, names);
            if (names.Length == 1 || names.All(unused.Contains))
                return source.WholeLineEdit(stmt.LineStart, source.EndLine(stmt), Array.Empty<string>());

            var (start, end) = ImportNames.RemovalSpans(source, names, new SCG.HashSet<int> { index }).Single();
            return new TextEdit { Range = new LspRange(PositionOf(source, start), PositionOf(source, end)), NewText = "" };
        }

        return null;
    }

    /// <summary>The LSP position of a 0-based document offset.</summary>
    private static Position PositionOf(SourceLines source, int offset)
    {
        var line = source.LineOfOffset(offset);
        return new Position(line - 1, offset - source.StartOffset(line));
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
