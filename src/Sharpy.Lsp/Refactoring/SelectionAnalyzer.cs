using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Services;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Maps LSP selection ranges to AST nodes for refactoring operations.
/// Bridges the gap between LSP 0-based positions and the compiler's 1-based AST spans.
/// </summary>
internal static class SelectionAnalyzer
{
    private static readonly AstPositionService PositionService = new();

    /// <summary>
    /// Finds the best-matching expression for a given LSP selection range.
    /// Returns the most specific expression whose span matches or is contained within the selection.
    /// </summary>
    /// <param name="ast">The parsed module AST.</param>
    /// <param name="sourceText">The full source text of the document (unused currently, reserved for future span-based matching).</param>
    /// <param name="selection">The LSP selection range (0-based).</param>
    /// <returns>The best-matching expression, or null if no expression matches the selection.</returns>
    public static Expression? FindSelectedExpression(Module ast, string sourceText, LspRange selection)
    {
        if (ast is null)
            return null;

        var (startLine, startCol) = PositionConverter.ToCompiler(selection.Start);
        var (endLine, endCol) = PositionConverter.ToCompiler(selection.End);

        // Find all nodes at the start position
        var nodesAtStart = PositionService.FindAllContainingNodes(ast, startLine, startCol);

        // Filter to expressions whose span fits within the selection
        Expression? bestMatch = null;

        foreach (var node in nodesAtStart)
        {
            if (node is not Expression expr)
                continue;

            // Check if this expression fits within the selection range.
            // For a zero-width cursor (start == end), any expression at that position qualifies.
            // For a range selection, the expression must be fully contained within the selection.
            if (IsZeroWidthSelection(selection))
            {
                // Cursor selection: prefer the innermost expression at this position.
                // Since FindAllContainingNodes returns outermost-to-innermost,
                // we keep overwriting to get the innermost.
                bestMatch = expr;
            }
            else if (IsNodeContainedInSelection(expr, startLine, startCol, endLine, endCol))
            {
                // Range selection: find the largest expression fully contained in the selection.
                // Prefer a tighter match (the one that covers the most of the selection
                // while still being fully contained).
                if (bestMatch is null || IsNodeLargerThan(expr, bestMatch))
                {
                    bestMatch = expr;
                }
            }
        }

        // If we found a match from the start-position walk, return it.
        if (bestMatch is not null)
            return bestMatch;

        // For range selections, also try nodes at the end position in case
        // the start lands between nodes.
        if (!IsZeroWidthSelection(selection))
        {
            var nodesAtEnd = PositionService.FindAllContainingNodes(ast, endLine, endCol);
            foreach (var node in nodesAtEnd)
            {
                if (node is Expression expr &&
                    IsNodeContainedInSelection(expr, startLine, startCol, endLine, endCol))
                {
                    if (bestMatch is null || IsNodeLargerThan(expr, bestMatch))
                    {
                        bestMatch = expr;
                    }
                }
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Finds contiguous statements that are fully contained within the given LSP selection range.
    /// Walks the module body and nested function/class/struct bodies to find the appropriate scope.
    /// </summary>
    /// <param name="ast">The parsed module AST.</param>
    /// <param name="sourceText">The full source text of the document (unused currently, reserved for future use).</param>
    /// <param name="selection">The LSP selection range (0-based).</param>
    /// <returns>A list of statements fully contained within the selection, in source order.</returns>
    public static IReadOnlyList<Statement> FindSelectedStatements(Module ast, string sourceText, LspRange selection)
    {
        if (ast is null)
            return Array.Empty<Statement>();

        var (startLine, startCol) = PositionConverter.ToCompiler(selection.Start);
        var (endLine, endCol) = PositionConverter.ToCompiler(selection.End);

        // Find the deepest statement body that contains the selection start
        var body = FindContainingBody(ast, startLine, startCol);

        // Collect statements from that body that are fully within the selection
        var selected = new SCG.List<Statement>();
        var inRange = false;

        foreach (var stmt in body)
        {
            if (IsNodeContainedInSelection(stmt, startLine, startCol, endLine, endCol))
            {
                inRange = true;
                selected.Add(stmt);
            }
            else if (inRange)
            {
                // Once we leave the range, stop (contiguous only)
                break;
            }
        }

        return selected;
    }

    /// <summary>
    /// Finds the FunctionDef that contains the given position.
    /// </summary>
    /// <param name="ast">The parsed module AST.</param>
    /// <param name="line">1-based line number.</param>
    /// <param name="col">1-based column number.</param>
    /// <returns>The containing FunctionDef, or null if the position is not inside a function.</returns>
    public static FunctionDef? FindContainingFunction(Module ast, int line, int col)
    {
        if (ast is null)
            return null;

        return PositionService.FindNodeOfType<FunctionDef>(ast, line, col);
    }

    /// <summary>
    /// Finds the class or struct definition that contains the given position.
    /// </summary>
    /// <param name="ast">The parsed module AST.</param>
    /// <param name="line">1-based line number.</param>
    /// <param name="col">1-based column number.</param>
    /// <returns>The containing ClassDef or StructDef as a Statement, or null if not inside either.</returns>
    public static Statement? FindContainingClass(Module ast, int line, int col)
    {
        if (ast is null)
            return null;

        // Try ClassDef first, then StructDef, and return whichever is innermost
        var classDef = PositionService.FindNodeOfType<ClassDef>(ast, line, col);
        var structDef = PositionService.FindNodeOfType<StructDef>(ast, line, col);

        if (classDef is null)
            return structDef;
        if (structDef is null)
            return classDef;

        // Both found: return whichever is more deeply nested (has a later start or is contained in the other)
        if (IsNodeContainedIn(structDef, classDef))
            return structDef;

        return classDef;
    }

    /// <summary>
    /// Finds the statement body (list of statements) in the deepest scope containing the given position.
    /// Walks into FunctionDef, ClassDef, StructDef, IfStatement, ForStatement, WhileStatement, etc.
    /// </summary>
    private static IReadOnlyList<Statement> FindContainingBody(Module ast, int line, int col)
    {
        // Start with the module body
        IReadOnlyList<Statement> currentBody = ast.Body;

        while (true)
        {
            var foundDeeper = false;

            foreach (var stmt in currentBody)
            {
                if (!ContainsPosition(stmt, line, col))
                    continue;

                // Try to descend into a nested body
                var nestedBody = GetStatementBody(stmt);
                if (nestedBody is not null && nestedBody.Count > 0)
                {
                    currentBody = nestedBody;
                    foundDeeper = true;
                    break;
                }
            }

            if (!foundDeeper)
                break;
        }

        return currentBody;
    }

    /// <summary>
    /// Gets the statement body of a compound statement, if it has one and
    /// the position falls within it.
    /// </summary>
    private static IReadOnlyList<Statement>? GetStatementBody(Statement stmt)
    {
        return stmt switch
        {
            FunctionDef fd => fd.Body,
            ClassDef cd => cd.Body,
            StructDef sd => sd.Body,
            InterfaceDef id => id.Body,
            IfStatement ifs => ifs.ThenBody,
            ForStatement fs => fs.Body,
            WhileStatement ws => ws.Body,
            TryStatement ts => ts.Body,
            WithStatement wis => wis.Body,
            _ => null
        };
    }

    /// <summary>
    /// Checks whether an AST node is fully contained within the given selection range.
    /// All coordinates are 1-based.
    /// </summary>
    private static bool IsNodeContainedInSelection(
        Node node, int selStartLine, int selStartCol, int selEndLine, int selEndCol)
    {
        // Node must have valid span
        if (node.LineStart == 0 && node.LineEnd == 0)
            return false;

        // Node start must be at or after selection start
        if (node.LineStart < selStartLine)
            return false;
        if (node.LineStart == selStartLine && node.ColumnStart < selStartCol)
            return false;

        // Node end must be at or before selection end
        if (node.LineEnd > selEndLine)
            return false;
        if (node.LineEnd == selEndLine && node.ColumnEnd > selEndCol)
            return false;

        return true;
    }

    /// <summary>
    /// Checks whether inner is fully contained within outer.
    /// </summary>
    private static bool IsNodeContainedIn(Node inner, Node outer)
    {
        return IsNodeContainedInSelection(
            inner,
            outer.LineStart, outer.ColumnStart,
            outer.LineEnd, outer.ColumnEnd);
    }

    /// <summary>
    /// Checks whether node A spans a larger region than node B.
    /// Used to pick the best (largest fully-contained) expression match.
    /// </summary>
    private static bool IsNodeLargerThan(Node a, Node b)
    {
        var aLines = a.LineEnd - a.LineStart;
        var bLines = b.LineEnd - b.LineStart;

        if (aLines != bLines)
            return aLines > bLines;

        // Same number of lines: compare column span
        var aSpan = (a.LineEnd == a.LineStart)
            ? a.ColumnEnd - a.ColumnStart
            : a.ColumnEnd; // multi-line: end column is sufficient approximation
        var bSpan = (b.LineEnd == b.LineStart)
            ? b.ColumnEnd - b.ColumnStart
            : b.ColumnEnd;

        return aSpan > bSpan;
    }

    /// <summary>
    /// Checks if the selection is a zero-width cursor position (start == end).
    /// </summary>
    private static bool IsZeroWidthSelection(LspRange selection)
    {
        return selection.Start.Line == selection.End.Line &&
               selection.Start.Character == selection.End.Character;
    }

    /// <summary>
    /// Checks if a node's span contains the given 1-based position.
    /// </summary>
    private static bool ContainsPosition(Node node, int line, int column)
    {
        if (node.LineStart == 0 && node.LineEnd == 0)
            return false;

        if (line < node.LineStart || line > node.LineEnd)
            return false;

        if (line == node.LineStart && column < node.ColumnStart)
            return false;

        if (line == node.LineEnd && column > node.ColumnEnd)
            return false;

        return true;
    }
}
