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
    private readonly LspConfiguration _configuration;

    public SharpyInlayHintHandler(LanguageService languageService, LspConfiguration configuration)
    {
        _languageService = languageService;
        _configuration = configuration;
    }

    public override async Task<InlayHintContainer?> Handle(InlayHintParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var hints = new List<InlayHint>();
        var range = request.Range;

        CollectInlayHints(analysis.Ast.Body, analysis, range, hints,
            typeAnnotations: _configuration.InlayHintTypeAnnotations,
            scope: new BindingScope());

        return new InlayHintContainer(hints);
    }

    public override Task<InlayHint> Handle(InlayHint request, CancellationToken ct)
    {
        // No resolve needed — hints are fully populated on first pass
        return Task.FromResult(request);
    }

    /// <summary>
    /// The names already bound in one lexical scope. A binding gets an inferred-type hint only
    /// the first time its name is bound in the scope: later assignments to the same name are
    /// rebindings, and a hint on each of them is noise. A function, class or struct body starts
    /// a fresh scope (so a name shadowed in a nested scope hints again at its own binding);
    /// if/while/for/try/with/match bodies share the enclosing scope.
    /// </summary>
    private sealed class BindingScope
    {
        private readonly HashSet<string> _bound = new(StringComparer.Ordinal);

        /// <summary>Records <paramref name="name"/> as bound; true if this is its first binding.</summary>
        public bool TryDeclare(string name) => _bound.Add(name);

        /// <summary>
        /// Records a binding that never carries a hint itself (parameter, loop target,
        /// <c>with … as</c>, <c>except … as</c>) so a later assignment to the same name is
        /// correctly treated as a rebinding.
        /// </summary>
        public void MarkBound(string name) => _bound.Add(name);
    }

    /// <param name="typeAnnotations">
    /// Whether inferred-type hints are produced (<c>sharpy.inlayHints.typeAnnotations</c>).
    /// Parameter-name hints are unaffected: they answer a different question and are gated
    /// separately by the client's own inlay-hint toggle.
    /// </param>
    /// <param name="scope">Names already bound in the scope these statements belong to.</param>
    /// <remarks>
    /// Inferred-type hints cover single-identifier bindings — an unannotated declaration and a
    /// plain <c>x = value</c> assignment (#1180). Explicit non-goals: tuple-unpacking targets,
    /// <c>for</c>-loop targets and walrus bindings, each of which is its own question about where
    /// the hint belongs; they are recorded as bound so they do not turn a later assignment into a
    /// spurious declaration, but they produce no hint.
    /// </remarks>
    private static void CollectInlayHints(
        IEnumerable<Statement> statements,
        Compiler.SemanticResult analysis,
        LspRange range,
        List<InlayHint> hints,
        bool typeAnnotations,
        BindingScope scope)
    {
        foreach (var rawStmt in statements)
        {
            // Statement-scoped @suppress (#1024): hints apply to the wrapped statement
            // exactly as if it were unwrapped.
            var stmt = rawStmt is DecoratedStatement decorated ? decorated.Statement : rawStmt;

            // Variable declarations without type annotations -> show inferred type
            if (stmt is VariableDeclaration varDecl)
            {
                var isDeclaring = scope.TryDeclare(varDecl.Name);

                if (typeAnnotations && varDecl.Type == null && isDeclaring)
                {
                    AddInferredTypeHint(
                        analysis.SemanticQuery!.FindSymbolByDeclaration(
                            varDecl.Name, varDecl.LineStart, varDecl.ColumnStart),
                        varDecl.Name, varDecl.NameLineStart, varDecl.NameColumnStart, range, hints);
                }

                // Check initializer for function calls
                if (varDecl.InitialValue != null)
                    CollectCallHintsFromExpression(varDecl.InitialValue, analysis, range, hints);
            }

            // Plain assignments are how most bindings are written; the declaring one shows
            // the type the compiler inferred for it.
            if (stmt is Assignment assignment)
            {
                if (assignment.Target is Identifier assignTarget)
                {
                    // TryDeclare must run for augmented assignments too — they bind the name for
                    // the rest of the scope — but only `=` introduces a value worth annotating.
                    var isDeclaring = scope.TryDeclare(assignTarget.Name)
                        && assignment.Operator == AssignmentOperator.Assign;

                    if (typeAnnotations && isDeclaring)
                    {
                        AddInferredTypeHint(
                            analysis.SemanticQuery!.GetIdentifierSymbol(assignTarget),
                            assignTarget.Name, assignTarget.LineStart, assignTarget.ColumnStart,
                            range, hints);
                    }
                }

                CollectCallHintsFromExpression(assignment.Value, analysis, range, hints);
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
                    {
                        var funcScope = new BindingScope();
                        foreach (var param in funcDef.Parameters)
                            funcScope.MarkBound(param.Name);
                        CollectInlayHints(funcDef.Body, analysis, range, hints, typeAnnotations, funcScope);
                        break;
                    }
                case ClassDef classDef:
                    CollectInlayHints(classDef.Body, analysis, range, hints, typeAnnotations, new BindingScope());
                    break;
                case StructDef structDef:
                    CollectInlayHints(structDef.Body, analysis, range, hints, typeAnnotations, new BindingScope());
                    break;
                case IfStatement ifStmt:
                    CollectInlayHints(ifStmt.ThenBody, analysis, range, hints, typeAnnotations, scope);
                    foreach (var elif in ifStmt.ElifClauses)
                        CollectInlayHints(elif.Body, analysis, range, hints, typeAnnotations, scope);
                    if (ifStmt.ElseBody.Length > 0)
                        CollectInlayHints(ifStmt.ElseBody, analysis, range, hints, typeAnnotations, scope);
                    break;
                case WhileStatement whileStmt:
                    CollectInlayHints(whileStmt.Body, analysis, range, hints, typeAnnotations, scope);
                    break;
                case ForStatement forStmt:
                    MarkTargetBound(forStmt.Target, scope);
                    CollectInlayHints(forStmt.Body, analysis, range, hints, typeAnnotations, scope);
                    if (forStmt.ElseBody.Length > 0)
                        CollectInlayHints(forStmt.ElseBody, analysis, range, hints, typeAnnotations, scope);
                    break;
                case TryStatement tryStmt:
                    CollectInlayHints(tryStmt.Body, analysis, range, hints, typeAnnotations, scope);
                    foreach (var handler in tryStmt.Handlers)
                    {
                        if (handler.Name != null)
                            scope.MarkBound(handler.Name);
                        CollectInlayHints(handler.Body, analysis, range, hints, typeAnnotations, scope);
                    }
                    if (tryStmt.ElseBody.Length > 0)
                        CollectInlayHints(tryStmt.ElseBody, analysis, range, hints, typeAnnotations, scope);
                    if (tryStmt.FinallyBody.Length > 0)
                        CollectInlayHints(tryStmt.FinallyBody, analysis, range, hints, typeAnnotations, scope);
                    break;
                case WithStatement withStmt:
                    foreach (var item in withStmt.Items)
                    {
                        if (item.Name != null)
                            scope.MarkBound(item.Name);
                    }
                    CollectInlayHints(withStmt.Body, analysis, range, hints, typeAnnotations, scope);
                    break;
                case MatchStatement matchStmt:
                    foreach (var matchCase in matchStmt.Cases)
                        CollectInlayHints(matchCase.Body, analysis, range, hints, typeAnnotations, scope);
                    break;
            }
        }
    }

    /// <summary>
    /// Records the names a loop target binds. Tuple targets bind each element; anything else
    /// (attribute or index targets) binds no name.
    /// </summary>
    private static void MarkTargetBound(Expression target, BindingScope scope)
    {
        switch (target)
        {
            case Identifier id:
                scope.MarkBound(id.Name);
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    MarkTargetBound(element, scope);
                break;
            case StarExpression star:
                MarkTargetBound(star.Operand, scope);
                break;
        }
    }

    /// <summary>
    /// Adds the inferred-type hint for a binding whose type is not written in the source.
    /// <paramref name="symbol"/> comes from the semantic model rather than a module-scope name
    /// lookup, which is what lets function-local and shadowed bindings resolve to their own
    /// symbol; the hint is produced only when that symbol is the one declared at
    /// <paramref name="nameLine"/>/<paramref name="nameColumn"/> (1-based source coordinates),
    /// so a rebinding that resolves to an earlier declaration stays silent.
    /// </summary>
    private static void AddInferredTypeHint(
        Symbol? symbol,
        string name,
        int nameLine,
        int nameColumn,
        LspRange range,
        List<InlayHint> hints)
    {
        if (symbol is not VariableSymbol variable)
            return;

        if (variable.EffectiveNameLine != nameLine || variable.EffectiveNameColumn != nameColumn)
            return;

        var inferredType = variable.Type;
        if (inferredType == null || inferredType is UnknownType || inferredType is VoidType)
            return;

        var lspLine = System.Math.Max(0, nameLine - 1);
        if (lspLine < range.Start.Line || lspLine > range.End.Line)
            return;

        var col = System.Math.Max(0, nameColumn - 1) + name.Length;
        hints.Add(new InlayHint
        {
            Position = new Position(lspLine, col),
            Label = new StringOrInlayHintLabelParts($": {inferredType.GetDisplayName()}"),
            Kind = InlayHintKind.Type,
            PaddingLeft = false,
            PaddingRight = true
        });
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

        if (expr is MultiAxisAccess multiAxisExpr)
        {
            CollectCallHintsFromExpression(multiAxisExpr.Object, analysis, range, hints);
            foreach (var dim in multiAxisExpr.Dimensions)
            {
                if (dim.Index != null)
                    CollectCallHintsFromExpression(dim.Index, analysis, range, hints);
                if (dim.Start != null)
                    CollectCallHintsFromExpression(dim.Start, analysis, range, hints);
                if (dim.Stop != null)
                    CollectCallHintsFromExpression(dim.Stop, analysis, range, hints);
                if (dim.Step != null)
                    CollectCallHintsFromExpression(dim.Step, analysis, range, hints);
            }
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
