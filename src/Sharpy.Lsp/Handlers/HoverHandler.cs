using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/hover requests.
/// Returns type information and symbol documentation for the node at the cursor position.
/// </summary>
internal sealed class SharpyHoverHandler : HoverHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharpyHoverHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<Hover?> Handle(HoverParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var node = _api.FindNodeAtPosition(analysis.Ast, line, col);

        if (node == null)
            return null;

        var hoverMarkdown = GetHoverMarkdown(node, analysis, line, col);
        if (hoverMarkdown == null)
            return null;

        return new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(
                new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = hoverMarkdown
                }
            )
        };
    }

    private string? GetHoverMarkdown(Node node, SemanticResult analysis, int line, int col)
    {
        var query = analysis.SemanticQuery!;

        switch (node)
        {
            case Identifier id:
                {
                    var symbol = query.GetIdentifierSymbol(id);
                    if (symbol != null)
                        return SymbolFormatter.FormatSymbolWithDocs(symbol);

                    // Fall back to type info
                    var type = query.GetEffectiveType(id);
                    if (type != null)
                        return $"```sharpy\n{id.Name}: {SymbolFormatter.FormatTypeInfo(type)}\n```";

                    return null;
                }

            case MemberAccess memberAccess:
                {
                    // Check if TypeChecker recorded a static/const member resolution
                    var resolution = query.GetMemberAccessResolution(memberAccess);
                    if (resolution != null)
                        return SymbolFormatter.FormatSymbolWithDocs(resolution.Value.Member);

                    // Check if this MemberAccess is the Function of an enclosing FunctionCall
                    // (e.g., items.count(2) — cursor on "count" gives MemberAccess, but the
                    // resolved call target is on the FunctionCall node)
                    var enclosingCall = _api.FindNodeOfType<FunctionCall>(analysis.Ast!, line, col);
                    if (enclosingCall != null && ReferenceEquals(enclosingCall.Function, memberAccess))
                    {
                        var target = query.GetCallTarget(enclosingCall);
                        if (target != null)
                            return SymbolFormatter.FormatSymbolWithDocs(target);
                    }

                    // Try resolving as a property on a builtin type (is_ok, is_err, is_some, etc.)
                    var objType = query.GetEffectiveType(memberAccess.Object);
                    if (objType != null && analysis.SymbolTable != null)
                    {
                        var builtinType = objType switch
                        {
                            GenericType gt => analysis.SymbolTable.BuiltinRegistry.GetType(gt.Name),
                            ResultType => analysis.SymbolTable.BuiltinRegistry.GetType("Result"),
                            OptionalType => analysis.SymbolTable.BuiltinRegistry.GetType("Optional"),
                            BuiltinType bt => analysis.SymbolTable.BuiltinRegistry.GetType(bt.Name),
                            _ => null
                        };
                        if (builtinType != null)
                        {
                            var prop = builtinType.Properties.FirstOrDefault(p => p.Name == memberAccess.Member);
                            if (prop != null)
                                return SymbolFormatter.FormatPropertyWithDocs(prop);
                        }
                    }

                    // Fall back to type info for the member access expression
                    var memberType = query.GetEffectiveType(memberAccess);
                    if (memberType != null)
                        return $"```sharpy\n{memberAccess.Member}: {SymbolFormatter.FormatTypeInfo(memberType)}\n```";

                    return null;
                }

            case FunctionCall call:
                {
                    var target = query.GetCallTarget(call);
                    if (target != null)
                        return SymbolFormatter.FormatSymbolWithDocs(target);
                    break;
                }

            case Expression expr:
                {
                    var type = query.GetEffectiveType(expr);
                    if (type != null)
                        return $"```sharpy\n{SymbolFormatter.FormatTypeInfo(type)}\n```";
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
