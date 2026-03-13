using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
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
            // The reference span covers the identifier name
            var nameLength = varDecl.Name.Length;
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

        // Delete the entire declaration line.
        // We delete from the start of the line to the start of the next line.
        var declLineStart = new Position(varDecl.LineStart - 1, 0);
        var declLineEnd = new Position(varDecl.LineEnd, 0); // Next line start
        edits.Add(new TextEdit
        {
            Range = new LspRange(declLineStart, declLineEnd),
            NewText = ""
        });

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

        // Build the replacement text by substituting parameter names in the return expression
        var returnExprText = SharpySourceGenerator.GetNodeSourceText(sourceText, returnStmt.Value);
        if (returnExprText is null)
            return null;

        // Build parameter-to-argument mapping
        var substitutionText = returnExprText;
        for (var i = 0; i < effectiveParams.Length; i++)
        {
            var paramName = effectiveParams[i].Name;
            var argText = SharpySourceGenerator.GetNodeSourceText(sourceText, funcCall.Arguments[i]);
            if (argText is null)
                return null;

            substitutionText = SubstituteIdentifier(substitutionText, paramName, argText);
        }

        // Build the edit: replace the function call with the substituted expression
        var callStart = PositionConverter.ToLsp(funcCall.LineStart, funcCall.ColumnStart);
        var callEnd = PositionConverter.ToLsp(funcCall.LineEnd, funcCall.ColumnEnd);

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
        return CheckReassignmentInBody(ast.Body, variableName);
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
            symbol.DeclarationColumn.Value);
    }

    /// <summary>
    /// Substitutes all occurrences of an identifier name in expression text with a replacement.
    /// Uses word-boundary-aware replacement to avoid partial matches
    /// (e.g., replacing "x" should not affect "max").
    /// </summary>
    private static string SubstituteIdentifier(string text, string identifier, string replacement)
    {
        // Use a simple word-boundary-aware approach: scan for the identifier
        // and only replace when surrounded by non-identifier characters.
        var result = new System.Text.StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            // Check if the identifier starts at position i
            if (i + identifier.Length <= text.Length &&
                text.AsSpan(i, identifier.Length).SequenceEqual(identifier.AsSpan()))
            {
                // Check word boundaries
                var beforeIsWordChar = i > 0 && IsIdentifierChar(text[i - 1]);
                var afterIsWordChar = i + identifier.Length < text.Length &&
                                      IsIdentifierChar(text[i + identifier.Length]);

                if (!beforeIsWordChar && !afterIsWordChar)
                {
                    result.Append(replacement);
                    i += identifier.Length;
                    continue;
                }
            }

            result.Append(text[i]);
            i++;
        }

        return result.ToString();
    }

    /// <summary>
    /// Returns true if the character can be part of a Python/Sharpy identifier.
    /// </summary>
    private static bool IsIdentifierChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

}
