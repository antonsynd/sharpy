using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/inlayHint requests.
/// Shows inferred types for variables without type annotations
/// and parameter names at call sites.
/// </summary>
internal sealed class SharplyInlayHintHandler : InlayHintsHandlerBase
{
    private readonly SharplyWorkspace _workspace;

    public SharplyInlayHintHandler(SharplyWorkspace workspace)
    {
        _workspace = workspace;
    }

    public override async Task<InlayHintContainer?> Handle(InlayHintParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _workspace.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var hints = new List<InlayHint>();
        var range = request.Range;

        CollectInlayHints(analysis.Ast.Body, analysis, range, hints);

        return new InlayHintContainer(hints);
    }

    public override Task<InlayHint> Handle(InlayHint request, CancellationToken ct)
    {
        // No resolve needed — hints are fully populated on first pass
        return Task.FromResult(request);
    }

    private static void CollectInlayHints(
        IEnumerable<Statement> statements,
        Compiler.SemanticResult analysis,
        LspRange range,
        List<InlayHint> hints)
    {
        foreach (var stmt in statements)
        {
            // Variable declarations without type annotations -> show inferred type
            if (stmt is VariableDeclaration varDecl && varDecl.Type == null)
            {
                var lspLine = System.Math.Max(0, varDecl.LineStart - 1);
                if (lspLine < range.Start.Line || lspLine > range.End.Line)
                    continue;

                var symbol = analysis.SymbolTable?.Lookup(varDecl.Name);
                var inferredType = (symbol as VariableSymbol)?.Type;

                if (inferredType != null && inferredType is not UnknownType && inferredType is not VoidType)
                {
                    var col = System.Math.Max(0, varDecl.ColumnStart - 1) + varDecl.Name.Length;
                    hints.Add(new InlayHint
                    {
                        Position = new Position(lspLine, col),
                        Label = new StringOrInlayHintLabelParts($": {inferredType.GetDisplayName()}"),
                        Kind = InlayHintKind.Type,
                        PaddingLeft = false,
                        PaddingRight = true
                    });
                }
            }

            // Function calls -> show parameter names
            if (stmt is ExpressionStatement exprStmt && exprStmt.Expression is FunctionCall call)
            {
                AddParameterHints(call, analysis, range, hints);
            }

            // Recurse into compound statements
            if (stmt is FunctionDef funcDef)
            {
                CollectInlayHints(funcDef.Body, analysis, range, hints);
            }
            else if (stmt is ClassDef classDef)
            {
                CollectInlayHints(classDef.Body, analysis, range, hints);
            }
            else if (stmt is IfStatement ifStmt)
            {
                CollectInlayHints(ifStmt.ThenBody, analysis, range, hints);
                foreach (var elif in ifStmt.ElifClauses)
                    CollectInlayHints(elif.Body, analysis, range, hints);
                if (ifStmt.ElseBody.Length > 0)
                    CollectInlayHints(ifStmt.ElseBody, analysis, range, hints);
            }
            else if (stmt is WhileStatement whileStmt)
            {
                CollectInlayHints(whileStmt.Body, analysis, range, hints);
            }
            else if (stmt is ForStatement forStmt)
            {
                CollectInlayHints(forStmt.Body, analysis, range, hints);
            }
        }
    }

    private static void AddParameterHints(
        FunctionCall call,
        Compiler.SemanticResult analysis,
        LspRange range,
        List<InlayHint> hints)
    {
        var query = analysis.SemanticQuery!;
        var target = query.GetCallTarget(call);
        if (target == null)
            return;

        var parameters = target.Parameters;

        // Determine offset for 'self' parameter
        var paramOffset = 0;
        if (parameters.Count > 0 &&
            string.Equals(parameters[0].Name, "self", StringComparison.Ordinal))
        {
            paramOffset = 1;
        }

        // Positional arguments only (keyword arguments already show their name)
        for (var i = 0; i < call.Arguments.Length; i++)
        {
            var paramIndex = i + paramOffset;
            if (paramIndex >= parameters.Count)
                break;

            var arg = call.Arguments[i];
            var param = parameters[paramIndex];
            var lspLine = System.Math.Max(0, arg.LineStart - 1);

            if (lspLine < range.Start.Line || lspLine > range.End.Line)
                continue;

            var col = System.Math.Max(0, arg.ColumnStart - 1);
            hints.Add(new InlayHint
            {
                Position = new Position(lspLine, col),
                Label = new StringOrInlayHintLabelParts($"{param.Name}:"),
                Kind = InlayHintKind.Parameter,
                PaddingLeft = false,
                PaddingRight = true
            });
        }
    }

    protected override InlayHintRegistrationOptions CreateRegistrationOptions(
        InlayHintClientCapabilities capability,
        ClientCapabilities clientCapabilities)
    {
        return new InlayHintRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy"),
            ResolveProvider = false
        };
    }
}
