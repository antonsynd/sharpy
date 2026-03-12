using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Text;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/definition requests.
/// Returns the location of the symbol's declaration.
/// </summary>
internal sealed class SharplyDefinitionHandler : DefinitionHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharplyDefinitionHandler(LanguageService languageService, CompilerApi api)
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

        var symbol = ResolveSymbol(node, analysis);
        if (symbol == null)
            return null;

        var location = GetSymbolLocation(symbol, uri);
        if (location == null)
            return null;

        return new LocationOrLocationLinks(location);
    }

    private static Symbol? ResolveSymbol(Node node, SemanticResult analysis)
    {
        var query = analysis.SemanticQuery!;

        return node switch
        {
            Identifier id => query.GetIdentifierSymbol(id),
            FunctionCall call => query.GetCallTarget(call),
            MemberAccess ma => ResolveFromMemberAccess(ma, analysis),
            _ => null
        };
    }

    private static Symbol? ResolveFromMemberAccess(MemberAccess ma, SemanticResult analysis)
    {
        var query = analysis.SemanticQuery!;

        // Try to get the type of the object, then look up the member
        var objType = query.GetEffectiveType(ma.Object);
        if (objType is UserDefinedType udt && udt.Symbol != null)
        {
            // Look through methods and fields
            var memberName = ma.Member;

            var method = udt.Symbol.Methods.Find(m =>
                string.Equals(m.Name, memberName, StringComparison.Ordinal));
            if (method != null)
                return method;

            var field = udt.Symbol.Fields.Find(f =>
                string.Equals(f.Name, memberName, StringComparison.Ordinal));
            if (field != null)
                return field;
        }

        return null;
    }

    private static Location? GetSymbolLocation(Symbol symbol, string fallbackUri)
    {
        if (symbol.DeclarationSpan == null)
            return null;

        var filePath = symbol.DeclaringFilePath ?? fallbackUri;
        var uri = filePath.StartsWith("file://", StringComparison.Ordinal)
            ? DocumentUri.From(filePath)
            : DocumentUri.FromFileSystemPath(filePath);

        // Use DeclarationLine/Column if available (1-based → 0-based)
        var startLine = System.Math.Max(0, (symbol.DeclarationLine ?? 1) - 1);
        var startCol = System.Math.Max(0, (symbol.DeclarationColumn ?? 1) - 1);

        // For end position, estimate based on name length
        var endCol = startCol + symbol.Name.Length;

        return new Location
        {
            Uri = uri,
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(startLine, startCol),
                new Position(startLine, endCol)
            )
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
