using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Services;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

internal sealed class SharpySelectionRangeHandler : SelectionRangeHandlerBase
{
    private readonly LanguageService _languageService;
    private static readonly AstPositionService PositionService = new();

    public SharpySelectionRangeHandler(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public override async Task<Container<SelectionRange>?> Handle(
        SelectionRangeParams request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var parseResult = await _languageService.GetParseResultAsync(uri, ct).ConfigureAwait(false);

        if (parseResult?.Ast == null)
            return null;

        var results = new List<SelectionRange>();

        foreach (var position in request.Positions)
        {
            var (line, col) = PositionConverter.ToCompiler(position);
            var nodes = PositionService.FindAllContainingNodes(parseResult.Ast, line, col);

            if (nodes.Count == 0)
            {
                results.Add(new SelectionRange
                {
                    Range = new LspRange(position, position)
                });
                continue;
            }

            // Build chain: outermost first (no parent), each inner node points to outer as Parent
            var chain = new SelectionRange
            {
                Range = new LspRange(
                    PositionConverter.ToLsp(nodes[0].LineStart, nodes[0].ColumnStart),
                    PositionConverter.ToLsp(nodes[0].LineEnd, nodes[0].ColumnEnd))
            };
            for (var i = 1; i < nodes.Count; i++)
            {
                var node = nodes[i];
                chain = new SelectionRange
                {
                    Range = new LspRange(
                        PositionConverter.ToLsp(node.LineStart, node.ColumnStart),
                        PositionConverter.ToLsp(node.LineEnd, node.ColumnEnd)),
                    Parent = chain
                };
            }

            results.Add(chain);
        }

        return new Container<SelectionRange>(results);
    }

    protected override SelectionRangeRegistrationOptions CreateRegistrationOptions(
        SelectionRangeCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new SelectionRangeRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
