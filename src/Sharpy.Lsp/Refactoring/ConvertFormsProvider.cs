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

        var source = SourceLines.Of(sourceText);
        var clauses = new SCG.List<Clause>();

        // Extract the literal from the if-condition
        var ifLiteral = ExtractLiteralFromEqualityCheck(ifStmt.Test, scrutinee);
        if (ifLiteral is null)
            return;
        clauses.Add(new Clause(ifStmt.LineStart, HeaderEnd(ifStmt.LineStart, ifStmt.HeaderLineEnd),
            $"case {FormatLiteral(ifLiteral, source)}:", ifStmt.ThenBody));

        // Extract literals from elif clauses
        foreach (var elif in ifStmt.ElifClauses)
        {
            var elifLiteral = ExtractLiteralFromEqualityCheck(elif.Test, scrutinee);
            if (elifLiteral is null)
                return;
            clauses.Add(new Clause(elif.LineStart, HeaderEnd(elif.LineStart, elif.HeaderLineEnd),
                $"case {FormatLiteral(elifLiteral, source)}:", elif.Body));
        }

        if (!ifStmt.ElseBody.IsDefaultOrEmpty)
        {
            if (ifStmt.ElseHeaderLine < 1)
                return;
            clauses.Add(new Clause(ifStmt.ElseHeaderLine, ifStmt.ElseHeaderLine, "case _:", ifStmt.ElseBody));
        }

        // D6 + D2: only the headers are re-spelled; each body is the region of lines between its header and
        // the next one (the last through the statement's TRUE last line), moved one indent unit right.
        var first = ifStmt.LineStart;
        var last = source.EndLine(ifStmt);
        if (!source.Contains(first) || !source.Contains(last))
            return;

        var indent = source.Indentation(first);
        var unit = source.IndentUnit;
        var newLines = new SCG.List<string> { indent + "match " + scrutinee + ":" };
        for (var i = 0; i < clauses.Count; i++)
        {
            var bodyEnd = i + 1 < clauses.Count ? clauses[i + 1].HeaderStart - 1 : last;
            EmitClause(source, clauses[i], bodyEnd, indent + unit, unit.Length, unit[0], newLines);
        }

        var textEdit = source.WholeLineEdit(first, last, newLines);
        var edit = CreateWorkspaceEdit(context.DocumentUri, textEdit.Range, textEdit.NewText);

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
    /// Formats a literal expression as Sharpy source text. Rendering only (documented-by-design):
    /// the caller admits only the literal kinds <c>IsSimpleLiteral</c> lists, so the switch is
    /// deliberately partial and no semantic decision keys on it.
    /// </summary>
    private static string FormatLiteral(Expression literal, SourceLines source)
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
            _ => ExtractSourceText(source, literal)
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

        var source = SourceLines.Of(sourceText);
        var first = matchStmt.LineStart;
        var last = source.EndLine(matchStmt);
        if (!source.Contains(first) || !source.Contains(last))
            return;

        var indent = source.Indentation(first);
        var scrutineeText = ExtractSourceText(source, matchStmt.Scrutinee);
        var matchHeaderEnd = HeaderEnd(first, matchStmt.HeaderLineEnd);

        // Each case's body region runs to the next case header in DOCUMENT order (the last through the
        // statement's true last line); the wildcard case is written last, as `else:`, with its region.
        var cases = matchStmt.Cases;
        var bodyEnds = new int[cases.Length];
        for (var i = 0; i < cases.Length; i++)
            bodyEnds[i] = i + 1 < cases.Length ? cases[i + 1].LineStart - 1 : last;

        var order = Enumerable.Range(0, cases.Length).Where(i => cases[i].Pattern is not WildcardPattern)
            .Concat(Enumerable.Range(0, cases.Length).Where(i => cases[i].Pattern is WildcardPattern))
            .ToList();

        // D6, CORRECTED carrier: the `match` header's comments have no re-spelled counterpart, so they are
        // written as own-line comments at the statement's indent above the new `if`; the lines between the
        // `match` header and the first case (comments, blank lines at the case column) follow, moved with the
        // cases' offset.
        var caseIndent = source.Indentation(cases[0].LineStart);
        var delta = indent.Length - caseIndent.Length;
        var newLines = new SCG.List<string>();
        foreach (var comment in source.CommentsOn(first, matchHeaderEnd))
            newLines.Add(indent + comment.Text);
        if (cases[0].LineStart - 1 > matchHeaderEnd)
            newLines.AddRange(Region.Reindent(source.Slice(matchHeaderEnd + 1, cases[0].LineStart - 1), delta, source.LiteralLines));

        var arm = 0;
        foreach (var i in order)
        {
            var matchCase = cases[i];
            var header = matchCase.Pattern is WildcardPattern
                ? "else:"
                : $"{(arm++ == 0 ? "if" : "elif")} {scrutineeText} == {FormatPatternAsLiteral(matchCase.Pattern, source)}:";
            var clause = new Clause(matchCase.LineStart, HeaderEnd(matchCase.LineStart, matchCase.HeaderLineEnd), header, matchCase.Body);
            EmitClause(source, clause, bodyEnds[i], indent, delta, ' ', newLines);
        }

        var textEdit = source.WholeLineEdit(first, last, newLines);
        var edit = CreateWorkspaceEdit(context.DocumentUri, textEdit.Range, textEdit.NewText);

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

    private static string FormatPatternAsLiteral(Pattern pattern, SourceLines source)
    {
        if (pattern is LiteralPattern lp)
            return FormatLiteral(lp.Literal, source);

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

        var nameEndLsp = PositionConverter.ToLsp(varDecl.NameLineStart, varDecl.NameColumnEnd);
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
        var source = SourceLines.Of(sourceText);
        if (!source.Contains(varDecl.LineStart))
            return;

        var declLine = source.Line(varDecl.LineStart);

        var nameEndOffset = varDecl.NameColumnEnd - 1;
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

        // D7: the selected statements' whole lines — through the last one's TRUE last line, comments,
        // blank lines and continuation lines included — move one indent unit right under `try:`; a line
        // inside a multi-line literal keeps its bytes (D3). Nothing is re-rendered.
        var source = SourceLines.Of(sourceText);
        var first = firstStmt.LineStart;
        var last = source.EndLine(lastStmt);
        if (!source.Contains(first) || !source.Contains(last) || last < first)
            return;

        var indent = source.Indentation(first);
        var indentUnit = source.IndentUnit;
        var bodyIndent = indent + indentUnit;

        var newLines = new SCG.List<string> { indent + "try:" };
        newLines.AddRange(Region.Reindent(source.Slice(first, last), indentUnit.Length, source.LiteralLines, indentUnit[0]));
        newLines.Add(indent + "except Exception as e:");
        newLines.Add(bodyIndent + "raise");

        var textEdit = source.WholeLineEdit(first, last, newLines);
        var edit = CreateWorkspaceEdit(context.DocumentUri, textEdit.Range, textEdit.NewText);

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
    /// One clause of a converted statement: its header's source lines (<see cref="HeaderStart"/> through the
    /// line of its colon), the re-spelled header text (no indentation) and its body statements.
    /// </summary>
    private sealed record Clause(int HeaderStart, int HeaderEnd, string NewHeader, ImmutableArray<Statement> Body);

    /// <summary>The line of a header's colon, or its first line when the parser recorded none.</summary>
    private static int HeaderEnd(int headerStart, int headerLineEnd) => headerLineEnd >= headerStart ? headerLineEnd : headerStart;

    /// <summary>
    /// Writes one clause (D6): its re-spelled header at <paramref name="headerIndent"/> carrying the inline
    /// comment of its colon line as written there, every other comment of its header lines as an own-line
    /// comment just above it, then its body REGION — the source lines after the header through
    /// <paramref name="bodyEnd"/>, comments and blank lines included — moved by ONE offset
    /// (<paramref name="delta"/> characters of <paramref name="fill"/>; literal lines byte-identical, D3).
    /// A body on the header line itself (<c>if x == 1: f()</c>) becomes the first body line, verbatim with its
    /// comment, at the header indent plus one unit; a clause with no body line gets <c>pass</c>.
    /// </summary>
    private static void EmitClause(
        SourceLines source, Clause clause, int bodyEnd, string headerIndent, int delta, char fill, SCG.List<string> into)
    {
        var bodyIndent = headerIndent + source.IndentUnit;
        var inlineBody = !clause.Body.IsDefaultOrEmpty && clause.Body[0].LineStart <= clause.HeaderEnd;
        var colonComment = inlineBody ? null : source.InlineComment(clause.HeaderEnd);

        foreach (var comment in source.CommentsOn(clause.HeaderStart, clause.HeaderEnd))
        {
            if (inlineBody && comment.Line == clause.HeaderEnd)
                continue; // the inline body's own comment travels with it
            if (colonComment is not null && comment.Position == colonComment.Position)
                continue;
            into.Add(headerIndent + comment.Text);
        }

        into.Add(headerIndent + clause.NewHeader + (colonComment is null ? "" : source.AttachedComment(clause.HeaderEnd)));

        var regionStart = clause.HeaderEnd + 1;
        if (inlineBody)
        {
            var content = source.Line(clause.HeaderEnd);
            into.Add(bodyIndent + content[Math.Min(content.Length, Math.Max(0, clause.Body[0].ColumnStart - 1))..]);
        }
        else if (regionStart > bodyEnd)
        {
            into.Add(bodyIndent + "pass");
        }

        if (regionStart <= bodyEnd)
            into.AddRange(Region.Reindent(source.Slice(regionStart, bodyEnd), delta, source.LiteralLines, fill));
    }

    /// <summary>
    /// The source text of a node, one piece per source line it spans — from <c>ColumnStart</c> on its
    /// first line through <c>ColumnEnd</c> on its last — read from the ONE line table.
    /// </summary>
    private static SCG.List<string> NodeLinePieces(SourceLines source, Node node)
    {
        if (node.LineStart < 1 || node.LineEnd < 1
            || !source.Contains(node.LineStart) || !source.Contains(node.LineEnd))
            return new SCG.List<string> { "" };

        if (node.LineStart == node.LineEnd)
        {
            var line = source.Line(node.LineStart);
            var start = System.Math.Max(0, node.ColumnStart - 1);
            var end = System.Math.Min(line.Length, node.ColumnEnd - 1);
            if (start >= end)
                return new SCG.List<string> { line.Substring(start) };
            return new SCG.List<string> { line.Substring(start, end - start) };
        }

        // Multi-line extraction
        var pieces = new SCG.List<string>();
        for (var i = node.LineStart; i <= node.LineEnd; i++)
        {
            var line = source.Line(i);
            if (i == node.LineStart)
                pieces.Add(line.Substring(System.Math.Max(0, node.ColumnStart - 1)));
            else if (i == node.LineEnd)
                pieces.Add(line.Substring(0, System.Math.Min(line.Length, node.ColumnEnd - 1)));
            else
                pieces.Add(line);
        }
        return pieces;
    }

    /// <summary>
    /// Extracts the source text for a node using its line/column span, its lines joined by the
    /// document's line break.
    /// </summary>
    private static string ExtractSourceText(SourceLines source, Node node) =>
        string.Join(source.DocumentLineBreak, NodeLinePieces(source, node));

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
