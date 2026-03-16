using System.Collections.Immutable;
using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Provides refactoring code actions for converting between equivalent language forms:
/// <list type="bullet">
///   <item>if/elif/else to match statement (when conditions are simple equality checks)</item>
///   <item>match statement to if/elif/else (when all cases use literal/wildcard patterns)</item>
///   <item>Add type annotations to variable declarations</item>
///   <item>Remove type annotations from variable declarations</item>
///   <item>Wrap selected statements in try/except</item>
/// </list>
/// </summary>
internal sealed class ConvertFormsProvider : ICodeActionProvider
{
    private static readonly AstPositionService PositionService = new();

    public Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(
        CodeActionProviderContext context,
        CancellationToken cancellationToken)
    {
        var actions = new SCG.List<CodeAction>();

        if (context.Analysis?.Ast is null || context.SourceText is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(actions);

        var ast = context.Analysis.Ast;
        var sourceText = context.SourceText;
        var (startLine, startCol) = PositionConverter.ToCompiler(context.Range.Start);

        // Sub-action (a): Convert if/elif/else -> match
        TryAddIfToMatchAction(ast, sourceText, startLine, startCol, context, actions);

        // Sub-action (b): Convert match -> if/elif/else
        TryAddMatchToIfAction(ast, sourceText, startLine, startCol, context, actions);

        // Sub-action (c): Add type annotations
        TryAddTypeAnnotationAction(ast, sourceText, startLine, startCol, context, actions);

        // Sub-action (d): Remove type annotations
        TryAddRemoveTypeAnnotationAction(ast, sourceText, startLine, startCol, context, actions);

        // Sub-action (e): Wrap in try/except
        TryAddWrapInTryExceptAction(ast, sourceText, context, actions);

        return Task.FromResult<IReadOnlyList<CodeAction>>(actions);
    }

    #region (a) Convert if/elif/else -> match

    private static void TryAddIfToMatchAction(
        Module ast,
        string sourceText,
        int line,
        int col,
        CodeActionProviderContext context,
        SCG.List<CodeAction> actions)
    {
        var ifStmt = PositionService.FindNodeOfType<IfStatement>(ast, line, col);
        if (ifStmt is null)
            return;

        // All conditions must be equality comparisons against the same variable
        var scrutinee = TryExtractCommonScrutinee(ifStmt);
        if (scrutinee is null)
            return;

        var arms = new SCG.List<(string literalText, ImmutableArray<Statement> body)>();

        // Extract the literal from the if-condition
        var ifLiteral = ExtractLiteralFromEqualityCheck(ifStmt.Test, scrutinee);
        if (ifLiteral is null)
            return;
        arms.Add((FormatLiteral(ifLiteral, sourceText), ifStmt.ThenBody));

        // Extract literals from elif clauses
        foreach (var elif in ifStmt.ElifClauses)
        {
            var elifLiteral = ExtractLiteralFromEqualityCheck(elif.Test, scrutinee);
            if (elifLiteral is null)
                return;
            arms.Add((FormatLiteral(elifLiteral, sourceText), elif.Body));
        }

        // Build the match statement text
        var indent = SharpySourceGenerator.GetIndentation(sourceText, ifStmt.LineStart - 1);
        var indentUnit = SharpySourceGenerator.GetIndentUnit(sourceText);
        var bodyIndent = indent + indentUnit;
        var bodyBodyIndent = bodyIndent + indentUnit;

        var sb = new StringBuilder();
        sb.Append(indent);
        sb.Append("match ");
        sb.Append(scrutinee);
        sb.Append(':');

        foreach (var (literal, body) in arms)
        {
            sb.AppendLine();
            sb.Append(bodyIndent);
            sb.Append("case ");
            sb.Append(literal);
            sb.Append(':');
            AppendBody(sb, body, bodyBodyIndent, sourceText);
        }

        if (!ifStmt.ElseBody.IsDefaultOrEmpty)
        {
            sb.AppendLine();
            sb.Append(bodyIndent);
            sb.Append("case _:");
            AppendBody(sb, ifStmt.ElseBody, bodyBodyIndent, sourceText);
        }

        var editRange = NodeToLspRange(ifStmt);
        var edit = CreateWorkspaceEdit(context.DocumentUri, editRange, sb.ToString());

        actions.Add(new CodeAction
        {
            Title = "Convert to match statement",
            Kind = CodeActionKind.Refactor,
            Edit = edit
        });
    }

    /// <summary>
    /// Checks that every condition in the if/elif chain is an equality check
    /// against the same identifier, and returns that identifier name.
    /// </summary>
    private static string? TryExtractCommonScrutinee(IfStatement ifStmt)
    {
        var name = ExtractScrutineeFromEquality(ifStmt.Test);
        if (name is null)
            return null;

        foreach (var elif in ifStmt.ElifClauses)
        {
            var elifName = ExtractScrutineeFromEquality(elif.Test);
            if (elifName != name)
                return null;
        }

        return name;
    }

    /// <summary>
    /// If the expression is <c>identifier == literal</c> or <c>literal == identifier</c>,
    /// returns the identifier name.
    /// </summary>
    private static string? ExtractScrutineeFromEquality(Expression expr)
    {
        if (expr is not BinaryOp { Operator: BinaryOperator.Equal } binOp)
            return null;

        if (binOp.Left is Identifier leftId && IsLiteralExpression(binOp.Right))
            return leftId.Name;
        if (binOp.Right is Identifier rightId && IsLiteralExpression(binOp.Left))
            return rightId.Name;

        return null;
    }

    /// <summary>
    /// Given an equality check and the scrutinee name, returns the literal side.
    /// </summary>
    private static Expression? ExtractLiteralFromEqualityCheck(Expression expr, string scrutineeName)
    {
        if (expr is not BinaryOp { Operator: BinaryOperator.Equal } binOp)
            return null;

        if (binOp.Left is Identifier { Name: var leftName } && leftName == scrutineeName
            && IsLiteralExpression(binOp.Right))
            return binOp.Right;

        if (binOp.Right is Identifier { Name: var rightName } && rightName == scrutineeName
            && IsLiteralExpression(binOp.Left))
            return binOp.Left;

        return null;
    }

    private static bool IsLiteralExpression(Expression expr)
    {
        return expr is IntegerLiteral or FloatLiteral or StringLiteral
            or BooleanLiteral or NoneLiteral;
    }

    /// <summary>
    /// Formats a literal expression as Sharpy source text.
    /// </summary>
    private static string FormatLiteral(Expression literal, string sourceText)
    {
        return literal switch
        {
            IntegerLiteral il => il.Suffix is not null ? il.Value + il.Suffix : il.Value,
            FloatLiteral fl => fl.Suffix is not null ? fl.Value + fl.Suffix : fl.Value,
            StringLiteral sl => sl.IsRaw
                ? $"r\"{EscapeStringContent(sl.Value)}\""
                : $"\"{EscapeStringContent(sl.Value)}\"",
            BooleanLiteral bl => bl.Value ? "True" : "False",
            NoneLiteral => "None",
            _ => ExtractSourceText(sourceText, literal)
        };
    }

    private static string EscapeStringContent(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    #endregion

    #region (b) Convert match -> if/elif/else

    private static void TryAddMatchToIfAction(
        Module ast,
        string sourceText,
        int line,
        int col,
        CodeActionProviderContext context,
        SCG.List<CodeAction> actions)
    {
        var matchStmt = PositionService.FindNodeOfType<MatchStatement>(ast, line, col);
        if (matchStmt is null)
            return;

        // Only offer if all cases use literal patterns or wildcard (no guards, no complex patterns)
        if (!AllCasesAreLiteralOrWildcard(matchStmt))
            return;

        var indent = SharpySourceGenerator.GetIndentation(sourceText, matchStmt.LineStart - 1);
        var indentUnit = SharpySourceGenerator.GetIndentUnit(sourceText);
        var bodyIndent = indent + indentUnit;
        var scrutineeText = ExtractSourceText(sourceText, matchStmt.Scrutinee);

        var sb = new StringBuilder();
        var isFirst = true;
        var wildcardCase = (MatchCase?)null;

        foreach (var matchCase in matchStmt.Cases)
        {
            if (matchCase.Pattern is WildcardPattern)
            {
                wildcardCase = matchCase;
                continue;
            }

            sb.Append(indent);
            if (isFirst)
            {
                sb.Append("if ");
                isFirst = false;
            }
            else
            {
                sb.Append("elif ");
            }

            sb.Append(scrutineeText);
            sb.Append(" == ");
            sb.Append(FormatPatternAsLiteral(matchCase.Pattern, sourceText));
            sb.Append(':');
            AppendBody(sb, matchCase.Body, bodyIndent, sourceText);
            sb.AppendLine();
        }

        if (wildcardCase is not null)
        {
            sb.Append(indent);
            sb.Append("else:");
            AppendBody(sb, wildcardCase.Body, bodyIndent, sourceText);
        }
        else
        {
            // Remove the trailing newline from the last elif/if block
            while (sb.Length > 0 && sb[sb.Length - 1] is '\r' or '\n')
                sb.Length--;
        }

        var editRange = NodeToLspRange(matchStmt);
        var edit = CreateWorkspaceEdit(context.DocumentUri, editRange, sb.ToString());

        actions.Add(new CodeAction
        {
            Title = "Convert to if/elif/else",
            Kind = CodeActionKind.Refactor,
            Edit = edit
        });
    }

    private static bool AllCasesAreLiteralOrWildcard(MatchStatement matchStmt)
    {
        foreach (var matchCase in matchStmt.Cases)
        {
            if (matchCase.Guard is not null)
                return false;

            if (matchCase.Pattern is not (LiteralPattern or WildcardPattern))
                return false;
        }

        return true;
    }

    private static string FormatPatternAsLiteral(Pattern pattern, string sourceText)
    {
        if (pattern is LiteralPattern lp)
            return FormatLiteral(lp.Literal, sourceText);

        // Wildcard should not reach here
        return "_";
    }

    #endregion

    #region (c) Add type annotations

    private static void TryAddTypeAnnotationAction(
        Module ast,
        string sourceText,
        int line,
        int col,
        CodeActionProviderContext context,
        SCG.List<CodeAction> actions)
    {
        var varDecl = PositionService.FindNodeOfType<VariableDeclaration>(ast, line, col);
        if (varDecl is null)
            return;

        // Only offer when there is no explicit type annotation
        if (varDecl.Type is not null)
            return;

        // Need an initial value to infer the type from
        if (varDecl.InitialValue is null)
            return;

        // Get the inferred type from semantic analysis
        var semanticQuery = context.Analysis?.SemanticQuery;
        if (semanticQuery is null)
            return;

        var inferredType = semanticQuery.GetExpressionType(varDecl.InitialValue);
        if (inferredType is null or UnknownType)
            return;

        var typeAnnotation = SharpySourceGenerator.FormatTypeAnnotation(inferredType);

        // The edit inserts ": <type>" after the variable name.
        // Variable name ends at (LineStart, ColumnStart + Name.Length - 1) in 1-based coords.
        // We insert right after the name.
        var nameEndLsp = PositionConverter.ToLsp(varDecl.LineStart, varDecl.ColumnStart + varDecl.Name.Length);
        var insertRange = new LspRange(nameEndLsp, nameEndLsp);

        var edit = CreateWorkspaceEdit(context.DocumentUri, insertRange, $": {typeAnnotation}");

        actions.Add(new CodeAction
        {
            Title = $"Add type annotation ': {typeAnnotation}'",
            Kind = CodeActionKind.Refactor,
            Edit = edit
        });
    }

    #endregion

    #region (d) Remove type annotations

    private static void TryAddRemoveTypeAnnotationAction(
        Module ast,
        string sourceText,
        int line,
        int col,
        CodeActionProviderContext context,
        SCG.List<CodeAction> actions)
    {
        var varDecl = PositionService.FindNodeOfType<VariableDeclaration>(ast, line, col);
        if (varDecl is null)
            return;

        // Only offer when there IS an explicit type annotation and an initial value
        // (the type can be inferred from the initial value)
        if (varDecl.Type is null || varDecl.InitialValue is null)
            return;

        // The type annotation spans from just after the variable name (the ": type" portion)
        // up to the " = " before the initial value.
        // We need to remove the ": type" part.
        // The colon starts right after the name, and the type ends just before the "=".
        // We find the ": type" by looking at the source text between the name end and "=".
        var lines = sourceText.Split('\n');
        var declLineIndex = varDecl.LineStart - 1;
        if (declLineIndex < 0 || declLineIndex >= lines.Length)
            return;

        var declLine = lines[declLineIndex];

        // Find the colon after the variable name
        var nameEndOffset = varDecl.ColumnStart - 1 + varDecl.Name.Length;
        var colonIndex = declLine.IndexOf(':', nameEndOffset);
        if (colonIndex < 0)
            return;

        // Find the equals sign after the type annotation
        var equalsIndex = declLine.IndexOf('=', colonIndex);
        if (equalsIndex < 0)
            return;

        // The range to remove is from the colon (inclusive) to just before the "=" (exclusive),
        // but we want to keep the space before "=". So remove from colon to equalsIndex,
        // trimming trailing space.
        var removeEnd = equalsIndex;
        while (removeEnd > colonIndex + 1 && declLine[removeEnd - 1] == ' ')
            removeEnd--;

        var removeStart = PositionConverter.ToLsp(varDecl.LineStart, colonIndex + 1);
        var removeEndPos = PositionConverter.ToLsp(varDecl.LineStart, removeEnd + 1);
        var removeRange = new LspRange(removeStart, removeEndPos);

        var edit = CreateWorkspaceEdit(context.DocumentUri, removeRange, "");

        actions.Add(new CodeAction
        {
            Title = "Remove type annotation",
            Kind = CodeActionKind.Refactor,
            Edit = edit
        });
    }

    #endregion

    #region (e) Wrap in try/except

    private static void TryAddWrapInTryExceptAction(
        Module ast,
        string sourceText,
        CodeActionProviderContext context,
        SCG.List<CodeAction> actions)
    {
        // Only offer when the user has selected one or more statements
        var selectedStatements = SelectionAnalyzer.FindSelectedStatements(
            ast, sourceText, context.Range);

        if (selectedStatements.Count == 0)
            return;

        // Don't offer if the selection is just a cursor (zero-width)
        if (SelectionAnalyzer.IsZeroWidthSelection(context.Range))
            return;

        var firstStmt = selectedStatements[0];
        var lastStmt = selectedStatements[selectedStatements.Count - 1];

        var indent = SharpySourceGenerator.GetIndentation(sourceText, firstStmt.LineStart - 1);
        var indentUnit = SharpySourceGenerator.GetIndentUnit(sourceText);
        var bodyIndent = indent + indentUnit;

        var sb = new StringBuilder();
        sb.Append(indent);
        sb.Append("try:");
        sb.AppendLine();

        // Re-indent the selected statements under the try block
        foreach (var stmt in selectedStatements)
        {
            var stmtText = ExtractStatementText(sourceText, stmt);
            var stmtLines = stmtText.Split('\n');
            foreach (var stmtLine in stmtLines)
            {
                sb.Append(bodyIndent);
                sb.AppendLine(stmtLine.TrimStart());
            }
        }

        sb.Append(indent);
        sb.Append("except Exception as e:");
        sb.AppendLine();
        sb.Append(bodyIndent);
        sb.AppendLine("raise");

        var editRange = new LspRange(
            PositionConverter.ToLsp(firstStmt.LineStart, 1),
            PositionConverter.ToLsp(lastStmt.LineEnd + 1, 1));
        var edit = CreateWorkspaceEdit(context.DocumentUri, editRange, sb.ToString());

        actions.Add(new CodeAction
        {
            Title = "Wrap in try/except",
            Kind = CodeActionKind.Refactor,
            Edit = edit
        });
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Appends a statement body with proper indentation. Each statement's source text
    /// is extracted from the original source and re-indented.
    /// </summary>
    private static void AppendBody(
        StringBuilder sb,
        ImmutableArray<Statement> body,
        string bodyIndent,
        string sourceText)
    {
        if (body.IsDefaultOrEmpty)
        {
            sb.AppendLine();
            sb.Append(bodyIndent);
            sb.Append("pass");
            return;
        }

        foreach (var stmt in body)
        {
            var stmtText = ExtractStatementText(sourceText, stmt);
            var stmtLines = stmtText.Split('\n');
            foreach (var stmtLine in stmtLines)
            {
                var trimmed = stmtLine.TrimStart();
                if (trimmed.Length == 0)
                    continue;
                sb.AppendLine();
                sb.Append(bodyIndent);
                sb.Append(trimmed);
            }
        }
    }

    /// <summary>
    /// Extracts the source text for a node using its line/column span.
    /// </summary>
    private static string ExtractSourceText(string sourceText, Node node)
    {
        var lines = sourceText.Split('\n');
        if (node.LineStart < 1 || node.LineEnd < 1
            || node.LineStart > lines.Length || node.LineEnd > lines.Length)
            return "";

        if (node.LineStart == node.LineEnd)
        {
            var line = lines[node.LineStart - 1];
            var start = System.Math.Max(0, node.ColumnStart - 1);
            var end = System.Math.Min(line.Length, node.ColumnEnd - 1);
            if (start >= end)
                return line.Substring(start).TrimEnd('\r');
            return line.Substring(start, end - start).TrimEnd('\r');
        }

        // Multi-line extraction
        var sb = new StringBuilder();
        for (var i = node.LineStart - 1; i <= node.LineEnd - 1 && i < lines.Length; i++)
        {
            var line = lines[i];
            if (i == node.LineStart - 1)
            {
                var start = System.Math.Max(0, node.ColumnStart - 1);
                sb.Append(line.Substring(start).TrimEnd('\r'));
            }
            else if (i == node.LineEnd - 1)
            {
                var end = System.Math.Min(line.Length, node.ColumnEnd - 1);
                sb.Append('\n');
                sb.Append(line.Substring(0, end).TrimEnd('\r'));
            }
            else
            {
                sb.Append('\n');
                sb.Append(line.TrimEnd('\r'));
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Extracts the full source text for a statement, including all its lines.
    /// </summary>
    private static string ExtractStatementText(string sourceText, Statement stmt)
    {
        return ExtractSourceText(sourceText, stmt);
    }

    /// <summary>
    /// Converts an AST node's span to an LSP Range.
    /// </summary>
    private static LspRange NodeToLspRange(Node node)
    {
        return new LspRange(
            PositionConverter.ToLsp(node.LineStart, node.ColumnStart),
            PositionConverter.ToLsp(node.LineEnd, node.ColumnEnd));
    }

    /// <summary>
    /// Creates a WorkspaceEdit with a single text edit.
    /// </summary>
    private static WorkspaceEdit CreateWorkspaceEdit(
        OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri uri,
        LspRange range,
        string newText)
    {
        return new WorkspaceEdit
        {
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [uri] = new[] { new TextEdit { Range = range, NewText = newText } }
            }
        };
    }

    #endregion
}
