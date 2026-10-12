using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Diagnostics;
using TokenType = Sharpy.Compiler.Lexer.TokenType;
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
                static (ctx, diag) => CreatePrefixUnderscoreAction(ctx, diag),
            [DiagnosticCodes.Validation.NamingConventionWarning] =
                static (ctx, diag) => CreateRenameToSuggestionAction(ctx, diag),
            [DiagnosticCodes.Semantic.UndefinedVariable] =
                static (ctx, diag) => CreateRenameToSuggestionAction(ctx, diag),
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
            return new TextEdit { Range = new LspRange(source.PositionOf(start), source.PositionOf(end)), NewText = "" };
        }

        return null;
    }

    /// <summary>
    /// The extent of the NAME a naming or unused-variable diagnostic is about. Those diagnostics are reported at
    /// their statement's start (`let x = 1`, `except E as e:`), so the diagnostic range is not the name's: the
    /// name is the first quoted word of the message, found as an identifier token from the diagnostic's start
    /// to the end of its logical line. Falls back to the diagnostic's range when the document or the name is
    /// not found (P47, #2232: `_let x`, `_except …`).
    /// </summary>
    private static LspRange NameRange(CodeActionProviderContext context, Diagnostic diag) =>
        NameToken(context, diag) is { } token
            ? token.IsBacktickEscaped // the name inside the backticks; the escape stays
                ? new LspRange(SourceLines.Of(context.SourceText!).PositionOf(token.Position + 1),
                    SourceLines.Of(context.SourceText!).PositionOf(token.Position + token.Length - 1))
                : new LspRange(SourceLines.Of(context.SourceText!).PositionOf(token.Position),
                    SourceLines.Of(context.SourceText!).PositionOf(token.Position + token.Length))
            : diag.Range;

    /// <summary>
    /// The identifier token <see cref="NameRange"/> describes: the first identifier spelled as the message's
    /// first quoted word, searched from the diagnostic's start through its end (a decorated field's range starts
    /// at the decorator) or, for a zero-width range, through the end of its logical line. Null when not found.
    /// </summary>
    private static Sharpy.Compiler.Lexer.Token? NameToken(CodeActionProviderContext context, Diagnostic diag)
    {
        if (context.SourceText is null)
            return null;

        var message = diag.Message ?? "";
        var open = message.IndexOf('\'', StringComparison.Ordinal);
        var close = open < 0 ? -1 : message.IndexOf("'", open + 1, StringComparison.Ordinal);
        if (close <= open + 1)
            return null;
        var name = message[(open + 1)..close];

        var source = SourceLines.Of(context.SourceText);
        var startLine = diag.Range.Start.Line + 1;
        var endLine = diag.Range.End.Line + 1;
        if (!source.Contains(startLine) || !source.Contains(endLine))
            return null;
        var from = source.StartOffset(startLine) + diag.Range.Start.Character;
        var to = source.StartOffset(endLine) + diag.Range.End.Character;
        foreach (var token in source.Tokens)
        {
            if (token.Position < from)
                continue;
            if (to > from ? token.Position >= to : token.Type is TokenType.Newline)
                break;
            if (token.Type == TokenType.Eof)
                break;
            if (token.Type == TokenType.Identifier && token.Value == name)
                return token;
        }

        return null;
    }

    private static CodeAction CreatePrefixUnderscoreAction(CodeActionProviderContext context, Diagnostic diag)
    {
        var uri = context.DocumentUri;

        // The underscore goes before the name — inside the backticks of an escaped one (`_class`).
        var start = NameToken(context, diag) is { } token
            ? SourceLines.Of(context.SourceText!).PositionOf(token.Position + (token.IsBacktickEscaped ? 1 : 0))
            : diag.Range.Start;

        var edit = new WorkspaceEdit
        {
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [uri] = [new TextEdit { Range = new LspRange(start, start), NewText = "_" }]
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
    /// warnings and undefined-identifier "did you mean?" errors. The edit covers the NAME's extent
    /// (<see cref="NameRange"/>), not a statement-start diagnostic range. Returns null when no
    /// suggestion is present.
    /// </summary>
    private static CodeAction? CreateRenameToSuggestionAction(CodeActionProviderContext context, Diagnostic diag)
    {
        var uri = context.DocumentUri;
        var suggestedName = ExtractSuggestedNameFromData(diag.Data);

        if (string.IsNullOrEmpty(suggestedName))
            return null;

        var edit = new WorkspaceEdit
        {
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [uri] = [new TextEdit { Range = NameRange(context, diag), NewText = suggestedName }]
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
