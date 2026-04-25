using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Services;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

internal sealed class SharpyLinkedEditingRangeHandler : LinkedEditingRangeHandlerBase
{
    private readonly LanguageService _languageService;
    private static readonly AstPositionService PositionService = new();

    public SharpyLinkedEditingRangeHandler(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public override async Task<LinkedEditingRanges> Handle(
        LinkedEditingRangeParams request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var parseResult = await _languageService.GetParseResultAsync(uri, ct).ConfigureAwait(false);

        if (parseResult?.Ast == null)
            return null!;

        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var nodes = PositionService.FindAllContainingNodes(parseResult.Ast, line, col);

        if (nodes.Count == 0)
            return null!;

        var innermost = nodes[^1];
        if (innermost is not Identifier cursorId)
            return null!;

        // Walk up to find enclosing FStringLiteral or TStringLiteral
        IReadOnlyList<FStringPart>? parts = null;
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            if (nodes[i] is FStringLiteral fstr)
            {
                parts = fstr.Parts;
                break;
            }
            if (nodes[i] is TStringLiteral tstr)
            {
                parts = tstr.Parts;
                break;
            }
        }

        if (parts == null)
            return null!;

        // Collect all Identifier expressions with the same name
        var matchingRanges = new List<LspRange>();
        foreach (var part in parts)
        {
            if (part.Expression is Identifier id && id.Name == cursorId.Name)
            {
                matchingRanges.Add(new LspRange(
                    PositionConverter.ToLsp(id.LineStart, id.ColumnStart),
                    PositionConverter.ToLsp(id.LineEnd, id.ColumnEnd)));
            }
        }

        if (matchingRanges.Count < 2)
            return null!;

        return new LinkedEditingRanges
        {
            Ranges = new Container<LspRange>(matchingRanges),
            WordPattern = "[a-zA-Z_][a-zA-Z0-9_]*"
        };
    }

    protected override LinkedEditingRangeRegistrationOptions CreateRegistrationOptions(
        LinkedEditingRangeClientCapabilities capability,
        ClientCapabilities clientCapabilities)
    {
        return new LinkedEditingRangeRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
