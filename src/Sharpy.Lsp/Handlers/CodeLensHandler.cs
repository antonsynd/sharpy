using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/codeLens requests.
/// Shows reference counts above function and class definitions,
/// and a "Run" lens above entry point functions.
/// </summary>
internal sealed class SharpyCodeLensHandler : CodeLensHandlerBase
{
    private readonly LanguageService _languageService;

    public SharpyCodeLensHandler(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public override async Task<CodeLensContainer?> Handle(CodeLensParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null || analysis.SymbolTable == null)
            return null;

        var lenses = new List<CodeLens>();

        foreach (var stmt in analysis.Ast.Body)
        {
            switch (stmt)
            {
                case FunctionDef funcDef:
                    AddFunctionLens(funcDef, analysis, lenses);
                    break;
                case ClassDef classDef:
                    AddDefinitionLens(classDef.Name, classDef, analysis, lenses);
                    break;
                case StructDef structDef:
                    AddDefinitionLens(structDef.Name, structDef, analysis, lenses);
                    break;
                case InterfaceDef interfaceDef:
                    AddDefinitionLens(interfaceDef.Name, interfaceDef, analysis, lenses);
                    break;
            }
        }

        return new CodeLensContainer(lenses);
    }

    public override Task<CodeLens> Handle(CodeLens request, CancellationToken ct)
    {
        // No resolve needed — we provide the command eagerly
        return Task.FromResult(request);
    }

    private static void AddFunctionLens(
        FunctionDef funcDef,
        Compiler.SemanticResult analysis,
        List<CodeLens> lenses)
    {
        var range = DefinitionRange(funcDef);

        // Reference count lens
        var symbol = analysis.SymbolTable!.Lookup(funcDef.Name);
        if (symbol != null)
        {
            var refCount = analysis.SemanticQuery!.GetReferences(symbol).Count;
            var refText = refCount == 1 ? "1 reference" : $"{refCount} references";

            lenses.Add(new CodeLens
            {
                Range = range,
                Command = new Command
                {
                    Title = refText,
                    Name = "sharpy.showReferences"
                }
            });
        }

        // "Run" lens for entry points (functions named "main")
        if (string.Equals(funcDef.Name, "main", StringComparison.Ordinal))
        {
            lenses.Add(new CodeLens
            {
                Range = range,
                Command = new Command
                {
                    Title = "Run",
                    Name = "sharpy.run"
                }
            });
        }
    }

    private static void AddDefinitionLens(
        string name,
        Node node,
        Compiler.SemanticResult analysis,
        List<CodeLens> lenses)
    {
        var range = DefinitionRange(node);

        var symbol = analysis.SymbolTable!.Lookup(name);
        if (symbol == null)
            return;

        var refCount = analysis.SemanticQuery!.GetReferences(symbol).Count;
        var refText = refCount == 1 ? "1 reference" : $"{refCount} references";

        lenses.Add(new CodeLens
        {
            Range = range,
            Command = new Command
            {
                Title = refText,
                Name = "sharpy.showReferences"
            }
        });
    }

    private static LspRange DefinitionRange(Node node)
    {
        var line = System.Math.Max(0, node.LineStart - 1);
        var col = System.Math.Max(0, node.ColumnStart - 1);
        return new LspRange(
            new Position(line, col),
            new Position(line, col));
    }

    protected override CodeLensRegistrationOptions CreateRegistrationOptions(
        CodeLensCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new CodeLensRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy"),
            ResolveProvider = false
        };
    }
}
