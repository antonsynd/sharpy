using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Provides "Extract Variable" refactoring code actions.
/// When the user selects an expression, offers to extract it into a named local variable.
/// </summary>
internal sealed class ExtractVariableProvider : ICodeActionProvider
{
    private static readonly AstPositionService PositionService = new();

    private const string DefaultVariableName = "result";

    public Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(
        CodeActionProviderContext context,
        CancellationToken cancellationToken)
    {
        var actions = new SCG.List<CodeAction>();

        // We need a non-empty selection, source text, and a parsed AST
        if (context.SourceText is null || context.Analysis?.Ast is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(actions);

        var selection = context.Range;

        // Only trigger on non-empty (range) selections
        if (SelectionAnalyzer.IsZeroWidthSelection(selection))
            return Task.FromResult<IReadOnlyList<CodeAction>>(actions);

        var ast = context.Analysis.Ast;
        var sourceText = context.SourceText;

        // Find the selected expression
        var expression = SelectionAnalyzer.FindSelectedExpression(ast, sourceText, selection);
        if (expression is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(actions);

        // Don't offer extract for trivial expressions (simple identifiers, simple literals)
        if (IsTrivialExpression(expression))
            return Task.FromResult<IReadOnlyList<CodeAction>>(actions);

        // Find the containing statement so we know where to insert the variable declaration
        var containingStatement = FindContainingStatement(ast, expression);
        if (containingStatement is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(actions);

        // Get the type of the expression if semantic info is available
        var semanticQuery = context.Analysis.SemanticQuery;
        var expressionType = semanticQuery?.GetEffectiveType(expression)
                          ?? semanticQuery?.GetExpressionType(expression);

        // Build the type annotation string
        string? typeAnnotation = null;
        if (expressionType is not null and not UnknownType and not VoidType)
        {
            typeAnnotation = SharpySourceGenerator.FormatTypeAnnotation(expressionType);
        }

        // Get the indentation of the containing statement's line (0-based line index)
        var stmtLineIndex = containingStatement.LineStart - 1; // Convert 1-based to 0-based
        var indentation = SharpySourceGenerator.GetIndentation(sourceText, stmtLineIndex);

        // Build the variable declaration text
        var declarationText = typeAnnotation is not null
            ? $"{indentation}{DefaultVariableName}: {typeAnnotation} = "
            : $"{indentation}{DefaultVariableName} = ";

        // Get the expression source text from the original document — its TRUE extent (#2295: a node whose last
        // token is a multi-line string records ColumnEnd on the token's start line), the same span the replace
        // edit below covers.
        var source = SourceLines.Of(sourceText);
        if (source.NodeSpan(expression) is not { } span)
            return Task.FromResult<IReadOnlyList<CodeAction>>(actions);
        var expressionSourceText = sourceText.Substring(span.Start, span.End - span.Start);

        // Build TextEdits:
        // 1. Insert the variable declaration on a new line before the containing statement
        // 2. Replace the selected expression with the variable name

        var edits = new SCG.List<TextEdit>();

        // Edit 1: Insert variable declaration before the statement
        // Insert at the beginning of the containing statement's line
        var insertPosition = PositionConverter.ToLsp(containingStatement.LineStart, 1);
        // A bare tuple (`1, 2` on the right of `let a, b = 1, 2`) is not an initializer: in a declaration it takes
        // its parentheses, or `result: … = 1, 2` does not parse (SPY0103) (P47, #2232).
        if (expression is TupleLiteral && !IsWrappedInParentheses(source, span.Start, span.End))
            expressionSourceText = "(" + expressionSourceText + ")";
        var declarationLine = declarationText + expressionSourceText + source.DocumentLineBreak;
        edits.Add(new TextEdit
        {
            Range = new LspRange(insertPosition, insertPosition),
            NewText = declarationLine
        });

        // Edit 2: Replace the expression with the variable name
        edits.Add(new TextEdit
        {
            Range = new LspRange(source.PositionOf(span.Start), source.PositionOf(span.End)),
            NewText = DefaultVariableName
        });

        var workspaceEdit = new WorkspaceEdit
        {
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [context.DocumentUri] = edits
            }
        };

        var title = typeAnnotation is not null
            ? $"Extract variable '{DefaultVariableName}: {typeAnnotation}'"
            : $"Extract variable '{DefaultVariableName}'";

        actions.Add(new CodeAction
        {
            Title = title,
            Kind = CodeActionKind.RefactorExtract,
            Edit = workspaceEdit
        });

        return Task.FromResult<IReadOnlyList<CodeAction>>(actions);
    }

    /// <summary>
    /// True when the text at <c>[start, end)</c> is one parenthesized group: its first token is <c>(</c> and the
    /// matching <c>)</c> is its last token (<c>(1, 2)</c> yes; <c>(f), g</c> no), read from the lexer's tokens.
    /// </summary>
    private static bool IsWrappedInParentheses(SourceLines source, int start, int end)
    {
        var depth = 0;
        var first = true;
        foreach (var token in source.Tokens)
        {
            if (token.Position < start || token.Length <= 0)
                continue;
            if (token.Position >= end)
                break;
            if (token.Type is Sharpy.Compiler.Lexer.TokenType.Newline or Sharpy.Compiler.Lexer.TokenType.Indent
                or Sharpy.Compiler.Lexer.TokenType.Dedent)
                continue;
            if (first && token.Type != Sharpy.Compiler.Lexer.TokenType.LeftParen)
                return false;
            first = false;
            if (token.Type is Sharpy.Compiler.Lexer.TokenType.LeftParen or Sharpy.Compiler.Lexer.TokenType.LeftBracket
                or Sharpy.Compiler.Lexer.TokenType.LeftBrace)
                depth++;
            else if (token.Type is Sharpy.Compiler.Lexer.TokenType.RightParen or Sharpy.Compiler.Lexer.TokenType.RightBracket
                or Sharpy.Compiler.Lexer.TokenType.RightBrace)
            {
                depth--;
                if (depth == 0 && token.Position + token.Length < end)
                    return false;
            }
        }

        return !first && depth == 0;
    }

    /// <summary>
    /// Determines if an expression is too trivial to warrant extraction.
    /// Simple identifiers and basic literals are not useful to extract.
    /// </summary>
    private static bool IsTrivialExpression(Expression expression)
    {
        return expression is Identifier
            or BooleanLiteral
            or NoneLiteral
            or EllipsisLiteral
            or SuperExpression;
    }

    /// <summary>
    /// Finds the nearest containing Statement for a given expression by walking
    /// the AST node hierarchy from innermost to outermost.
    /// </summary>
    /// <remarks>
    /// <see cref="AstPositionService.FindAllContainingNodes"/> returns nodes ordered
    /// outermost-to-innermost. We walk backwards from the expression to find the
    /// first (innermost) Statement that directly contains it.
    /// For example, in <c>print(foo() + bar())</c> selecting <c>foo() + bar()</c>
    /// yields the chain Module -> ExpressionStatement -> FunctionCall -> BinaryOp.
    /// Walking backwards from BinaryOp, the first Statement is ExpressionStatement,
    /// which is the correct insertion point.
    /// </remarks>
    private static Statement? FindContainingStatement(Module ast, Expression expression)
    {
        var allNodes = PositionService.FindAllContainingNodes(
            ast, expression.LineStart, expression.ColumnStart);

        // Walk from innermost to outermost, starting after the expression itself,
        // to find the nearest enclosing Statement.
        var foundExpression = false;
        for (var i = allNodes.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(allNodes[i], expression))
            {
                foundExpression = true;
                continue;
            }

            if (foundExpression && allNodes[i] is Statement stmt)
            {
                return stmt;
            }
        }

        return null;
    }



}
