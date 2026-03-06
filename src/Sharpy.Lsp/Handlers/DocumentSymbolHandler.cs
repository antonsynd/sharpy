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
internal sealed class SharplyDocumentSymbolHandler : DocumentSymbolHandlerBase
{
    private readonly SharplyWorkspace _workspace;

    public SharplyDocumentSymbolHandler(SharplyWorkspace workspace)
    {
        _workspace = workspace;
    }

    public override async Task<SymbolInformationOrDocumentSymbolContainer?> Handle(
        DocumentSymbolParams request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _workspace.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null)
            return null;

        var symbols = new System.Collections.Generic.List<SymbolInformationOrDocumentSymbol>();

        foreach (var stmt in analysis.Ast.Body)
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

        return new DocumentSymbol
        {
            Name = f.Name,
            Kind = SymbolKind.Function,
            Detail = returnDetail,
            Range = NodeToRange(f),
            SelectionRange = NameToRange(f),
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

        return new DocumentSymbol
        {
            Name = name,
            Kind = kind,
            Range = NodeToRange(node),
            SelectionRange = NameToRange(node),
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

        return new DocumentSymbol
        {
            Name = e.Name,
            Kind = SymbolKind.Enum,
            Range = NodeToRange(e),
            SelectionRange = NameToRange(e),
            Children = new Container<DocumentSymbol>(children),
        };
    }

    private static DocumentSymbol? ConvertClassMember(Statement stmt)
    {
        return stmt switch
        {
            FunctionDef f => new DocumentSymbol
            {
                Name = f.Name,
                Kind = SymbolKind.Method,
                Range = NodeToRange(f),
                SelectionRange = NameToRange(f),
            },
            PropertyDef p => new DocumentSymbol
            {
                Name = p.Name,
                Kind = SymbolKind.Property,
                Range = NodeToRange(p),
                SelectionRange = NameToRange(p),
            },
            EventDef e => new DocumentSymbol
            {
                Name = e.Name,
                Kind = SymbolKind.Event,
                Range = NodeToRange(e),
                SelectionRange = NameToRange(e),
            },
            VariableDeclaration v => new DocumentSymbol
            {
                Name = v.Name,
                Kind = SymbolKind.Field,
                Range = NodeToRange(v),
                SelectionRange = NodeToRange(v),
            },
            _ => null
        };
    }

    private static DocumentSymbol ConvertVariable(VariableDeclaration v)
    {
        var name = v.Name;
        return new DocumentSymbol
        {
            Name = name,
            Kind = SymbolKind.Variable,
            Range = NodeToRange(v),
            SelectionRange = NodeToRange(v),
        };
    }

    private static DocumentSymbol ConvertTypeAlias(TypeAlias t)
    {
        return new DocumentSymbol
        {
            Name = t.Name,
            Kind = SymbolKind.TypeParameter,
            Range = NodeToRange(t),
            SelectionRange = NameToRange(t),
        };
    }

    private static LspRange NodeToRange(Node node)
    {
        return new LspRange(
            PositionConverter.ToLsp(node.LineStart, node.ColumnStart),
            PositionConverter.ToLsp(node.LineEnd, node.ColumnEnd)
        );
    }

    private static LspRange NameToRange(Node node)
    {
        // Use just the first line for the selection range (the name position)
        return new LspRange(
            PositionConverter.ToLsp(node.LineStart, node.ColumnStart),
            PositionConverter.ToLsp(node.LineStart, node.ColumnEnd)
        );
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
