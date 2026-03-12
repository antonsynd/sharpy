using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/foldingRange requests.
/// Produces folding ranges for compound statements and type definitions.
/// </summary>
internal sealed class SharplyFoldingRangeHandler : FoldingRangeHandlerBase
{
    private readonly LanguageService _languageService;

    public SharplyFoldingRangeHandler(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public override async Task<Container<FoldingRange>?> Handle(
        FoldingRangeRequestParam request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null)
            return null;

        var ranges = new List<FoldingRange>();
        CollectFoldingRanges(analysis.Ast.Body, ranges);

        return new Container<FoldingRange>(ranges);
    }

    private static void CollectFoldingRanges(IEnumerable<Statement> statements, List<FoldingRange> ranges)
    {
        foreach (var stmt in statements)
        {
            CollectStatementRanges(stmt, ranges);
        }
    }

    private static void CollectStatementRanges(Statement stmt, List<FoldingRange> ranges)
    {
        switch (stmt)
        {
            case FunctionDef f:
                AddRange(ranges, f, FoldingRangeKind.Region);
                CollectFoldingRanges(f.Body, ranges);
                break;

            case ClassDef c:
                AddRange(ranges, c, FoldingRangeKind.Region);
                CollectFoldingRanges(c.Body, ranges);
                break;

            case StructDef s:
                AddRange(ranges, s, FoldingRangeKind.Region);
                CollectFoldingRanges(s.Body, ranges);
                break;

            case InterfaceDef i:
                AddRange(ranges, i, FoldingRangeKind.Region);
                CollectFoldingRanges(i.Body, ranges);
                break;

            case EnumDef e:
                AddRange(ranges, e, FoldingRangeKind.Region);
                break;

            case IfStatement ifStmt:
                AddRange(ranges, ifStmt, FoldingRangeKind.Region);
                CollectFoldingRanges(ifStmt.ThenBody, ranges);
                foreach (var elif in ifStmt.ElifClauses)
                    CollectFoldingRanges(elif.Body, ranges);
                CollectFoldingRanges(ifStmt.ElseBody, ranges);
                break;

            case ForStatement forStmt:
                AddRange(ranges, forStmt, FoldingRangeKind.Region);
                CollectFoldingRanges(forStmt.Body, ranges);
                CollectFoldingRanges(forStmt.ElseBody, ranges);
                break;

            case WhileStatement whileStmt:
                AddRange(ranges, whileStmt, FoldingRangeKind.Region);
                CollectFoldingRanges(whileStmt.Body, ranges);
                CollectFoldingRanges(whileStmt.ElseBody, ranges);
                break;

            case TryStatement tryStmt:
                AddRange(ranges, tryStmt, FoldingRangeKind.Region);
                CollectFoldingRanges(tryStmt.Body, ranges);
                foreach (var handler in tryStmt.Handlers)
                    CollectFoldingRanges(handler.Body, ranges);
                CollectFoldingRanges(tryStmt.ElseBody, ranges);
                CollectFoldingRanges(tryStmt.FinallyBody, ranges);
                break;

            case MatchStatement matchStmt:
                AddRange(ranges, matchStmt, FoldingRangeKind.Region);
                foreach (var matchCase in matchStmt.Cases)
                    CollectFoldingRanges(matchCase.Body, ranges);
                break;

            case WithStatement withStmt:
                AddRange(ranges, withStmt, FoldingRangeKind.Region);
                CollectFoldingRanges(withStmt.Body, ranges);
                break;

            case PropertyDef p:
                if (p.IsFunctionStyle && p.Body.Length > 0)
                {
                    AddRange(ranges, p, FoldingRangeKind.Region);
                    CollectFoldingRanges(p.Body, ranges);
                }
                break;
        }
    }

    private static void AddRange(List<FoldingRange> ranges, Node node, FoldingRangeKind kind)
    {
        // Only add folding range if it spans multiple lines
        var startLine = node.LineStart - 1; // Convert to 0-based
        var endLine = node.LineEnd - 1;

        if (endLine > startLine)
        {
            ranges.Add(new FoldingRange
            {
                StartLine = startLine,
                EndLine = endLine,
                Kind = kind
            });
        }
    }

    protected override FoldingRangeRegistrationOptions CreateRegistrationOptions(
        FoldingRangeCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new FoldingRangeRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
