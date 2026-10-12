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
        if (ifLiteral is null || FormatLiteral(ifLiteral, source) is not { } ifLiteralText)
            return;
        clauses.Add(new Clause(ifStmt.LineStart, HeaderEnd(ifStmt.LineStart, ifStmt.HeaderLineEnd),
            $"case {ifLiteralText}:", ifStmt.ThenBody));

        // Extract literals from elif clauses
        foreach (var elif in ifStmt.ElifClauses)
        {
            var elifLiteral = ExtractLiteralFromEqualityCheck(elif.Test, scrutinee);
            if (elifLiteral is null || FormatLiteral(elifLiteral, source) is not { } elifLiteralText)
                return;
            clauses.Add(new Clause(elif.LineStart, HeaderEnd(elif.LineStart, elif.HeaderLineEnd),
                $"case {elifLiteralText}:", elif.Body));
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
    /// Formats a literal expression as Sharpy source text. Rendering only (documented-by-design): the
    /// literal kinds <c>IsLiteralExpression</c> lists are re-spelled; any other node a literal pattern
    /// can hold (a negative number, `case -1:`, is a <see cref="UnaryOp"/>) is its verbatim source text
    /// read from the ONE seam (<see cref="SourceLines.NodeText"/>, plan D1) — null when that node is not
    /// on the document's lines, and the caller then offers nothing. No semantic decision keys on it.
    /// </summary>
    private static string? FormatLiteral(Expression literal, SourceLines source)
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
            _ => source.NodeText(literal)
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

        // A match of only `case _:` has no `if` to begin the chain — a bare `else:` does not parse, and any
        // other spelling re-renders the program: no legal text exists, so the action is not offered.
        if (matchStmt.Cases.All(c => c.Pattern is WildcardPattern))
            return;

        var source = SourceLines.Of(sourceText);
        var first = matchStmt.LineStart;
        var last = source.EndLine(matchStmt);
        if (!source.Contains(first) || !source.Contains(last))
            return;

        var indent = source.Indentation(first);
        // The scrutinee's verbatim text from the ONE seam (D1): a multi-line string literal's tail lines come
        // with it (the parser's ColumnEnd for a multi-line last token is on its START line, #2295, which a
        // ColumnStart..ColumnEnd slice read as `"""a` — the header did not parse).
        if (source.NodeText(matchStmt.Scrutinee) is not { } scrutineeText)
            return;
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
        // The scrutinee's text is written into every `if`/`elif` header: a comment inside it would be written
        // once per arm — with more than one arm no legal text keeps it once, so the action is not offered; with
        // one arm it travels inside the scrutinee and is not repeated above (P47, #2232).
        var scrutineeSpan = source.NodeSpan(matchStmt.Scrutinee);
        if (scrutineeSpan is { } span && source.HasCommentIn(span.Start, span.End)
            && cases.Count(c => c.Pattern is not WildcardPattern) > 1)
            return;

        var newLines = new SCG.List<string>();
        foreach (var comment in source.CommentsOn(first, matchHeaderEnd))
        {
            if (scrutineeSpan is { } s && comment.Position >= s.Start && comment.Position < s.End)
                continue;
            newLines.Add(indent + comment.Text);
        }
        if (cases[0].LineStart - 1 > matchHeaderEnd)
            newLines.AddRange(Region.Reindent(source.Slice(matchHeaderEnd + 1, cases[0].LineStart - 1), delta, source.LiteralLines));

        var arm = 0;
        foreach (var i in order)
        {
            var matchCase = cases[i];
            if (FormatPatternAsLiteral(matchCase.Pattern, source) is not { } literalText)
                return;
            var header = matchCase.Pattern is WildcardPattern
                ? "else:"
                : $"{(arm++ == 0 ? "if" : "elif")} {scrutineeText} == {literalText}:";
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

    private static string? FormatPatternAsLiteral(Pattern pattern, SourceLines source)
    {
        if (pattern is LiteralPattern lp)
            return FormatLiteral(lp.Literal, source);

        // A wildcard's header is `else:`; its text is never written
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

        // A decorated field declaration needs its annotation — without it the decorator precedes a bare
        // assignment, which does not parse (SPY0105): no legal text exists, so the action is not offered.
        if (!varDecl.Decorators.IsDefaultOrEmpty
            || (PositionService.FindNodeOfType<DecoratedStatement>(ast, line, col) is { } decorated
                && ReferenceEquals(decorated.Statement, varDecl)))
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
            ast, sourceText, context.Range, out var owner);

        if (selectedStatements.Count == 0)
            return;

        // A try block cannot stand in a type body, a module-level declaration inside one is no longer a
        // declaration, and a nested declaration inside one is scoped to it (a read after it is undefined): no
        // legal text exists there, so the action is not offered.
        if (!SelectionAnalyzer.CanMoveIntoBlock(ast, sourceText, owner, selectedStatements)
            || !SourceLines.Of(sourceText).BeginsItsLine(selectedStatements[0]))
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
