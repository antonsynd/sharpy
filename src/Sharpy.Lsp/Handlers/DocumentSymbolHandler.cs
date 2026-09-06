using System.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/documentSymbol requests.
/// Produces hierarchical symbols for the document outline view.
/// </summary>
internal sealed class SharpyDocumentSymbolHandler : DocumentSymbolHandlerBase
{
    private readonly LanguageService _languageService;

    public SharpyDocumentSymbolHandler(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public override async Task<SymbolInformationOrDocumentSymbolContainer?> Handle(
        DocumentSymbolParams request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var parseResult = await _languageService.GetParseResultAsync(uri, ct).ConfigureAwait(false);

        if (parseResult?.Ast == null)
            return null;

        var symbols = new System.Collections.Generic.List<DocumentSymbol>();
        CollectOutlineSymbols(parseResult.Ast.Body, new BindingScope(), SymbolKind.Variable, symbols);

        return new SymbolInformationOrDocumentSymbolContainer(
            symbols.Select(s => new SymbolInformationOrDocumentSymbol(s)));
    }

    /// <summary>
    /// Walks <paramref name="statements"/> with a <see cref="BindingScope"/>, converting named
    /// declarations via the existing <see cref="ConvertStatement"/> / <see cref="ConvertClassMember"/>
    /// arms AND producing outline entries for declaring plain assignments (<c>x = 42</c>). Recurses
    /// into compound control-flow statements (if/else, while, for, try, with, match) so that an
    /// assignment inside a conditional still appears in the outline; does NOT recurse into
    /// scope-creating bodies (function, class, struct, etc.) because those are their own outline
    /// subtrees.
    /// </summary>
    /// <param name="assignmentKind">
    /// <see cref="SymbolKind.Variable"/> at module level; <see cref="SymbolKind.Field"/> inside a
    /// type body.
    /// </param>
    private static void CollectOutlineSymbols(
        IEnumerable<Statement> statements,
        BindingScope scope,
        SymbolKind assignmentKind,
        System.Collections.Generic.List<DocumentSymbol> symbols)
    {
        var isClassLevel = assignmentKind == SymbolKind.Field;

        foreach (var rawStmt in statements)
        {
            var stmt = rawStmt is DecoratedStatement decorated ? decorated.Statement : rawStmt;

            // Mark named declarations as bound so later assignments to the same name are
            // correctly treated as rebindings.
            MarkNamedDeclaration(stmt, scope);

            // Existing named-declaration conversion (unchanged behavior).
            var converted = isClassLevel ? ConvertClassMember(rawStmt) : ConvertStatement(rawStmt);
            if (converted != null)
                symbols.Add(converted);

            // Plain assignment declarations: only `=` introduces a new binding.
            if (stmt is Assignment { Target: Identifier id } assignment
                && assignment.Operator == AssignmentOperator.Assign
                && scope.TryDeclare(id.Name))
            {
                symbols.Add(MakeSymbol(id.Name, assignmentKind, rawStmt));
            }

            // Recurse into compound control-flow statements.
            switch (stmt)
            {
                case IfStatement ifStmt:
                    CollectOutlineSymbols(ifStmt.ThenBody, scope, assignmentKind, symbols);
                    foreach (var elif in ifStmt.ElifClauses)
                        CollectOutlineSymbols(elif.Body, scope, assignmentKind, symbols);
                    if (ifStmt.ElseBody.Length > 0)
                        CollectOutlineSymbols(ifStmt.ElseBody, scope, assignmentKind, symbols);
                    break;
                case WhileStatement whileStmt:
                    CollectOutlineSymbols(whileStmt.Body, scope, assignmentKind, symbols);
                    break;
                case ForStatement forStmt:
                    BindingScopeWalker.MarkTargetBound(forStmt.Target, scope);
                    CollectOutlineSymbols(forStmt.Body, scope, assignmentKind, symbols);
                    if (forStmt.ElseBody.Length > 0)
                        CollectOutlineSymbols(forStmt.ElseBody, scope, assignmentKind, symbols);
                    break;
                case TryStatement tryStmt:
                    CollectOutlineSymbols(tryStmt.Body, scope, assignmentKind, symbols);
                    foreach (var handler in tryStmt.Handlers)
                    {
                        if (handler.Name != null)
                            scope.MarkBound(handler.Name);
                        CollectOutlineSymbols(handler.Body, scope, assignmentKind, symbols);
                    }
                    if (tryStmt.ElseBody.Length > 0)
                        CollectOutlineSymbols(tryStmt.ElseBody, scope, assignmentKind, symbols);
                    if (tryStmt.FinallyBody.Length > 0)
                        CollectOutlineSymbols(tryStmt.FinallyBody, scope, assignmentKind, symbols);
                    break;
                case WithStatement withStmt:
                    foreach (var item in withStmt.Items)
                    {
                        if (item.Target is Identifier iId)
                            scope.MarkBound(iId.Name);
                    }
                    CollectOutlineSymbols(withStmt.Body, scope, assignmentKind, symbols);
                    break;
                case MatchStatement matchStmt:
                    foreach (var matchCase in matchStmt.Cases)
                    {
                        BindingScopeWalker.MarkPatternBound(matchCase.Pattern, scope);
                        CollectOutlineSymbols(matchCase.Body, scope, assignmentKind, symbols);
                    }
                    break;
                case DeferStatement deferStmt:
                    CollectOutlineSymbols(deferStmt.Body, scope, assignmentKind, symbols);
                    break;
                    // FunctionDef, ClassDef, StructDef, etc. are scope-creating bodies —
                    // they appear in the outline as their own subtrees and are NOT recursed
                    // for assignment discovery.
            }
        }
    }

    /// <summary>
    /// Marks names introduced by named declarations as bound in <paramref name="scope"/>, so
    /// a later assignment to the same name is treated as a rebinding rather than a declaration.
    /// </summary>
    private static void MarkNamedDeclaration(Statement stmt, BindingScope scope)
    {
        switch (stmt)
        {
            case FunctionDef f: scope.MarkBound(f.Name); break;
            case ClassDef c: scope.MarkBound(c.Name); break;
            case StructDef s: scope.MarkBound(s.Name); break;
            case InterfaceDef i: scope.MarkBound(i.Name); break;
            case EnumDef e: scope.MarkBound(e.Name); break;
            case VariableDeclaration v: scope.MarkBound(v.Name); break;
            case TypeAlias t: scope.MarkBound(t.Name); break;
            case UnionDef u: scope.MarkBound(u.Name); break;
            case DelegateDef d: scope.MarkBound(d.Name); break;
            case PropertyDef p: scope.MarkBound(p.Name); break;
            case EventDef e: scope.MarkBound(e.Name); break;
        }
    }

    private static DocumentSymbol? ConvertStatement(Statement stmt)
    {
        return stmt switch
        {
            FunctionDef f => ConvertFunction(f),
            ClassDef c => ConvertTypeWithBody(c.Name, SymbolKind.Class, c.Body, c),
            StructDef s => ConvertTypeWithBody(s.Name, SymbolKind.Struct, s.Body, s),
            InterfaceDef i => ConvertTypeWithBody(i.Name, SymbolKind.Interface, i.Body, i),
            EnumDef e => ConvertEnum(e),
            VariableDeclaration v => ConvertVariable(v),
            TypeAlias t => ConvertTypeAlias(t),
            // Plan-950124 Phase 2 remediation: the two remaining named type declarations. LSP
            // SymbolKind has neither Union nor Delegate; both lower to classes, so Class it is —
            // a union's cases are its constructors, its methods children like a class's.
            UnionDef u => ConvertUnion(u),
            DelegateDef d => MakeSymbol(d.Name, SymbolKind.Class, d),
            _ => null
        };
    }

    private static DocumentSymbol ConvertUnion(UnionDef u)
    {
        var children = new System.Collections.Generic.List<DocumentSymbol>();
        foreach (var unionCase in u.Cases)
        {
            var caseRange = new LspRange(
                PositionConverter.ToLsp(unionCase.LineStart, unionCase.ColumnStart),
                PositionConverter.ToLsp(unionCase.LineEnd, unionCase.ColumnEnd));
            var nameRange = new LspRange(
                PositionConverter.ToLsp(unionCase.NameLineStart, unionCase.NameColumnStart),
                PositionConverter.ToLsp(unionCase.NameLineStart, unionCase.NameColumnEnd));
            children.Add(new DocumentSymbol
            {
                Name = unionCase.Name,
                Kind = SymbolKind.Constructor,
                Range = caseRange,
                SelectionRange = nameRange,
            });
        }

        CollectOutlineSymbols(u.Body, new BindingScope(), SymbolKind.Field, children);

        var range = NodeToRange(u);
        return new DocumentSymbol
        {
            Name = u.Name,
            Kind = SymbolKind.Class,
            Range = range,
            SelectionRange = NameSelectionRange(u, range),
            Children = new Container<DocumentSymbol>(children),
        };
    }

    private static DocumentSymbol ConvertFunction(FunctionDef f)
    {
        var returnDetail = "";
        if (f.ReturnType != null)
            returnDetail = $" -> {f.ReturnType}";

        var range = NodeToRange(f);
        return new DocumentSymbol
        {
            Name = f.Name,
            Kind = SymbolKind.Function,
            Detail = returnDetail,
            Range = range,
            SelectionRange = NameSelectionRange(f, range),
        };
    }

    private static DocumentSymbol ConvertTypeWithBody(
        string name,
        SymbolKind kind,
        ImmutableArray<Statement> body,
        Node node)
    {
        var children = new System.Collections.Generic.List<DocumentSymbol>();
        CollectOutlineSymbols(body, new BindingScope(), SymbolKind.Field, children);

        var range = NodeToRange(node);
        return new DocumentSymbol
        {
            Name = name,
            Kind = kind,
            Range = range,
            SelectionRange = NameSelectionRange(node, range),
            Children = new Container<DocumentSymbol>(children),
        };
    }

    private static DocumentSymbol ConvertEnum(EnumDef e)
    {
        var children = new System.Collections.Generic.List<DocumentSymbol>();
        foreach (var member in e.Members)
        {
            var memberRange = new LspRange(
                PositionConverter.ToLsp(member.LineStart, member.ColumnStart),
                PositionConverter.ToLsp(member.LineEnd, member.ColumnEnd)
            );
            var nameRange = new LspRange(
                PositionConverter.ToLsp(member.LineStart, member.NameColumnStart),
                PositionConverter.ToLsp(member.LineStart, member.NameColumnEnd)
            );
            children.Add(new DocumentSymbol
            {
                Name = member.Name,
                Kind = SymbolKind.EnumMember,
                Range = memberRange,
                SelectionRange = nameRange,
            });
        }

        var range = NodeToRange(e);
        return new DocumentSymbol
        {
            Name = e.Name,
            Kind = SymbolKind.Enum,
            Range = range,
            SelectionRange = NameSelectionRange(e, range),
            Children = new Container<DocumentSymbol>(children),
        };
    }

    private static DocumentSymbol? ConvertClassMember(Statement stmt)
    {
        return stmt switch
        {
            FunctionDef f => MakeSymbol(f.Name, SymbolKind.Method, f),
            PropertyDef p => MakeSymbol(p.Name, SymbolKind.Property, p),
            EventDef e => MakeSymbol(e.Name, SymbolKind.Event, e),
            VariableDeclaration v => MakeSymbol(v.Name, SymbolKind.Field, v),
            // Nested type declarations and aliases outline exactly as they do at module level
            // (with their own children); before this they vanished from the outline.
            ClassDef or StructDef or InterfaceDef or EnumDef or UnionDef or DelegateDef or TypeAlias
                => ConvertStatement(stmt),
            _ => null
        };
    }

    private static DocumentSymbol ConvertVariable(VariableDeclaration v)
    {
        var range = NodeToRange(v);
        return new DocumentSymbol
        {
            Name = v.Name,
            Kind = SymbolKind.Variable,
            Range = range,
            SelectionRange = NameSelectionRange(v, range),
        };
    }

    private static DocumentSymbol ConvertTypeAlias(TypeAlias t)
    {
        var range = NodeToRange(t);
        return new DocumentSymbol
        {
            Name = t.Name,
            Kind = SymbolKind.TypeParameter,
            Range = range,
            SelectionRange = NameSelectionRange(t, range),
        };
    }

    private static DocumentSymbol MakeSymbol(string name, SymbolKind kind, Node node)
    {
        var range = NodeToRange(node);
        return new DocumentSymbol
        {
            Name = name,
            Kind = kind,
            Range = range,
            SelectionRange = NameSelectionRange(node, range),
        };
    }

    private static LspRange NodeToRange(Node node)
    {
        return new LspRange(
            PositionConverter.ToLsp(node.LineStart, node.ColumnStart),
            PositionConverter.ToLsp(node.LineEnd, node.ColumnEnd)
        );
    }

    /// <summary>
    /// Creates a selection range for the name portion of a node.
    /// Falls back to the full range when a precise name range cannot be computed,
    /// ensuring the LSP invariant (selectionRange ⊆ fullRange) always holds.
    /// </summary>
    private static LspRange NameSelectionRange(Node node, LspRange fullRange)
    {
        // For single-line nodes, the full range already covers just that line — use it directly.
        if (node.LineStart == node.LineEnd)
            return fullRange;

        // For multi-line nodes, narrow to first line only (the declaration line).
        // End at fullRange.End if it happens to be on the same line (shouldn't happen, but safe).
        var start = fullRange.Start;
        var end = fullRange.End.Line == start.Line ? fullRange.End : start;
        var selectionRange = new LspRange(start, end);

        AssertSelectionContained(selectionRange, fullRange, node.GetType().Name);
        return selectionRange;
    }

    /// <summary>
    /// Asserts that selectionRange is contained within fullRange.
    /// Fires in DEBUG builds to catch bugs early.
    /// </summary>
    [Conditional("DEBUG")]
    private static void AssertSelectionContained(LspRange selectionRange, LspRange fullRange, string context)
    {
        bool startOk = selectionRange.Start.Line > fullRange.Start.Line
            || (selectionRange.Start.Line == fullRange.Start.Line
                && selectionRange.Start.Character >= fullRange.Start.Character);
        bool endOk = selectionRange.End.Line < fullRange.End.Line
            || (selectionRange.End.Line == fullRange.End.Line
                && selectionRange.End.Character <= fullRange.End.Character);

        Debug.Assert(startOk && endOk,
            $"DocumentSymbol '{context}': selectionRange {selectionRange} not contained in fullRange {fullRange}");
    }

    protected override DocumentSymbolRegistrationOptions CreateRegistrationOptions(
        DocumentSymbolCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DocumentSymbolRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
