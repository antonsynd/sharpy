using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Shared helper for converting a <see cref="Symbol"/>'s declaration location
/// to an LSP <see cref="Location"/>.
/// </summary>
internal static class SymbolLocationHelper
{
    /// <summary>
    /// Creates an LSP <see cref="Location"/> from a symbol's declaration metadata.
    /// Returns null if the symbol has no declaration span.
    /// </summary>
    public static Location? GetSymbolLocation(Symbol symbol, string fallbackUri)
    {
        if (symbol.DeclarationSpan == null)
            return null;

        var filePath = symbol.DeclaringFilePath ?? fallbackUri;
        var uri = filePath.StartsWith("file://", StringComparison.Ordinal)
            ? DocumentUri.From(filePath)
            : DocumentUri.FromFileSystemPath(filePath);

        var startLine = System.Math.Max(0, (symbol.DeclarationLine ?? 1) - 1);
        var startCol = System.Math.Max(0, (symbol.DeclarationColumn ?? 1) - 1);
        var endCol = startCol + symbol.Name.Length;

        return new Location
        {
            Uri = uri,
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(startLine, startCol),
                new Position(startLine, endCol))
        };
    }
}
