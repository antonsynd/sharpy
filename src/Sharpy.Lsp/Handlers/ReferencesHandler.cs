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
internal sealed class SharplyReferencesHandler : ReferencesHandlerBase
{
    private readonly SharplyWorkspace _workspace;
    private readonly CompilerApi _api;

    public SharplyReferencesHandler(SharplyWorkspace workspace, CompilerApi api)
    {
        _workspace = workspace;
        _api = api;
    }

    public override async Task<LocationContainer?> Handle(ReferenceParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _workspace.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

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
            var declLine = System.Math.Max(0, (symbol.DeclarationLine ?? 1) - 1);
            var declCol = System.Math.Max(0, (symbol.DeclarationColumn ?? 1) - 1);
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

        // Get all references
        var references = analysis.SemanticQuery.GetReferences(symbol);
        foreach (var refLoc in references)
        {
            var refLine = System.Math.Max(0, refLoc.Line - 1);
            var refCol = System.Math.Max(0, refLoc.Column - 1);
            var refEnd = refCol + symbol.Name.Length;

            var refFilePath = refLoc.FilePath ?? uri;
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

        return new LocationContainer(locations);
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
