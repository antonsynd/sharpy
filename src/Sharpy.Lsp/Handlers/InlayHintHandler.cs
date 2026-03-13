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
internal sealed class SharpyInlayHintHandler : InlayHintsHandlerBase
{
    private readonly LanguageService _languageService;

    public SharpyInlayHintHandler(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public override async Task<InlayHintContainer?> Handle(InlayHintParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

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
                if (lspLine >= range.Start.Line && lspLine <= range.End.Line)
                {
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

                // Check initializer for function calls
                if (varDecl.InitialValue != null)
                    CollectCallHintsFromExpression(varDecl.InitialValue, analysis, range, hints);
            }

            // Expression statements with function calls -> show parameter names
            if (stmt is ExpressionStatement exprStmt)
            {
                CollectCallHintsFromExpression(exprStmt.Expression, analysis, range, hints);
            }

            // Return statements may contain function calls
            if (stmt is ReturnStatement returnStmt && returnStmt.Value != null)
            {
                CollectCallHintsFromExpression(returnStmt.Value, analysis, range, hints);
            }

            // Recurse into compound statements
            switch (stmt)
            {
                case FunctionDef funcDef:
                    CollectInlayHints(funcDef.Body, analysis, range, hints);
                    break;
                case ClassDef classDef:
                    CollectInlayHints(classDef.Body, analysis, range, hints);
                    break;
                case StructDef structDef:
                    CollectInlayHints(structDef.Body, analysis, range, hints);
                    break;
                case IfStatement ifStmt:
                    CollectInlayHints(ifStmt.ThenBody, analysis, range, hints);
                    foreach (var elif in ifStmt.ElifClauses)
                        CollectInlayHints(elif.Body, analysis, range, hints);
                    if (ifStmt.ElseBody.Length > 0)
                        CollectInlayHints(ifStmt.ElseBody, analysis, range, hints);
                    break;
                case WhileStatement whileStmt:
                    CollectInlayHints(whileStmt.Body, analysis, range, hints);
                    break;
                case ForStatement forStmt:
                    CollectInlayHints(forStmt.Body, analysis, range, hints);
                    if (forStmt.ElseBody.Length > 0)
                        CollectInlayHints(forStmt.ElseBody, analysis, range, hints);
                    break;
                case TryStatement tryStmt:
                    CollectInlayHints(tryStmt.Body, analysis, range, hints);
                    foreach (var handler in tryStmt.Handlers)
                        CollectInlayHints(handler.Body, analysis, range, hints);
                    if (tryStmt.ElseBody.Length > 0)
                        CollectInlayHints(tryStmt.ElseBody, analysis, range, hints);
                    if (tryStmt.FinallyBody.Length > 0)
                        CollectInlayHints(tryStmt.FinallyBody, analysis, range, hints);
                    break;
                case WithStatement withStmt:
                    CollectInlayHints(withStmt.Body, analysis, range, hints);
                    break;
                case MatchStatement matchStmt:
                    foreach (var matchCase in matchStmt.Cases)
                        CollectInlayHints(matchCase.Body, analysis, range, hints);
                    break;
            }
        }
    }

    /// <summary>
    /// Recursively walks an expression tree to find FunctionCall nodes
    /// and add parameter name hints for each.
    /// </summary>
    private static void CollectCallHintsFromExpression(
        Expression expr,
        Compiler.SemanticResult analysis,
        LspRange range,
        List<InlayHint> hints)
    {
        if (expr is FunctionCall call)
        {
            AddParameterHints(call, analysis, range, hints);
            // Recurse into arguments (they may contain nested calls)
            foreach (var arg in call.Arguments)
                CollectCallHintsFromExpression(arg, analysis, range, hints);
            // Recurse into the function expression itself (e.g., obj.method())
            CollectCallHintsFromExpression(call.Function, analysis, range, hints);
            return;
        }

        if (expr is BinaryOp binExpr)
        {
            CollectCallHintsFromExpression(binExpr.Left, analysis, range, hints);
            CollectCallHintsFromExpression(binExpr.Right, analysis, range, hints);
            return;
        }

        if (expr is UnaryOp unaryExpr)
        {
            CollectCallHintsFromExpression(unaryExpr.Operand, analysis, range, hints);
            return;
        }

        if (expr is MemberAccess memberAccess)
        {
            CollectCallHintsFromExpression(memberAccess.Object, analysis, range, hints);
            return;
        }

        if (expr is IndexAccess indexExpr)
        {
            CollectCallHintsFromExpression(indexExpr.Object, analysis, range, hints);
            CollectCallHintsFromExpression(indexExpr.Index, analysis, range, hints);
            return;
        }

        if (expr is ConditionalExpression condExpr)
        {
            CollectCallHintsFromExpression(condExpr.Test, analysis, range, hints);
            CollectCallHintsFromExpression(condExpr.ThenValue, analysis, range, hints);
            CollectCallHintsFromExpression(condExpr.ElseValue, analysis, range, hints);
            return;
        }

        if (expr is TupleLiteral tupleExpr)
        {
            foreach (var element in tupleExpr.Elements)
                CollectCallHintsFromExpression(element, analysis, range, hints);
            return;
        }

        if (expr is ListLiteral listExpr)
        {
            foreach (var element in listExpr.Elements)
                CollectCallHintsFromExpression(element, analysis, range, hints);
            return;
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
