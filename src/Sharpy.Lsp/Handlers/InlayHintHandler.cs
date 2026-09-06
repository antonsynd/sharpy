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
                    // Keyed on the declaration node, like the Assignment arm below is keyed on
                    // its target identifier. The name-and-position scan this replaced searched
                    // reference-populated collections and module scope, so a function-local
                    // binding nothing reads — `const LIMIT = 42` and no more — resolved to
                    // nothing and showed no type (#1222).
                    AddInferredTypeHint(
                        analysis.SemanticQuery!.GetDeclarationSymbol(varDecl),
                        varDecl.NameLineStart, varDecl.NameColumnStart, varDecl.NameColumnEnd, range, hints);
                }

            }

            // Plain assignments are how most bindings are written; the declaring one shows
            // the type the compiler inferred for it.
            if (stmt is Assignment { Target: Identifier assignTarget } assignment)
            {
                // TryDeclare must run for augmented assignments too — they bind the name for
                // the rest of the scope — but only `=` introduces a value worth annotating.
                // Whether a bare `x = value` declares or rebinds is the checker's recorded
                // TargetBinding, not this walker's lexical scope: a def body starts a fresh
                // BindingScope, but a store to a name that exists in an ENCLOSING scope — the
                // module or the enclosing function — writes through to that binding
                // (variable_scoping.md §Write-Through Assignment) and is not a declaration.
                var isDeclaring = scope.TryDeclare(assignTarget.Name)
                    && assignment.Operator == AssignmentOperator.Assign
                    && analysis.SemanticInfo?.GetTargetBinding(assignTarget)?.Kind != TargetBindingKind.Rebinds;

                if (typeAnnotations && isDeclaring)
                {
                    AddInferredTypeHint(
                        analysis.SemanticQuery!.GetIdentifierSymbol(assignTarget),
                        assignTarget.LineStart, assignTarget.ColumnStart,
                        assignTarget.ColumnStart + SymbolExtents.SourceNameLength(assignTarget.Name, assignTarget.IsNameBacktickEscaped),
                        range, hints);
                }
            }

            // Parameter-name hints for every call in every expression position of the statement:
            // initializers and values, but also `if`/`while` conditions, `for` iterators, `with`
            // context expressions, `match` scrutinees and guards, `except` filters, `assert` /
            // `raise` / `yield` operands, parameter defaults. The statement's own GetChildNodes is
            // the authority on which positions those are (the same reasoning as CallHintCollector
            // below); the four-kind if-chain this replaced hinted only declarations, assignments,
            // expression statements and returns, so a call in a condition or an iterator was
            // silently unhinted (plan-950124 Phase 2 remediation). Suites are recursed by the
            // switch below, so nested statements hint on their own turn.
            foreach (var child in stmt.GetChildNodes())
            {
                if (child is Expression expression)
                    CollectCallHintsFromExpression(expression, analysis, range, hints);
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
                    BindingScopeWalker.MarkTargetBound(forStmt.Target, scope);
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
                        // Identifier-only: non-identifier targets bind no name (#1697).
                        if (item.Target is Identifier iId)
                            scope.MarkBound(iId.Name);
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
                            BindingScopeWalker.MarkPatternBound(matchCase.Pattern, caseScope, analysis.SemanticInfo);
                            CollectInlayHints(matchCase.Body, analysis, range, hints, typeAnnotations, caseScope);
                        }
                        foreach (var caseScope in caseScopes)
                            scope.MergeFrom(caseScope);
                        break;
                    }
                // Bodied declarations and the deferred suite (plan-950124 Phase 2 remediation):
                // before this their statements were never visited, so a call inside a property
                // getter, a union method, a function-style event accessor or a `defer` block got
                // no hint at all.
                case PropertyDef propDef:
                    {
                        var propScope = new BindingScope();
                        foreach (var param in propDef.Parameters)
                            propScope.MarkBound(param.Name);
                        CollectInlayHints(propDef.Body, analysis, range, hints, typeAnnotations, propScope);
                        foreach (var observer in propDef.Observers)
                        {
                            var observerScope = new BindingScope();
                            observerScope.MarkBound(observer.ParamName);
                            CollectInlayHints(observer.Body, analysis, range, hints, typeAnnotations, observerScope);
                        }
                        break;
                    }
                case UnionDef unionDef:
                    CollectInlayHints(unionDef.Body, analysis, range, hints, typeAnnotations, new BindingScope());
                    break;
                case EventDef eventDef:
                    {
                        var eventScope = new BindingScope();
                        foreach (var param in eventDef.Parameters)
                            eventScope.MarkBound(param.Name);
                        CollectInlayHints(eventDef.Body, analysis, range, hints, typeAnnotations, eventScope);
                        break;
                    }
                case DeferStatement deferStmt:
                    // The deferred suite runs in the enclosing scope: same BindingScope.
                    CollectInlayHints(deferStmt.Body, analysis, range, hints, typeAnnotations, scope);
                    break;
            }
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
        int nameLine,
        int nameColumn,
        int nameColumnEnd,
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

        var col = System.Math.Max(0, nameColumnEnd - 1);
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
    /// Adds parameter-name hints for every call anywhere inside an expression tree.
    /// </summary>
    private static void CollectCallHintsFromExpression(
        Expression expr,
        Compiler.SemanticResult analysis,
        LspRange range,
        List<InlayHint> hints)
    {
        new CallHintCollector(analysis, range, hints).Visit(expr);
    }

    /// <summary>
    /// Finds the calls in an expression tree. It overrides one method — every other node type
    /// reaches its children through <see cref="AstVisitor.DefaultVisit"/>, which walks
    /// <c>GetChildNodes()</c>.
    /// <para>
    /// This deliberately owns no list of expression forms. The walker it replaced was a chain of
    /// <c>if (expr is X)</c> arms covering nine of the AST's forty-one expression types, so
    /// comprehensions, lambdas, dict/set literals, f-strings, slices, comparison chains, await and
    /// sixteen other forms silently produced no hints, as did a call in a keyword argument even
    /// though <c>FunctionCall</c> was on the list (#1223). Any hand-maintained enumeration —
    /// including a <c>switch</c> over every node — falls behind the AST again on the next node
    /// added; deferring to the traversal authority cannot. <c>Parenthesized</c> in particular
    /// needs no unwrapping here: its child is yielded like any other, so <c>(f(x))</c> hints
    /// exactly like <c>f(x)</c>.
    /// </para>
    /// </summary>
    private sealed class CallHintCollector : AstVisitor
    {
        private readonly Compiler.SemanticResult _analysis;
        private readonly LspRange _range;
        private readonly List<InlayHint> _hints;

        public CallHintCollector(Compiler.SemanticResult analysis, LspRange range, List<InlayHint> hints)
        {
            _analysis = analysis;
            _range = range;
            _hints = hints;
        }

        public override void VisitFunctionCall(FunctionCall node)
        {
            AddParameterHints(node, _analysis, _range, _hints);

            // Keep descending: the callee, the positional arguments and the keyword-argument
            // values can all hold further calls.
            base.VisitFunctionCall(node);
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
