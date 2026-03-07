using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using Sharpy.Compiler.Semantic;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using LspSymbolKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles workspace/symbol requests.
/// Searches all open documents for symbols matching the query string.
/// </summary>
internal sealed class SharplyWorkspaceSymbolHandler : WorkspaceSymbolsHandlerBase
{
    private readonly SharplyWorkspace _workspace;

    public SharplyWorkspaceSymbolHandler(SharplyWorkspace workspace)
    {
        _workspace = workspace;
    }

    public override async Task<Container<WorkspaceSymbol>?> Handle(
        WorkspaceSymbolParams request,
        CancellationToken ct)
    {
        var query = request.Query ?? "";
        var results = new List<WorkspaceSymbol>();

        foreach (var uri in _workspace.GetAllDocumentUris())
        {
            var analysis = await _workspace.GetAnalysisAsync(uri, ct).ConfigureAwait(false);
            if (analysis?.SymbolTable == null)
                continue;

            foreach (var symbol in analysis.SymbolTable.GlobalScope.GetAllSymbols())
            {
                // Skip builtins (no declaration location)
                if (symbol.DeclarationLine == null)
                    continue;

                // Case-insensitive substring match
                if (query.Length > 0 &&
                    symbol.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var kind = MapSymbolKind(symbol);
                var line = System.Math.Max(0, (symbol.DeclarationLine ?? 1) - 1);
                var col = System.Math.Max(0, (symbol.DeclarationColumn ?? 1) - 1);
                var endCol = col + symbol.Name.Length;

                var symbolUri = symbol.DeclaringFilePath != null
                    && symbol.DeclaringFilePath.StartsWith("file://", StringComparison.Ordinal)
                    ? DocumentUri.From(symbol.DeclaringFilePath)
                    : symbol.DeclaringFilePath != null
                        ? DocumentUri.FromFileSystemPath(symbol.DeclaringFilePath)
                        : DocumentUri.From(uri);

                results.Add(new WorkspaceSymbol
                {
                    Name = symbol.Name,
                    Kind = kind,
                    Location = new Location
                    {
                        Uri = symbolUri,
                        Range = new LspRange(
                            new Position(line, col),
                            new Position(line, endCol))
                    }
                });
            }
        }

        return new Container<WorkspaceSymbol>(results);
    }

    private static LspSymbolKind MapSymbolKind(Symbol symbol)
    {
        return symbol switch
        {
            TypeSymbol ts => ts.TypeKind switch
            {
                TypeKind.Class => LspSymbolKind.Class,
                TypeKind.Struct => LspSymbolKind.Struct,
                TypeKind.Interface => LspSymbolKind.Interface,
                TypeKind.Enum => LspSymbolKind.Enum,
                _ => LspSymbolKind.Class
            },
            FunctionSymbol => LspSymbolKind.Function,
            VariableSymbol v => v.IsConstant ? LspSymbolKind.Constant : LspSymbolKind.Variable,
            ModuleSymbol => LspSymbolKind.Module,
            TypeAliasSymbol => LspSymbolKind.TypeParameter,
            _ => LspSymbolKind.Variable
        };
    }

    protected override WorkspaceSymbolRegistrationOptions CreateRegistrationOptions(
        WorkspaceSymbolCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new WorkspaceSymbolRegistrationOptions();
    }
}
