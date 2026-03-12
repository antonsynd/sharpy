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
            typeAnnotation = SharplySourceGenerator.FormatTypeAnnotation(expressionType);
        }

        // Get the indentation of the containing statement's line (0-based line index)
        var stmtLineIndex = containingStatement.LineStart - 1; // Convert 1-based to 0-based
        var indentation = SharplySourceGenerator.GetIndentation(sourceText, stmtLineIndex);

        // Build the variable declaration text
        var declarationText = typeAnnotation is not null
            ? $"{indentation}{DefaultVariableName}: {typeAnnotation} = "
            : $"{indentation}{DefaultVariableName} = ";

        // Get the expression source text from the original document
        var expressionSourceText = SharplySourceGenerator.GetNodeSourceText(sourceText, expression);
        if (expressionSourceText is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(actions);

        // Build TextEdits:
        // 1. Insert the variable declaration on a new line before the containing statement
        // 2. Replace the selected expression with the variable name

        var edits = new SCG.List<TextEdit>();

        // Edit 1: Insert variable declaration before the statement
        // Insert at the beginning of the containing statement's line
        var insertPosition = PositionConverter.ToLsp(containingStatement.LineStart, 1);
        var declarationLine = $"{declarationText}{expressionSourceText}\n";
        edits.Add(new TextEdit
        {
            Range = new LspRange(insertPosition, insertPosition),
            NewText = declarationLine
        });

        // Edit 2: Replace the expression with the variable name
        var exprStart = PositionConverter.ToLsp(expression.LineStart, expression.ColumnStart);
        var exprEnd = PositionConverter.ToLsp(expression.LineEnd, expression.ColumnEnd);
        edits.Add(new TextEdit
        {
            Range = new LspRange(exprStart, exprEnd),
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
