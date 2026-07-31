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
    /// The names already bound on one control-flow path of a lexical scope. A binding gets an
    /// inferred-type hint only the first time its name is bound on its path: later assignments
    /// to the same name are rebindings, and a hint on each of them is noise. A function, class
    /// or struct body starts a fresh scope (so a name shadowed in a nested scope hints again at
    /// its own binding); while/for/with bodies share the enclosing scope; mutually-exclusive
    /// branches (if/elif/else, except handlers, match cases) each work on a <see cref="Fork"/>
    /// and are merged back with <see cref="MergeFrom"/> — each branch's first binding hints,
    /// and code after the construct treats a name bound in any branch as already bound.
    /// </summary>
    private sealed class BindingScope
    {
        private readonly HashSet<string> _bound;

        public BindingScope() => _bound = new(StringComparer.Ordinal);

        private BindingScope(HashSet<string> bound) => _bound = bound;

        /// <summary>Records <paramref name="name"/> as bound; true if this is its first binding.</summary>
        public bool TryDeclare(string name) => _bound.Add(name);

        /// <summary>
        /// Records a binding that never carries a hint itself (parameter, loop target,
        /// <c>with … as</c>, <c>except … as</c>, match-pattern capture) so a later assignment
        /// to the same name is correctly treated as a rebinding.
        /// </summary>
        public void MarkBound(string name) => _bound.Add(name);

        /// <summary>A copy for one branch of a mutually-exclusive construct.</summary>
        public BindingScope Fork() => new(new HashSet<string>(_bound, StringComparer.Ordinal));

        /// <summary>Folds a branch's bindings back in: bound in any branch is bound after it.</summary>
        public void MergeFrom(BindingScope branch) => _bound.UnionWith(branch._bound);
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
    /// <c>for</c>-loop targets, walrus bindings and match-pattern captures, each of which is its
    /// own question about where the hint belongs; they are recorded as bound so they do not turn
    /// a later assignment into a spurious declaration, but they produce no hint.
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
                    {
                        // Mutually-exclusive branches: each is its own control-flow path, so a
                        // name bound in one branch still declares (and hints) in a sibling.
                        var branchScopes = new List<BindingScope> { scope.Fork() };
                        CollectInlayHints(ifStmt.ThenBody, analysis, range, hints, typeAnnotations, branchScopes[0]);
                        foreach (var elif in ifStmt.ElifClauses)
                        {
                            var elifScope = scope.Fork();
                            branchScopes.Add(elifScope);
                            CollectInlayHints(elif.Body, analysis, range, hints, typeAnnotations, elifScope);
                        }
                        if (ifStmt.ElseBody.Length > 0)
                        {
                            var elseScope = scope.Fork();
                            branchScopes.Add(elseScope);
                            CollectInlayHints(ifStmt.ElseBody, analysis, range, hints, typeAnnotations, elseScope);
                        }
                        foreach (var branch in branchScopes)
                            scope.MergeFrom(branch);
                        break;
                    }
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
                    {
                        // The try body and each except handler are alternative paths; the else
                        // block runs only after the try body completes, so it continues the try
                        // body's fork. finally runs on every path and sees the merged scope.
                        var tryScope = scope.Fork();
                        CollectInlayHints(tryStmt.Body, analysis, range, hints, typeAnnotations, tryScope);
                        var handlerScopes = new List<BindingScope>();
                        foreach (var handler in tryStmt.Handlers)
                        {
                            var handlerScope = scope.Fork();
                            handlerScopes.Add(handlerScope);
                            if (handler.Name != null)
                                handlerScope.MarkBound(handler.Name);
                            CollectInlayHints(handler.Body, analysis, range, hints, typeAnnotations, handlerScope);
                        }
                        if (tryStmt.ElseBody.Length > 0)
                            CollectInlayHints(tryStmt.ElseBody, analysis, range, hints, typeAnnotations, tryScope);
                        scope.MergeFrom(tryScope);
                        foreach (var handlerScope in handlerScopes)
                            scope.MergeFrom(handlerScope);
                        if (tryStmt.FinallyBody.Length > 0)
                            CollectInlayHints(tryStmt.FinallyBody, analysis, range, hints, typeAnnotations, scope);
                        break;
                    }
                case WithStatement withStmt:
                    foreach (var item in withStmt.Items)
                    {
                        if (item.Name != null)
                            scope.MarkBound(item.Name);
                    }
                    CollectInlayHints(withStmt.Body, analysis, range, hints, typeAnnotations, scope);
                    break;
                case MatchStatement matchStmt:
                    {
                        // Sibling cases are alternative paths; a case's pattern captures are
                        // bindings, so an assignment to one inside the body is a rebinding.
                        var caseScopes = new List<BindingScope>();
                        foreach (var matchCase in matchStmt.Cases)
                        {
                            var caseScope = scope.Fork();
                            caseScopes.Add(caseScope);
                            MarkPatternBound(matchCase.Pattern, caseScope);
                            CollectInlayHints(matchCase.Body, analysis, range, hints, typeAnnotations, caseScope);
                        }
                        foreach (var caseScope in caseScopes)
                            scope.MergeFrom(caseScope);
                        break;
                    }
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
    /// Records the names a match pattern captures (<c>case x:</c>, <c>case Point(x=px):</c>,
    /// <c>case [head, *rest]:</c>, …). Like loop targets, captures produce no hint themselves,
    /// but a later assignment to a captured name is a rebinding.
    /// </summary>
    private static void MarkPatternBound(Pattern pattern, BindingScope scope)
    {
        switch (pattern)
        {
            case BindingPattern binding:
                scope.MarkBound(binding.Name.Name);
                break;
            case TypePattern { BindingName: { } bindingName }:
                scope.MarkBound(bindingName.Name);
                break;
            case StarPattern { Capture: { } capture }:
                MarkPatternBound(capture, scope);
                break;
            case TuplePattern tuple:
                foreach (var element in tuple.Elements)
                    MarkPatternBound(element, scope);
                break;
            case ListPattern list:
                foreach (var element in list.Elements)
                    MarkPatternBound(element, scope);
                break;
            case PositionalPattern positional:
                foreach (var element in positional.Elements)
                    MarkPatternBound(element, scope);
                break;
            case PropertyPattern property:
                foreach (var field in property.Fields)
                    MarkPatternBound(field.Pattern, scope);
                break;
            case UnionCasePattern unionCase:
                foreach (var field in unionCase.FieldPatterns)
                    MarkPatternBound(field, scope);
                break;
            case OrPattern orPattern:
                foreach (var alternative in orPattern.Alternatives)
                    MarkPatternBound(alternative, scope);
                break;
            case AndPattern andPattern:
                MarkPatternBound(andPattern.Left, scope);
                MarkPatternBound(andPattern.Right, scope);
                break;
            case GuardPattern guard:
                MarkPatternBound(guard.Inner, scope);
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
