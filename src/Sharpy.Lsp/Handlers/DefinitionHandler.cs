using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/definition requests.
/// Returns the location of the symbol's declaration.
/// </summary>
internal sealed class SharpyDefinitionHandler : DefinitionHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharpyDefinitionHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<LocationOrLocationLinks?> Handle(
        DefinitionParams request,
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

        var symbol = ResolveSymbol(node, analysis, line, col);
        if (symbol == null)
            return null;

        var location = SymbolLocationHelper.GetSymbolLocation(symbol, uri);
        if (location == null)
            return null;

        return new LocationOrLocationLinks(location);
    }

    private Symbol? ResolveSymbol(Node node, SemanticResult analysis, int line, int col)
    {
        var query = analysis.SemanticQuery!;

        return node switch
        {
            Identifier id => query.GetIdentifierSymbol(id),
            FunctionCall call => query.GetCallTarget(call),
            MemberAccess ma => MemberAccessSymbolResolver.Resolve(query, ma,
                _api.FindNodeOfType<FunctionCall>(analysis.Ast!, line, col)),
            _ => null
        };
    }

    protected override DefinitionRegistrationOptions CreateRegistrationOptions(
        DefinitionCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DefinitionRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
