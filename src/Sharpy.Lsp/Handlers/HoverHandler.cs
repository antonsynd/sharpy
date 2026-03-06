using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/hover requests.
/// Returns type information and symbol documentation for the node at the cursor position.
/// </summary>
internal sealed class SharplyHoverHandler : HoverHandlerBase
{
    private readonly SharplyWorkspace _workspace;
    private readonly CompilerApi _api;

    public SharplyHoverHandler(SharplyWorkspace workspace, CompilerApi api)
    {
        _workspace = workspace;
        _api = api;
    }

    public override async Task<Hover?> Handle(HoverParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _workspace.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var node = _api.FindNodeAtPosition(analysis.Ast, line, col);

        if (node == null)
            return null;

        var hoverText = GetHoverText(node, analysis);
        if (hoverText == null)
            return null;

        return new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(
                new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = $"```sharpy\n{hoverText}\n```"
                }
            )
        };
    }

    private static string? GetHoverText(Node node, SemanticResult analysis)
    {
        var query = analysis.SemanticQuery!;

        switch (node)
        {
            case Identifier id:
            {
                var symbol = query.GetIdentifierSymbol(id);
                if (symbol != null)
                    return SymbolFormatter.FormatSymbol(symbol);

                // Fall back to type info
                var type = query.GetEffectiveType(id);
                if (type != null)
                    return $"{id.Name}: {SymbolFormatter.FormatTypeInfo(type)}";

                return null;
            }

            case FunctionCall call:
            {
                var target = query.GetCallTarget(call);
                if (target != null)
                    return SymbolFormatter.FormatSymbol(target);
                break;
            }

            case Expression expr:
            {
                var type = query.GetEffectiveType(expr);
                if (type != null)
                    return SymbolFormatter.FormatTypeInfo(type);
                break;
            }
        }

        return null;
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
