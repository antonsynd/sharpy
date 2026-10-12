using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using Sharpy.Lsp.Handlers;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Provides "Inline Variable" and "Inline Function" refactoring code actions.
///
/// Inline Variable: When the cursor is on a variable declaration with an initializer,
/// replaces all references with the initializer expression and removes the declaration.
/// Only applies when the initializer has no side effects and there is exactly one assignment.
///
/// Inline Function (single-expression only): When the cursor is on a function call,
/// replaces the call with the function body if it is a single return statement.
/// Substitutes parameter names with argument expressions. Only applies when the
/// function has a single call site.
/// </summary>
internal sealed class InlineProvider : ICodeActionProvider
{
    private static readonly AstPositionService PositionService = new();

    public Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(
        CodeActionProviderContext context,
        CancellationToken cancellationToken)
    {
        var actions = new SCG.List<CodeAction>();

        if (context.SourceText is null || context.Analysis?.Ast is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(actions);

        var ast = context.Analysis.Ast;
        var sourceText = context.SourceText;
        var semanticQuery = context.Analysis.SemanticQuery;

        if (semanticQuery is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(actions);

        var (line, col) = PositionConverter.ToCompiler(context.Range.Start);

        // Try inline variable
        var inlineVarAction = TryInlineVariable(ast, sourceText, semanticQuery, context, line, col);
        if (inlineVarAction is not null)
            actions.Add(inlineVarAction);

        // Try inline function
        var inlineFuncAction = TryInlineFunction(ast, sourceText, semanticQuery, context, line, col);
        if (inlineFuncAction is not null)
            actions.Add(inlineFuncAction);

        return Task.FromResult<IReadOnlyList<CodeAction>>(actions);
    }

    /// <summary>
    /// Attempts to produce an "Inline variable" code action when the cursor is on a
    /// VariableDeclaration that has an initializer with no side effects.
    /// </summary>
    private static CodeAction? TryInlineVariable(
        Module ast,
        string sourceText,
        ISemanticQuery semanticQuery,
        CodeActionProviderContext context,
        int line,
        int col)
    {
        // Find if cursor is on a VariableDeclaration
        var varDecl = FindVariableDeclarationAtPosition(ast, line, col);
        if (varDecl is null)
            return null;

        // Must have an initializer
        if (varDecl.InitialValue is null)
            return null;

        // Initializer must have no side effects
        if (!IsSideEffectFree(varDecl.InitialValue))
            return null;

        // Find the variable's symbol via the identifier at the declaration name.
        // We need to locate an Identifier node for this variable to look up the symbol.
        var symbol = FindVariableSymbol(ast, semanticQuery, varDecl, line, col);
        if (symbol is null)
            return null;

        // Get all references to this variable
        var references = semanticQuery.GetReferences(symbol);

        // Verify no reassignment: the references should only be reads.
        // The declaration itself is not in the references list (references are recorded
        // for Identifier nodes that reference the symbol, not the declaration).
        // Check that no Assignment statement targets this variable anywhere in the AST.
        if (HasReassignment(ast, varDecl.Name))
            return null;

        // Extract the initializer source text
        var initializerText = SharpySourceGenerator.GetNodeSourceText(sourceText, varDecl.InitialValue);
        if (initializerText is null)
            return null;

        // Build text edits:
        // 1. Replace each reference with the initializer text
        // 2. Delete the variable declaration line
        var edits = new SCG.List<TextEdit>();

        // Replace all references with the initializer expression.
        // References are sorted by position; process them (order doesn't matter for
        // non-overlapping edits, but we build them consistently).
        foreach (var reference in references)
        {
            var refStart = PositionConverter.ToLsp(reference.Line, reference.Column);
            var nameLength = SymbolExtents.ReferenceExtentLength(reference, symbol.Name);
            var refEnd = new Position(refStart.Line, refStart.Character + nameLength);

            // Wrap the initializer in parentheses if it's a compound expression
            // to preserve operator precedence at the substitution site.
            var replacementText = NeedsParentheses(varDecl.InitialValue)
                ? $"({initializerText})"
                : initializerText;

            edits.Add(new TextEdit
            {
                Range = new LspRange(refStart, refEnd),
                NewText = replacementText
            });
        }

        // Delete the declaration's whole lines through its TRUE last line (D2: a multi-line string
        // initializer's tail lines go with it), its trailing comment included (D5).
        // The declaration ends where its initializer ends (its true end — a multi-line string's tail line
        // included); the declaration node's own LineEnd can run on to the next statement's line in a class
        // body, and deleting through it would delete that statement too.
        var source = SourceLines.Of(sourceText);
        if (source.NodeSpan(varDecl.InitialValue) is not { } initializerSpan)
            return null;

        // A comment inside the initializer travels with its text to the ONE reference, written once (the rule
        // Convert to if/elif/else applies to a comment in the scrutinee); with two or more references it would be
        // duplicated. (With none it would be dropped — but a declaration with no reference never reaches here:
        // FindVariableSymbol resolves the symbol through a reference identifier, so the action is not offered on
        // it with or without a comment.) A declaration alone in its block cannot be deleted without leaving the
        // block empty. No legal text exists for either: not offered (P47, #2232).
        if ((references.Count != 1 && source.HasCommentIn(initializerSpan.Start, initializerSpan.End))
            || ContainingBlockSize(ast.Body, varDecl) is null or 1)
            return null;
        var declEnd = source.LineOfOffset(Math.Max(initializerSpan.Start, initializerSpan.End - 1));
        if (edits.Any(e => e.Range.Start.Line + 1 <= declEnd && e.Range.End.Line + 1 >= varDecl.LineStart))
            return null; // a reference inside the declaration's own lines: no legal deletion exists
        edits.Add(source.WholeLineEdit(varDecl.LineStart, declEnd, Array.Empty<string>()));

        var workspaceEdit = new WorkspaceEdit
        {
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [context.DocumentUri] = edits
            }
        };

        return new CodeAction
        {
            Title = $"Inline variable '{varDecl.Name}'",
            Kind = CodeActionKind.RefactorInline,
            Edit = workspaceEdit
        };
    }

    /// <summary>
    /// Attempts to produce an "Inline function" code action when the cursor is on a
    /// FunctionCall that targets a single-expression function with one call site.
    /// </summary>
    private static CodeAction? TryInlineFunction(
        Module ast,
        string sourceText,
        ISemanticQuery semanticQuery,
        CodeActionProviderContext context,
        int line,
        int col)
    {
        // Find if cursor is on a FunctionCall
        var funcCall = PositionService.FindNodeOfType<FunctionCall>(ast, line, col);
        if (funcCall is null)
            return null;

        // Resolve the call target
        var targetSymbol = semanticQuery.GetCallTarget(funcCall);
        if (targetSymbol is null)
            return null;

        // Find the function definition AST node
        var funcDef = FindFunctionDef(ast, targetSymbol);
        if (funcDef is null)
            return null;

        // Function body must be a single ReturnStatement with a value
        if (funcDef.Body.Length != 1)
            return null;

        if (funcDef.Body[0] is not ReturnStatement { Value: not null } returnStmt)
            return null;

        // Only inline if there is a single call site
        var references = semanticQuery.GetReferences(targetSymbol);
        if (references.Count != 1)
            return null;

        // Must not have keyword arguments (substitution would be ambiguous)
        if (funcCall.KeywordArguments.Length > 0)
            return null;

        // Argument count must match parameter count (no variadic, no defaults missing)
        var parameters = funcDef.Parameters;

        // Skip 'self' parameter for methods
        var effectiveParams = parameters;
        if (parameters.Length > 0 && parameters[0].Name == "self")
        {
            effectiveParams = parameters.RemoveAt(0);
        }

        if (funcCall.Arguments.Length != effectiveParams.Length)
            return null;

        // The texts this action copies and the call it replaces, at their TRUE extents.
        var source = SourceLines.Of(sourceText);
        if (source.NodeSpan(returnStmt.Value) is not { } returnSpan || source.NodeSpan(funcCall) is not { } callSpan)
            return null;

        var argumentSpans = new SCG.List<(int Start, int End)>();
        foreach (var argument in funcCall.Arguments)
        {
            // An argument passed with a modifier (`in v`, `out v`, `ref v`) is a passing convention, not an
            // expression a use of the parameter could be replaced by: no legal text exists, not offered.
            if (argument is ModifiedArgument || source.NodeSpan(argument) is not { } argumentSpan)
                return null;
            argumentSpans.Add(argumentSpan);
        }

        // A comment in the copied return expression would be duplicated (the function stays), one in an
        // argument copied once per use, one in the call's brackets dropped: no legal text keeps it, so the
        // action is not offered (P47, #2232).
        if (source.HasCommentIn(returnSpan.Start, returnSpan.End) || source.HasCommentIn(callSpan.Start, callSpan.End))
            return null;

        // Substitute each parameter's USES — the identifiers that resolve to the parameter, found in the AST,
        // never a same-spelled name bound inside the expression (a lambda's parameter, a comprehension target),
        // which a textual replace would turn into an argument and un-parse (P47, #2232).
        var uses = new SCG.List<(int Start, int End, int Argument)>();
        var finder = new IdentifierUseFinder();
        finder.Visit(returnStmt.Value);
        foreach (var identifier in finder.Found)
        {
            var parameterIndex = -1;
            for (var i = 0; i < effectiveParams.Length; i++)
            {
                if (effectiveParams[i].Name == identifier.Name)
                    parameterIndex = i;
            }

            if (parameterIndex < 0)
                continue;

            var symbol = semanticQuery.GetIdentifierSymbol(identifier);
            if (symbol is null)
                return null;
            if (symbol.EffectiveNameLine is not { } declared)
                return null;
            var headerEnd = funcDef.HeaderLineEnd > 0 ? funcDef.HeaderLineEnd : funcDef.LineStart;
            if (declared < funcDef.LineStart || declared > headerEnd)
                continue; // bound inside the expression, not the parameter
            if (source.NodeSpan(identifier) is not { } useSpan)
                return null;
            uses.Add((useSpan.Start, useSpan.End, parameterIndex));
        }

        var substitution = new System.Text.StringBuilder();
        var at = returnSpan.Start;
        foreach (var use in uses.OrderBy(u => u.Start))
        {
            substitution.Append(sourceText, at, use.Start - at);
            // A parameter use is an atom: an argument that is not one (a lambda, an operator expression) goes
            // in parentheses so it binds as the parameter did (`f(x)` with f = `lambda x: x + 3` → `(lambda …)(x)`).
            var argumentSpan = argumentSpans[use.Argument];
            var atomic = funcCall.Arguments[use.Argument] is Identifier or IntegerLiteral or FloatLiteral or StringLiteral
                or BooleanLiteral or NoneLiteral or FunctionCall;
            if (!atomic)
                substitution.Append('(');
            substitution.Append(sourceText, argumentSpan.Start, argumentSpan.End - argumentSpan.Start);
            if (!atomic)
                substitution.Append(')');
            at = use.End;
        }

        substitution.Append(sourceText, at, returnSpan.End - at);
        var substitutionText = substitution.ToString();

        // Build the edit: replace the function call with the substituted expression
        var callStart = source.PositionOf(callSpan.Start);
        var callEnd = source.PositionOf(callSpan.End);

        // Wrap in parentheses to preserve grouping at the call site
        var replacementText = $"({substitutionText})";

        var edits = new SCG.List<TextEdit>
        {
            new TextEdit
            {
                Range = new LspRange(callStart, callEnd),
                NewText = replacementText
            }
        };

        var workspaceEdit = new WorkspaceEdit
        {
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [context.DocumentUri] = edits
            }
        };

        return new CodeAction
        {
            Title = $"Inline function '{targetSymbol.Name}'",
            Kind = CodeActionKind.RefactorInline,
            Edit = workspaceEdit
        };
    }

    /// <summary>
    /// Finds a VariableDeclaration at the given 1-based position.
    /// </summary>
    private static VariableDeclaration? FindVariableDeclarationAtPosition(Module ast, int line, int col)
    {
        return PositionService.FindNodeOfType<VariableDeclaration>(ast, line, col);
    }

    /// <summary>
    /// Finds the Symbol for a variable declaration by locating an Identifier node
    /// that matches the variable name at or near the declaration, or by searching
    /// references in the AST.
    /// </summary>
    private static Symbol? FindVariableSymbol(
        Module ast,
        ISemanticQuery semanticQuery,
        VariableDeclaration varDecl,
        int line,
        int col)
    {
        // First, try to find an Identifier at the cursor position
        var nodeAtPosition = PositionService.FindNodeOfType<Identifier>(ast, line, col);
        if (nodeAtPosition is not null && nodeAtPosition.Name == varDecl.Name)
        {
            var sym = semanticQuery.GetIdentifierSymbol(nodeAtPosition);
            if (sym is not null)
                return sym;
        }

        // Walk the AST to find any Identifier that references this variable
        // and has been resolved by the semantic analysis.
        return FindSymbolByWalkingAst(ast, semanticQuery, varDecl.Name);
    }

    /// <summary>
    /// Walks the AST to find the first Identifier with the given name that has a resolved symbol.
    /// </summary>
    private static Symbol? FindSymbolByWalkingAst(
        Module ast,
        ISemanticQuery semanticQuery,
        string name)
    {
        return WalkForIdentifierSymbol(ast, semanticQuery, name);
    }

    /// <summary>
    /// Recursively walks AST nodes to find an Identifier matching the name with a resolved symbol.
    /// </summary>
    private static Symbol? WalkForIdentifierSymbol(Node node, ISemanticQuery semanticQuery, string name)
    {
        if (node is Identifier id && id.Name == name)
        {
            var sym = semanticQuery.GetIdentifierSymbol(id);
            if (sym is not null)
                return sym;
        }

        foreach (var child in node.GetChildNodes())
        {
            var found = WalkForIdentifierSymbol(child, semanticQuery, name);
            if (found is not null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Checks whether the given expression has potential side effects.
    /// Only side-effect-free expressions are safe to inline: literals, identifiers,
    /// member access, and arithmetic/comparison expressions composed of safe sub-expressions.
    /// </summary>
    private static bool IsSideEffectFree(Expression expression)
    {
        return expression switch
        {
            IntegerLiteral => true,
            FloatLiteral => true,
            StringLiteral => true,
            BooleanLiteral => true,
            NoneLiteral => true,
            Identifier => true,
            MemberAccess ma => IsSideEffectFree(ma.Object),
            BinaryOp bin => IsSideEffectFree(bin.Left) && IsSideEffectFree(bin.Right),
            UnaryOp un => IsSideEffectFree(un.Operand),
            Parenthesized p => IsSideEffectFree(p.Expression),
            ConditionalExpression cond =>
                IsSideEffectFree(cond.Test) &&
                IsSideEffectFree(cond.ThenValue) &&
                IsSideEffectFree(cond.ElseValue),
            // Function calls, index access, comprehensions, etc. may have side effects
            _ => false
        };
    }

    /// <summary>
    /// Determines whether the expression needs parentheses when inlined at a usage site.
    /// Binary operations and conditional expressions need wrapping to preserve precedence.
    /// </summary>
    private static bool NeedsParentheses(Expression expression)
    {
        return expression is BinaryOp or UnaryOp or ConditionalExpression;
    }

    /// <summary>
    /// Checks whether the variable with the given name is reassigned anywhere in the AST.
    /// Looks for Assignment statements where the target is an Identifier with the matching name.
    /// </summary>
    private static bool HasReassignment(Module ast, string variableName)
    {
        if (CheckReassignmentInBody(ast.Body, variableName))
            return true;

        // Every binding target anywhere — a tuple or starred target (`let pair, other = …`, `first, *tail = …`),
        // a `for` target — not only a bare-name assignment: substituting a value for a name that is BOUND there
        // does not parse (P47, #2232).
        var finder = new BindingTargetFinder(variableName);
        foreach (var statement in ast.Body)
            finder.Visit(statement);
        return finder.Found;
    }

    /// <summary>
    /// The number of statements in the block that holds <paramref name="target"/> (null when not found): a
    /// declaration alone in its block cannot be deleted — the block would be left empty, which does not parse.
    /// </summary>
    private static int? ContainingBlockSize(IReadOnlyList<Statement> body, Statement target)
    {
        foreach (var statement in body)
        {
            if (ReferenceEquals(statement, target))
                return body.Count;
            foreach (var inner in BodiesOf(statement))
            {
                if (ContainingBlockSize(inner, target) is { } size)
                    return size;
            }
        }

        return null;
    }

    /// <summary>
    /// The statement blocks directly under <paramref name="statement"/>. Deliberately partial: a kind not listed
    /// holds no statement block, and a declaration whose block is not found is not offered
    /// (<see cref="ContainingBlockSize"/> returns null, which the caller refuses).
    /// </summary>
    private static IEnumerable<IReadOnlyList<Statement>> BodiesOf(Statement statement)
    {
        switch (statement)
        {
            case DecoratedStatement decorated:
                yield return new[] { decorated.Statement };
                break;
            case FunctionDef f:
                yield return f.Body;
                break;
            case ClassDef c:
                yield return c.Body;
                break;
            case StructDef s:
                yield return s.Body;
                break;
            case InterfaceDef i:
                yield return i.Body;
                break;
            case WithStatement w:
                yield return w.Body;
                break;
            case IfStatement ifs:
                yield return ifs.ThenBody;
                foreach (var elif in ifs.ElifClauses)
                    yield return elif.Body;
                yield return ifs.ElseBody;
                break;
            case ForStatement fs:
                yield return fs.Body;
                yield return fs.ElseBody;
                break;
            case WhileStatement ws:
                yield return ws.Body;
                yield return ws.ElseBody;
                break;
            case TryStatement ts:
                yield return ts.Body;
                foreach (var handler in ts.Handlers)
                    yield return handler.Body;
                yield return ts.ElseBody;
                yield return ts.FinallyBody;
                break;
            case MatchStatement ms:
                foreach (var matchCase in ms.Cases)
                    yield return matchCase.Body;
                break;
        }
    }

    /// <summary>Every <see cref="Identifier"/> expression under a node.</summary>
    private sealed class IdentifierUseFinder : AstVisitor
    {
        public SCG.List<Identifier> Found { get; } = new();

        public override void VisitIdentifier(Identifier node)
        {
            Found.Add(node);
            base.VisitIdentifier(node);
        }
    }

    /// <summary>Finds <c>name</c> among the identifiers of any assignment or <c>for</c> target.</summary>
    private sealed class BindingTargetFinder(string name) : AstVisitor
    {
        public bool Found { get; private set; }

        public override void VisitAssignment(Assignment node)
        {
            Found |= Names(node.Target);
            base.VisitAssignment(node);
        }

        public override void VisitForStatement(ForStatement node)
        {
            Found |= Names(node.Target);
            base.VisitForStatement(node);
        }

        private bool Names(Expression target)
        {
            var collector = new NameCollector(name);
            collector.Visit(target);
            return collector.Found;
        }
    }

    private sealed class NameCollector(string name) : AstVisitor
    {
        public bool Found { get; private set; }

        public override void VisitIdentifier(Identifier node)
        {
            if (node.Name == name)
                Found = true;
            base.VisitIdentifier(node);
        }
    }

    /// <summary>
    /// Recursively checks a list of statements for reassignment of the given variable.
    /// </summary>
    private static bool CheckReassignmentInBody(
        System.Collections.Immutable.ImmutableArray<Statement> body,
        string variableName)
    {
        foreach (var stmt in body)
        {
            if (CheckReassignmentInStatement(stmt, variableName))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks a single statement (and its nested bodies) for reassignment.
    /// </summary>
    private static bool CheckReassignmentInStatement(Statement stmt, string variableName)
    {
        switch (stmt)
        {
            case Assignment assign:
                if (assign.Target is Identifier id && id.Name == variableName)
                    return true;
                break;

            case IfStatement ifStmt:
                if (CheckReassignmentInBody(ifStmt.ThenBody, variableName))
                    return true;
                foreach (var elif in ifStmt.ElifClauses)
                {
                    if (CheckReassignmentInBody(elif.Body, variableName))
                        return true;
                }
                if (CheckReassignmentInBody(ifStmt.ElseBody, variableName))
                    return true;
                break;

            case ForStatement forStmt:
                if (CheckReassignmentInBody(forStmt.Body, variableName))
                    return true;
                if (CheckReassignmentInBody(forStmt.ElseBody, variableName))
                    return true;
                break;

            case WhileStatement whileStmt:
                if (CheckReassignmentInBody(whileStmt.Body, variableName))
                    return true;
                if (CheckReassignmentInBody(whileStmt.ElseBody, variableName))
                    return true;
                break;

            case TryStatement tryStmt:
                if (CheckReassignmentInBody(tryStmt.Body, variableName))
                    return true;
                foreach (var handler in tryStmt.Handlers)
                {
                    if (CheckReassignmentInBody(handler.Body, variableName))
                        return true;
                }
                if (CheckReassignmentInBody(tryStmt.ElseBody, variableName))
                    return true;
                if (CheckReassignmentInBody(tryStmt.FinallyBody, variableName))
                    return true;
                break;

            case WithStatement withStmt:
                if (CheckReassignmentInBody(withStmt.Body, variableName))
                    return true;
                break;

            case FunctionDef funcDef:
                // Check inside nested function bodies as well
                if (CheckReassignmentInBody(funcDef.Body, variableName))
                    return true;
                break;

            case ClassDef classDef:
                if (CheckReassignmentInBody(classDef.Body, variableName))
                    return true;
                break;
            default:
                // walker-default-contract: any kind not listed above is deliberately ignored by
                // this walker (rostered in DispatchSiteInventoryTests).
                break;
        }

        return false;
    }

    /// <summary>
    /// Finds the FunctionDef AST node that corresponds to a FunctionSymbol,
    /// using the symbol's declaration line and column.
    /// </summary>
    private static FunctionDef? FindFunctionDef(Module ast, FunctionSymbol symbol)
    {
        if (symbol.DeclarationLine is null || symbol.DeclarationColumn is null)
            return null;

        return PositionService.FindNodeOfType<FunctionDef>(
            ast,
            symbol.DeclarationLine.Value,
            symbol.DeclarationColumn.Value)
            ?? (symbol.EffectiveNameLine is not null && symbol.EffectiveNameColumn is not null
                ? PositionService.FindNodeOfType<FunctionDef>(
                    ast,
                    symbol.EffectiveNameLine.Value,
                    symbol.EffectiveNameColumn.Value)
                : null);
    }

}
