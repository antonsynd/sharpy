using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/references requests.
/// Returns all locations where a symbol is referenced.
/// </summary>
internal sealed class SharpyReferencesHandler : ReferencesHandlerBase
{
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharpyReferencesHandler(SharpyWorkspace workspace, LanguageService languageService, CompilerApi api)
    {
        _workspace = workspace;
        _languageService = languageService;
        _api = api;
    }

    public override async Task<LocationContainer?> Handle(ReferenceParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var node = _api.FindNodeAtPosition(analysis.Ast, line, col);

        if (node == null)
            return null;

        // Resolve the symbol
        var symbol = node switch
        {
            Identifier id => analysis.SemanticQuery.GetIdentifierSymbol(id),
            FunctionCall call => analysis.SemanticQuery.GetCallTarget(call),
            _ => null
        };

        if (symbol == null)
            return null;

        var locations = new System.Collections.Generic.List<Location>();

        // Include declaration if requested
        if (request.Context.IncludeDeclaration && symbol.DeclarationSpan != null)
        {
            var declLine = System.Math.Max(0, (symbol.EffectiveNameLine ?? 1) - 1);
            var declCol = System.Math.Max(0, (symbol.EffectiveNameColumn ?? 1) - 1);
            var declEnd = declCol + symbol.Name.Length;

            var declFilePath = symbol.DeclaringFilePath ?? uri;
            var declUri = declFilePath.StartsWith("file://", StringComparison.Ordinal)
                ? DocumentUri.From(declFilePath)
                : DocumentUri.FromFileSystemPath(declFilePath);

            locations.Add(new Location
            {
                Uri = declUri,
                Range = new LspRange(
                    new Position(declLine, declCol),
                    new Position(declLine, declEnd))
            });
        }

        // Get references from current file
        var references = analysis.SemanticQuery.GetReferences(symbol);
        AddReferenceLocations(locations, references, symbol.Name, uri);

        // Collect references from other workspace files
        var allUris = _workspace.GetAllDocumentUris();
        var otherUris = allUris.Where(u => !string.Equals(u, uri, StringComparison.Ordinal)).ToList();

        if (otherUris.Count > 0)
        {
            var reporter = _languageService.ProgressReporter;
            using var progress = reporter != null
                ? await reporter.BeginAsync("Finding references", ct).ConfigureAwait(false)
                : ProgressScope.NoOp;

            for (var i = 0; i < otherUris.Count; i++)
            {
                var otherUri = otherUris[i];
                progress.Report(
                    $"Searching {System.IO.Path.GetFileName(UriToFilePath(otherUri) ?? otherUri)}",
                    (i + 1) * 100 / otherUris.Count);

                try
                {
                    var otherAnalysis = await _languageService.GetAnalysisAsync(otherUri, ct).ConfigureAwait(false);
                    if (otherAnalysis?.SemanticQuery == null)
                        continue;

                    var crossRefs = otherAnalysis.SemanticQuery.FindReferencesBySymbolIdentity(
                        symbol.Name, symbol.DeclaringFilePath);
                    AddReferenceLocations(locations, crossRefs, symbol.Name, otherUri);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Skip files that fail to analyze
                }
            }
        }

        return new LocationContainer(locations);
    }

    private static void AddReferenceLocations(
        System.Collections.Generic.List<Location> locations,
        IReadOnlyList<Compiler.Semantic.SymbolReference> references,
        string symbolName,
        string fallbackUri)
    {
        foreach (var refLoc in references)
        {
            var refLine = System.Math.Max(0, refLoc.Line - 1);
            var refCol = System.Math.Max(0, refLoc.Column - 1);
            var refEnd = refCol + symbolName.Length;

            var refFilePath = refLoc.FilePath ?? fallbackUri;
            var refUri = refFilePath.StartsWith("file://", StringComparison.Ordinal)
                ? DocumentUri.From(refFilePath)
                : DocumentUri.FromFileSystemPath(refFilePath);

            locations.Add(new Location
            {
                Uri = refUri,
                Range = new LspRange(
                    new Position(refLine, refCol),
                    new Position(refLine, refEnd))
            });
        }
    }

    private static string? UriToFilePath(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile)
            return parsed.LocalPath;

        return System.IO.Path.IsPathRooted(uri) ? uri : null;
    }

    protected override ReferenceRegistrationOptions CreateRegistrationOptions(
        ReferenceCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new ReferenceRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
