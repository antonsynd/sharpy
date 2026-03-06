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
    /// Falls back to a zero-width range at the start position if span info is absent.
    /// </summary>
    public static LspRange DiagnosticToRange(CompilerDiagnostic diagnostic, SourceText? sourceText)
    {
        if (diagnostic.Span.HasValue && sourceText != null)
        {
            return ToLspRange(diagnostic.Span.Value, sourceText);
        }

        // Fall back to line/column with zero-width range
        var line = System.Math.Max(0, (diagnostic.Line ?? 1) - 1);
        var col = System.Math.Max(0, (diagnostic.Column ?? 1) - 1);
        var pos = new Position(line, col);
        return new LspRange(pos, pos);
    }
}
