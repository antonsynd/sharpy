using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/rename and textDocument/prepareRename requests.
/// </summary>
internal sealed class SharplyRenameHandler : RenameHandlerBase
{
    private readonly SharplyWorkspace _workspace;
    private readonly CompilerApi _api;

    public SharplyRenameHandler(SharplyWorkspace workspace, CompilerApi api)
    {
        _workspace = workspace;
        _api = api;
    }

    public override async Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _workspace.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var newName = request.NewName;

        // Validate new name
        if (string.IsNullOrWhiteSpace(newName) || Lexer.KeywordNames.Contains(newName))
            return null;

        if (!IsValidIdentifier(newName))
            return null;

        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var node = _api.FindNodeAtPosition(analysis.Ast, line, col);

        if (node is not Identifier id)
            return null;

        var symbol = analysis.SemanticQuery.GetIdentifierSymbol(id);
        if (symbol == null)
            return null;

        // Don't rename builtins
        if (symbol.DeclaringFilePath == null && symbol.DeclarationSpan == null)
            return null;

        var edits = new Dictionary<DocumentUri, System.Collections.Generic.IList<TextEdit>>();

        // Edit declaration
        if (symbol.DeclarationSpan != null)
        {
            var declLine = System.Math.Max(0, (symbol.DeclarationLine ?? 1) - 1);
            var declCol = System.Math.Max(0, (symbol.DeclarationColumn ?? 1) - 1);

            var declFilePath = symbol.DeclaringFilePath ?? uri;
            var declUri = declFilePath.StartsWith("file://", StringComparison.Ordinal)
                ? DocumentUri.From(declFilePath)
                : DocumentUri.FromFileSystemPath(declFilePath);

            AddEdit(edits, declUri, declLine, declCol, symbol.Name.Length, newName);
        }

        // Edit all references in current file
        var references = analysis.SemanticQuery.GetReferences(symbol);
        AddReferenceEdits(edits, references, symbol.Name, uri, newName);

        // Edit references in other workspace files
        var allUris = _workspace.GetAllDocumentUris();
        foreach (var otherUri in allUris)
        {
            if (string.Equals(otherUri, uri, StringComparison.Ordinal))
                continue;

            try
            {
                var otherAnalysis = await _workspace.GetAnalysisAsync(otherUri, ct).ConfigureAwait(false);
                if (otherAnalysis?.SemanticQuery == null)
                    continue;

                var crossRefs = otherAnalysis.SemanticQuery.FindReferencesBySymbolIdentity(
                    symbol.Name, symbol.DeclaringFilePath);
                AddReferenceEdits(edits, crossRefs, symbol.Name, otherUri, newName);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Skip files that fail to analyze
            }
        }

        return new WorkspaceEdit
        {
            Changes = edits.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value as IEnumerable<TextEdit>)
        };
    }

    private static void AddReferenceEdits(
        Dictionary<DocumentUri, System.Collections.Generic.IList<TextEdit>> edits,
        IReadOnlyList<Compiler.Semantic.SymbolReference> references,
        string symbolName,
        string fallbackUri,
        string newName)
    {
        foreach (var refLoc in references)
        {
            var refLine = System.Math.Max(0, refLoc.Line - 1);
            var refCol = System.Math.Max(0, refLoc.Column - 1);

            var refFilePath = refLoc.FilePath ?? fallbackUri;
            var refUri = refFilePath.StartsWith("file://", StringComparison.Ordinal)
                ? DocumentUri.From(refFilePath)
                : DocumentUri.FromFileSystemPath(refFilePath);

            AddEdit(edits, refUri, refLine, refCol, symbolName.Length, newName);
        }
    }

    private static void AddEdit(
        Dictionary<DocumentUri, System.Collections.Generic.IList<TextEdit>> edits,
        DocumentUri uri,
        int line,
        int col,
        int oldNameLength,
        string newName)
    {
        if (!edits.TryGetValue(uri, out var fileEdits))
        {
            fileEdits = new System.Collections.Generic.List<TextEdit>();
            edits[uri] = fileEdits;
        }

        fileEdits.Add(new TextEdit
        {
            Range = new LspRange(
                new Position(line, col),
                new Position(line, col + oldNameLength)),
            NewText = newName
        });
    }

    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        for (var i = 1; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                return false;
        }

        return true;
    }

    protected override RenameRegistrationOptions CreateRegistrationOptions(
        RenameCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new RenameRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy"),
            PrepareProvider = false
        };
    }
}
