using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/documentHighlight requests.
/// Highlights all occurrences of the symbol under cursor within the same document.
/// </summary>
internal sealed class SharpyDocumentHighlightHandler : DocumentHighlightHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharpyDocumentHighlightHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<DocumentHighlightContainer?> Handle(
        DocumentHighlightParams request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var node = _api.FindNodeAtPosition(analysis.Ast, line, col);

        if (node == null)
            return null;

        var symbol = DeclarationCursorResolver.Resolve(node, analysis.SemanticQuery, line, col, logger: null);

        if (symbol == null)
            return null;

        var results = new List<DocumentHighlight>();
        var seen = new RangeDedupe();
        var bindings = DeclarationCursorResolver.BindingSet(symbol, analysis.SemanticQuery);

        foreach (var binding in bindings)
        {
            if (binding.EffectiveNameLine != null)
            {
                var declLine = System.Math.Max(0, (binding.EffectiveNameLine ?? 1) - 1);
                var declCol = System.Math.Max(0, (binding.EffectiveNameColumn ?? 1) - 1);
                var declEnd = declCol + SymbolExtents.NameExtentLength(binding);

                var declRange = new LspRange(
                    new Position(declLine, declCol),
                    new Position(declLine, declEnd));

                if (seen.IsFirst(declRange))
                {
                    results.Add(new DocumentHighlight
                    {
                        Range = declRange,
                        Kind = DocumentHighlightKind.Write
                    });
                }
            }

            var references = analysis.SemanticQuery.GetReferences(binding);
            foreach (var refLoc in references)
            {
                if (refLoc.FilePath != null && !refLoc.FilePath.Equals(uri, StringComparison.Ordinal)
                    && !uri.EndsWith(refLoc.FilePath, StringComparison.Ordinal))
                    continue;

                var refLine = System.Math.Max(0, refLoc.Line - 1);
                var refCol = System.Math.Max(0, refLoc.Column - 1);
                var refEnd = refCol + SymbolExtents.ReferenceExtentLength(refLoc, binding.Name);

                var refRange = new LspRange(
                    new Position(refLine, refCol),
                    new Position(refLine, refEnd));

                if (!seen.IsFirst(refRange))
                    continue;

                results.Add(new DocumentHighlight
                {
                    Range = refRange,
                    Kind = DocumentHighlightKind.Read
                });
            }
        }

        return results.Any()
            ? new DocumentHighlightContainer(results)
            : null;
    }

    protected override DocumentHighlightRegistrationOptions CreateRegistrationOptions(
        DocumentHighlightCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DocumentHighlightRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
