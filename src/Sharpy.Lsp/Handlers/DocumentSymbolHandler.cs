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

        var symbols = new System.Collections.Generic.List<SymbolInformationOrDocumentSymbol>();

        foreach (var stmt in parseResult.Ast.Body)
        {
            var symbol = ConvertStatement(stmt);
            if (symbol != null)
                symbols.Add(new SymbolInformationOrDocumentSymbol(symbol));
        }

        return new SymbolInformationOrDocumentSymbolContainer(symbols);
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
            _ => null
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
        foreach (var member in body)
        {
            var child = ConvertClassMember(member);
            if (child != null)
                children.Add(child);
        }

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
            children.Add(new DocumentSymbol
            {
                Name = member.Name,
                Kind = SymbolKind.EnumMember,
                Range = memberRange,
                SelectionRange = memberRange,
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
