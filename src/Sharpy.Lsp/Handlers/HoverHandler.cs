using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/hover requests.
/// Delegates to <see cref="HoverService"/> for hover resolution logic.
/// </summary>
internal sealed class SharpyHoverHandler : HoverHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly HoverService _hoverService;

    public SharpyHoverHandler(LanguageService languageService, HoverService hoverService)
    {
        _languageService = languageService;
        _hoverService = hoverService;
    }

    public override async Task<Hover?> Handle(HoverParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var result = _hoverService.GetHoverResult(analysis, line, col);
        if (result == null)
            return null;

        var node = result.Node;
        return new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(
                new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = result.Markdown
                }
            ),
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                PositionConverter.ToLsp(node.LineStart, node.ColumnStart),
                PositionConverter.ToLsp(node.LineEnd, node.ColumnEnd)
            )
        };
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(
        HoverCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new HoverRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
