using System.Collections.Immutable;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Lexer;
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
    /// <param name="sourceText">The full source text of the document: coverage is judged on its offsets and tokens.</param>
    /// <param name="selection">The LSP selection range (0-based).</param>
    /// <returns>A list of statements fully contained within the selection, in source order.</returns>
    public static IReadOnlyList<Statement> FindSelectedStatements(Module ast, string sourceText, LspRange selection)
        => FindSelectedStatements(ast, sourceText, selection, out _);

    /// <summary>
    /// <see cref="FindSelectedStatements(Module, string, LspRange)"/>, also returning the statement that owns
    /// the body the statements were taken from (null for the module body).
    /// </summary>
    public static IReadOnlyList<Statement> FindSelectedStatements(Module ast, string sourceText, LspRange selection, out Statement? owner)
    {
        owner = null;
        if (ast is null)
            return Array.Empty<Statement>();

        var (startLine, startCol) = PositionConverter.ToCompiler(selection.Start);
        var (endLine, endCol) = PositionConverter.ToCompiler(selection.End);

        // A statement is COVERED by the selection when the selection starts at or before its first character and
        // ends at or after its last code character (Extent.StatementEndOffset — trailing whitespace and comments
        // do not count). The parser's LineEnd/ColumnEnd for a block's last statement runs on to the next token,
        // so a selection ending exactly at `    return result` used to drop that statement (P47, #2232).
        bool Covers(Statement stmt) => sourceText is null
            ? IsNodeContainedInSelection(stmt, startLine, startCol, endLine, endCol)
            : CoversByText(SourceLines.Of(sourceText), stmt, selection);

        // Find the deepest statement body that contains the selection start — but never descend into a
        // statement the selection holds whole: selecting `interface I: …` from its first column selects the
        // interface, not its members (P47, #2232).
        var body = FindContainingBody(ast, startLine, startCol, Covers, out owner);

        // Collect statements from that body that are fully within the selection
        var selected = new SCG.List<Statement>();
        var inRange = false;

        foreach (var stmt in body)
        {
            if (Covers(stmt))
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

        // A selection holding ONE whole function where no block may hold a def (the module, a class body) means
        // the function's body: wrapping or extracting the def itself has no legal text, its statements do — the
        // base's reading of a whole-def selection, which a statement it failed to cover had hidden (P47, #2232).
        // Only a body of its own lines: a bodiless `def f(self) -> T` (abstract, interface) or a stub
        // `def f(): ...` has none, and stays refused.
        if (selected.Count == 1 && selected[0].UnwrapDecorated() is FunctionDef function && !BlockMayHold(owner, selected)
            && function.Body.Length > 0 && function.HeaderLineEnd > 0 && function.Body[0].LineStart > function.HeaderLineEnd
            && function.Body.All(Covers))
        {
            owner = function;
            return function.Body;
        }

        return selected;
    }

    /// <summary>
    /// True when <paramref name="statements"/>, taken from the body <paramref name="owner"/> owns (null = the
    /// module), may be moved into a statement block — a <c>try:</c> body or a new function's body — and still
    /// mean what they meant (P47, #2232). A type body (class, struct, interface, enum) holds members, not
    /// statements: no block may stand there. At module level a declaration (a def, a type, an import, a
    /// module variable or const) moved into a block stops being a declaration (SPY0340 / SPY0202). Inside a
    /// function a declaration moved into a block is scoped to that block, so a declaration whose name is read
    /// after the region would leave that read undefined (SPY0200; chore R-GP, #2296), and a declaration's
    /// attached decorators sit above its first line, outside the lines a mover takes, so the first selected
    /// statement may not carry one (SPY0105 on the decorator left behind). Where no legal text exists the
    /// action is not offered.
    /// </summary>
    public static bool CanMoveIntoBlock(Module ast, string sourceText, Statement? owner, IReadOnlyList<Statement> statements)
        => BlockMayHold(owner, statements) && !LeavesADecoratorBehind(statements)
           && !DeclaresANameReadAfter(ast, sourceText, statements);

    /// <summary>
    /// The structural half of <see cref="CanMoveIntoBlock"/>: false in a type body, and at module level for a
    /// selection holding a module declaration.
    /// </summary>
    private static bool BlockMayHold(Statement? owner, IReadOnlyList<Statement> statements)
    {
        if (owner is ClassDef or StructDef or InterfaceDef or EnumDef)
            return false;
        return owner is not null || !statements.Any(IsModuleDeclaration);
    }

    /// <summary>
    /// True when one of <paramref name="statements"/> declares a name (a def, a type alias, a type) that is
    /// read after the region, before the end of the function holding it (the module when none does). The
    /// reads are the lexer's identifier tokens, so a read in a type annotation, an f-string hole or a nested
    /// function counts; a name after a <c>.</c> is a member, not a read. True as well when the region's end
    /// cannot be read from the tokens: without that fact the action cannot be judged safe.
    /// </summary>
    private static bool DeclaresANameReadAfter(Module ast, string sourceText, IReadOnlyList<Statement> statements)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var statement in statements)
        {
            if (DeclaredName(statement.UnwrapDecorated()) is { Length: > 0 } name)
                names.Add(name);
        }

        if (names.Count == 0)
            return false;

        var source = SourceLines.Of(sourceText);
        var tokens = source.Tokens;
        if (Extent.StatementEndOffset(statements[^1], tokens, source) is not { } regionEnd)
            return true;

        var scopeEnd = sourceText.Length;
        if (EnclosingFunction(ast, statements) is { } function)
        {
            if (Extent.StatementEndOffset(function, tokens, source) is not { } functionEnd)
                return true;
            scopeEnd = functionEnd;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Position < regionEnd)
                continue;
            if (token.Position >= scopeEnd)
                break;
            if (token.Type == TokenType.Identifier && names.Contains(token.Value)
                && (i == 0 || tokens[i - 1].Type != TokenType.Dot))
                return true;
        }

        return false;
    }

    /// <summary>The name a def or a type declaration (<see cref="StatementExtensions.TryGetNestedDeclaration"/>) binds; null for any other statement.</summary>
    private static string? DeclaredName(Statement statement) =>
        statement is FunctionDef function ? function.Name
        : statement.TryGetNestedDeclaration(out var declaration) ? declaration.Name
        : null;

    /// <summary>
    /// True when the first of <paramref name="statements"/> carries a decorator written above its first line:
    /// the parser attaches a def's, a type's, a property's, an event's or a field's decorators to it while its
    /// <c>LineStart</c> is the declaration's own line, and the movers take lines from there. A
    /// <see cref="DecoratedStatement"/> (statement-scoped <c>@suppress</c>) starts at its decorator and is whole.
    /// </summary>
    private static bool LeavesADecoratorBehind(IReadOnlyList<Statement> statements)
    {
        var first = statements[0];
        var decorators = first switch
        {
            FunctionDef f => f.Decorators,
            VariableDeclaration v => v.Decorators,
            PropertyDef p => p.Decorators,
            EventDef e => e.Decorators,
            _ when first.TryGetNestedDeclaration(out var declaration) => declaration.Decorators,
            _ => ImmutableArray<Decorator>.Empty,
        };
        return !decorators.IsDefaultOrEmpty && decorators.Any(d => d.LineStart > 0 && d.LineStart < first.LineStart);
    }

    /// <summary>The innermost function whose body holds <paramref name="statements"/>, or null at module level.</summary>
    private static FunctionDef? EnclosingFunction(Module ast, IReadOnlyList<Statement> statements)
    {
        var first = statements[0];
        FunctionDef? enclosing = null;
        foreach (var node in PositionService.FindAllContainingNodes(ast, first.LineStart, first.ColumnStart))
        {
            if (node is FunctionDef function && !statements.Any(s => ReferenceEquals(s.UnwrapDecorated(), function)))
                enclosing = function;
        }

        return enclosing;
    }

    private static bool IsModuleDeclaration(Statement statement) =>
        statement is DecoratedStatement
        || statement is FunctionDef or ClassDef or StructDef or InterfaceDef or EnumDef or UnionDef or DelegateDef
            or PropertyDef or TypeAlias or ImportStatement or FromImportStatement
        || statement is VariableDeclaration { IsLet: false };

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
    private static IReadOnlyList<Statement> FindContainingBody(
        Module ast, int line, int col, Func<Statement, bool> covered, out Statement? owner)
    {
        // Start with the module body
        IReadOnlyList<Statement> currentBody = ast.Body;
        owner = null;

        while (true)
        {
            var foundDeeper = false;

            foreach (var stmt in currentBody)
            {
                if (!ContainsPosition(stmt, line, col) || covered(stmt))
                    continue;

                // Try to descend into a nested body
                var nestedBody = GetStatementBody(stmt);
                if (nestedBody is not null && nestedBody.Count > 0)
                {
                    currentBody = nestedBody;
                    owner = stmt;
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
    /// True when <paramref name="selection"/> covers <paramref name="statement"/>: it starts at or before the
    /// statement's first character and ends at or after its last CODE character — the true end from the
    /// statement's own tokens (<see cref="Extent.StatementEndOffset"/>), so whitespace, a line break or a trailing
    /// comment after it never matters, and a selection ending inside it does not cover it.
    /// </summary>
    private static bool CoversByText(SourceLines source, Statement statement, LspRange selection)
    {
        if (!source.Contains(statement.LineStart) || statement.ColumnStart < 1)
            return false;

        var start = source.StartOffset(statement.LineStart) + statement.ColumnStart - 1;
        int end;
        if (Extent.StatementEndOffset(statement, source.Tokens, source) is { } tokenEnd)
        {
            end = tokenEnd;
        }
        else if (source.Contains(statement.LineEnd) && statement.ColumnEnd >= 1)
        {
            end = Math.Min(source.Text.Length, source.StartOffset(statement.LineEnd) + statement.ColumnEnd - 1);
        }
        else
        {
            return false;
        }

        // A token whose recorded length runs past its source text (a normalized `.5`) must not push the end into
        // the line break that follows it.
        while (end > start && char.IsWhiteSpace(source.Text[end - 1]))
            end--;

        return OffsetOf(source, selection.Start) <= start && end <= OffsetOf(source, selection.End);
    }

    /// <summary>The 0-based document offset of an LSP position, its character clamped to the line's content.</summary>
    private static int OffsetOf(SourceLines source, Position position)
    {
        var line = position.Line + 1;
        if (line > source.Count)
            return source.Text.Length;
        if (line < 1)
            return 0;
        return source.StartOffset(line) + Math.Min(position.Character, source.Line(line).Length);
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
    internal static bool IsZeroWidthSelection(LspRange selection)
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
