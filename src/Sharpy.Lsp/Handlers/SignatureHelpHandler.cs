using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/signatureHelp requests.
/// Provides parameter hints when inside function call parentheses.
/// </summary>
internal sealed class SharpySignatureHelpHandler : SignatureHelpHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharpySignatureHelpHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<SignatureHelp?> Handle(SignatureHelpParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var (line, col) = PositionConverter.ToCompiler(request.Position);

        // Find the enclosing function call
        var call = FindEnclosingCall(analysis.Ast, line, col);
        if (call == null)
            return null;

        var target = analysis.SemanticQuery.GetCallTarget(call);
        if (target is not FunctionSymbol funcSymbol)
            return null;

        var parameters = new System.Collections.Generic.List<ParameterInformation>();
        foreach (var param in funcSymbol.Parameters)
        {
            var label = param.Name;
            if (param.Type != null)
                label += $": {SymbolFormatter.FormatTypeInfo(param.Type)}";

            parameters.Add(new ParameterInformation
            {
                Label = new ParameterInformationLabel(label),
            });
        }

        var signatureLabel = BuildSignatureLabel(funcSymbol);
        var signature = new SignatureInformation
        {
            Label = signatureLabel,
            Parameters = new Container<ParameterInformation>(parameters),

        };

        // Estimate active parameter from argument count before cursor
        var activeParam = EstimateActiveParameter(call, line, col);

        return new SignatureHelp
        {
            Signatures = new Container<SignatureInformation>(signature),
            ActiveSignature = 0,
            ActiveParameter = activeParam,
        };
    }

    private static string BuildSignatureLabel(FunctionSymbol func)
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (var param in func.Parameters)
        {
            var part = param.Name;
            if (param.Type != null)
                part += $": {SymbolFormatter.FormatTypeInfo(param.Type)}";
            parts.Add(part);
        }

        var returnPart = func.ReturnType != null
            ? $" -> {SymbolFormatter.FormatTypeInfo(func.ReturnType)}"
            : "";

        return $"def {func.Name}({string.Join(", ", parts)}){returnPart}";
    }

    /// <summary>
    /// Walk the AST to find a FunctionCall that contains the given position.
    /// </summary>
    private static FunctionCall? FindEnclosingCall(Module module, int line, int col)
    {
        FunctionCall? best = null;

        void Visit(Node node)
        {
            if (node is FunctionCall call && Contains(call, line, col))
            {
                best = call;
            }

            foreach (var child in node.GetChildNodes())
            {
                Visit(child);
            }
        }

        foreach (var stmt in module.Body)
        {
            Visit(stmt);
        }

        return best;
    }

    private static bool Contains(Node node, int line, int col)
    {
        if (line < node.LineStart || line > node.LineEnd)
            return false;
        if (line == node.LineStart && col < node.ColumnStart)
            return false;
        if (line == node.LineEnd && col > node.ColumnEnd)
            return false;
        return true;
    }

    /// <summary>
    /// Estimate which parameter is active by counting arguments before the cursor position.
    /// </summary>
    private static int EstimateActiveParameter(FunctionCall call, int line, int col)
    {
        var count = 0;
        foreach (var arg in call.Arguments)
        {
            // If cursor is past this argument, increment
            if (arg.LineEnd < line || (arg.LineEnd == line && arg.ColumnEnd < col))
                count++;
            else
                break;
        }

        return count;
    }

    protected override SignatureHelpRegistrationOptions CreateRegistrationOptions(
        SignatureHelpCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new SignatureHelpRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy"),
            TriggerCharacters = new Container<string>("(", ","),
        };
    }
}
