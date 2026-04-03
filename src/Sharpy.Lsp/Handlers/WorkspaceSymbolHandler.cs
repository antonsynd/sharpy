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
/// Searches all open documents and project files for symbols matching the query string.
/// </summary>
internal sealed class SharpyWorkspaceSymbolHandler : WorkspaceSymbolsHandlerBase
{
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;

    public SharpyWorkspaceSymbolHandler(SharpyWorkspace workspace, LanguageService languageService)
    {
        _workspace = workspace;
        _languageService = languageService;
    }

    public override async Task<Container<WorkspaceSymbol>?> Handle(
        WorkspaceSymbolParams request,
        CancellationToken ct)
    {
        var query = request.Query ?? "";
        var results = new List<WorkspaceSymbol>();

        // Collect URIs from both open documents and project files, deduplicated.
        var allUris = new HashSet<string>(StringComparer.Ordinal);
        foreach (var uri in _workspace.GetAllDocumentUris())
            allUris.Add(uri);
        foreach (var uri in _languageService.GetProjectFileUris())
            allUris.Add(uri);

        foreach (var uri in allUris)
        {
            var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);
            if (analysis?.SymbolTable == null)
                continue;

            foreach (var symbol in analysis.SymbolTable.GlobalScope.GetAllSymbols()
                .Concat(analysis.SymbolTable.GetAllModuleScopeSymbols()))
            {
                // Skip builtins (no declaration location)
                if (symbol.DeclarationLine == null)
                    continue;

                // Case-insensitive substring match or camelCase fuzzy match
                if (query.Length > 0 &&
                    symbol.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    !MatchesCamelCase(symbol.Name, query))
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

    /// <summary>
    /// Checks if the query matches the camelCase/PascalCase initials of the symbol name.
    /// For example, "FBT" matches "FileBasedTest" (capitals F, B, T).
    /// </summary>
    internal static bool MatchesCamelCase(string symbolName, string query)
    {
        if (query.Length == 0)
            return true;

        // Extract uppercase letters at word boundaries from the symbol name.
        // Word boundaries: start of string, after '_', or an uppercase letter following a lowercase.
        var qi = 0;
        for (var i = 0; i < symbolName.Length && qi < query.Length; i++)
        {
            var ch = symbolName[i];
            var isWordStart = i == 0
                || (i > 0 && symbolName[i - 1] == '_' && ch != '_')
                || (char.IsUpper(ch) && i > 0 && char.IsLower(symbolName[i - 1]));

            if (isWordStart && char.ToUpperInvariant(ch) == char.ToUpperInvariant(query[qi]))
            {
                qi++;
            }
        }

        return qi == query.Length;
    }

    protected override WorkspaceSymbolRegistrationOptions CreateRegistrationOptions(
        WorkspaceSymbolCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new WorkspaceSymbolRegistrationOptions();
    }
}
