using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Text;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp;

/// <summary>
/// Converts between LSP 0-based positions and compiler 1-based positions.
/// </summary>
internal static class PositionConverter
{
    /// <summary>
    /// Converts LSP 0-based line/column to compiler 1-based line/column.
    /// </summary>
    public static (int Line, int Column) ToCompiler(Position lspPosition)
    {
        return (lspPosition.Line + 1, lspPosition.Character + 1);
    }

    /// <summary>
    /// Converts compiler 1-based line/column to LSP 0-based Position.
    /// </summary>
    public static Position ToLsp(int compilerLine, int compilerColumn)
    {
        return new Position(
            System.Math.Max(0, compilerLine - 1),
            System.Math.Max(0, compilerColumn - 1)
        );
    }

    /// <summary>
    /// Converts a compiler TextSpan to an LSP Range using SourceText for line/column lookup.
    /// </summary>
    public static LspRange ToLspRange(TextSpan span, SourceText sourceText)
    {
        var (startLine, startCol) = sourceText.GetLineAndColumn(span.Start);
        var endPosition = System.Math.Min(span.Start + span.Length, sourceText.Length);
        var (endLine, endCol) = sourceText.GetLineAndColumn(endPosition);

        return new LspRange(
            ToLsp(startLine, startCol),
            ToLsp(endLine, endCol)
        );
    }

    /// <summary>
    /// Creates an LSP Range from a CompilerDiagnostic.
    /// Falls back to a zero-width range at the diagnostic's line/column when span info is absent,
    /// when no text is supplied, or when the span does not start inside the supplied text.
    /// </summary>
    /// <remarks>
    /// A span that starts outside <paramref name="sourceText"/> was recorded against a different
    /// text — a sibling file's diagnostic handed the open buffer's text, or an analysis the buffer
    /// has since outrun (#2242). Clamping it would draw the squiggle at end-of-file, a position the
    /// diagnostic never named; the recorded line/column is text-independent, so it is the fallback.
    /// Never throws for such a span: <see cref="SourceText.GetLineAndColumn"/> does, and one throw
    /// used to abort the whole publication it was part of.
    /// </remarks>
    public static LspRange DiagnosticToRange(CompilerDiagnostic diagnostic, SourceText? sourceText)
    {
        if (diagnostic.Span is { } span && sourceText != null && StartsInside(span, sourceText))
        {
            return ToLspRange(span, sourceText);
        }

        // Fall back to line/column with zero-width range
        var line = System.Math.Max(0, (diagnostic.Line ?? 1) - 1);
        var col = System.Math.Max(0, (diagnostic.Column ?? 1) - 1);
        var pos = new Position(line, col);
        return new LspRange(pos, pos);
    }

    /// <summary>
    /// True when <paramref name="span"/> starts at a position <paramref name="sourceText"/> can
    /// resolve (end-of-text included). The end needs no check: <see cref="ToLspRange"/> clamps it.
    /// </summary>
    private static bool StartsInside(TextSpan span, SourceText sourceText)
        => span.Start >= 0 && span.Start <= sourceText.Length && span.Length >= 0;
}
