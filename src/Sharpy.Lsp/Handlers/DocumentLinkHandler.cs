using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/documentLink requests.
/// Produces clickable links on import statements that navigate to the
/// imported module's source file. Standard-library and .NET module imports
/// produce no link because they are not user-navigable source files.
/// </summary>
internal sealed class SharpyDocumentLinkHandler : DocumentLinkHandlerBase
{
    private readonly LanguageService _languageService;

    public SharpyDocumentLinkHandler(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public override async Task<DocumentLinkContainer?> Handle(
        DocumentLinkParams request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SymbolTable == null)
            return null;

        var links = new List<DocumentLink>();
        CollectLinks(analysis.Ast.Body, analysis.SymbolTable, links);

        return new DocumentLinkContainer(links);
    }

    public override Task<DocumentLink> Handle(DocumentLink request, CancellationToken ct)
    {
        // Links are resolved eagerly in the initial response.
        return Task.FromResult(request);
    }

    private static void CollectLinks(
        IEnumerable<Statement> statements,
        SymbolTable symbolTable,
        List<DocumentLink> links)
    {
        foreach (var stmt in statements)
        {
            switch (stmt)
            {
                case ImportStatement import:
                    AddImportLinks(import, symbolTable, links);
                    break;
                case FromImportStatement fromImport:
                    AddFromImportLink(fromImport, symbolTable, links);
                    break;
            }
        }
    }

    private static void AddImportLinks(
        ImportStatement import,
        SymbolTable symbolTable,
        List<DocumentLink> links)
    {
        foreach (var alias in import.Names)
        {
            var filePath = ResolveImportFilePath(alias, symbolTable);
            if (filePath == null)
                continue;

            var range = new LspRange(
                PositionConverter.ToLsp(alias.LineStart, alias.ColumnStart),
                PositionConverter.ToLsp(alias.LineEnd, alias.ColumnEnd));

            links.Add(CreateLink(range, filePath));
        }
    }

    private static void AddFromImportLink(
        FromImportStatement fromImport,
        SymbolTable symbolTable,
        List<DocumentLink> links)
    {
        var filePath = ResolveFromImportFilePath(fromImport, symbolTable);
        if (filePath == null)
            return;

        // Highlight the module portion that follows the leading "from " keyword.
        const int FromKeywordLength = 5; // "from "
        var startColumn = fromImport.ColumnStart + FromKeywordLength;
        var endColumn = startColumn + fromImport.Module.Length;

        var range = new LspRange(
            PositionConverter.ToLsp(fromImport.LineStart, startColumn),
            PositionConverter.ToLsp(fromImport.LineStart, endColumn));

        links.Add(CreateLink(range, filePath));
    }

    /// <summary>
    /// Resolves the source file backing a plain <c>import module</c> alias.
    /// Returns null for .NET / stdlib modules and unresolved imports.
    /// </summary>
    private static string? ResolveImportFilePath(ImportAlias alias, SymbolTable symbolTable)
    {
        var name = alias.AsName ?? alias.Name;
        var parts = name.Split('.');

        var current = LookupModule(parts[0], symbolTable);
        if (current == null)
            return null;

        if (alias.AsName == null)
        {
            for (var i = 1; i < parts.Length; i++)
            {
                if (!current.Exports.TryGetValue(parts[i], out var next)
                    || next is not ModuleSymbol nextModule)
                {
                    return null;
                }

                current = nextModule;
            }
        }

        return ToNavigableSpyPath(current.FilePath, current.IsNetModule);
    }

    /// <summary>
    /// Resolves the source file backing a <c>from module import ...</c> statement.
    /// Tries: module symbol lookup, then imported symbol DeclaringFilePath.
    /// Returns null for stdlib / .NET / unresolved imports.
    /// </summary>
    private static string? ResolveFromImportFilePath(
        FromImportStatement fromImport,
        SymbolTable symbolTable)
    {
        // Try looking up the module directly
        var moduleSymbol = LookupModule(fromImport.Module, symbolTable);
        if (moduleSymbol != null)
        {
            var navigable = ToNavigableSpyPath(moduleSymbol.FilePath, moduleSymbol.IsNetModule);
            if (navigable != null)
                return navigable;
        }

        // Fall back to checking the declaring file of imported symbols
        foreach (var alias in fromImport.Names)
        {
            var symbol = symbolTable.Lookup(alias.AsName ?? alias.Name)
                ?? symbolTable.LookupInModuleScopes(alias.AsName ?? alias.Name);
            var path = symbol?.DeclaringFilePath;
            var navigable = ToNavigableSpyPath(path, isNetModule: false);
            if (navigable != null)
                return navigable;
        }

        return null;
    }

    private static ModuleSymbol? LookupModule(string name, SymbolTable symbolTable)
    {
        return symbolTable.Lookup(name) as ModuleSymbol
            ?? symbolTable.LookupInModuleScopes(name) as ModuleSymbol;
    }

    /// <summary>
    /// Returns the given path only if it points at an existing Sharpy source file
    /// that the user can navigate to; otherwise null.
    /// </summary>
    private static string? ToNavigableSpyPath(string? filePath, bool isNetModule)
    {
        if (isNetModule || string.IsNullOrEmpty(filePath))
            return null;

        if (!filePath.EndsWith(".spy", StringComparison.OrdinalIgnoreCase))
            return null;

        return File.Exists(filePath) ? filePath : null;
    }

    private static DocumentLink CreateLink(LspRange range, string filePath)
    {
        return new DocumentLink
        {
            Range = range,
            Target = DocumentUri.FromFileSystemPath(filePath)
        };
    }

    protected override DocumentLinkRegistrationOptions CreateRegistrationOptions(
        DocumentLinkCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DocumentLinkRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy"),
            ResolveProvider = false
        };
    }
}
