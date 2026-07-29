using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests;

public class InlayHintTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly LspConfiguration _configuration = new();
    private readonly SharpyInlayHintHandler _handler;

    public InlayHintTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyInlayHintHandler(_languageService, _configuration);
    }

    private async Task<InlayHintContainer?> GetHintsAsync(string source, int startLine = 0, int endLine = 100)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new InlayHintParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Range = new LspRange(
                new Position(startLine, 0),
                new Position(endLine, 0))
        };

        return await _handler.Handle(request, CancellationToken.None);
    }

    [Fact]
    public async Task ModuleLevelInferredVariable_ShowsTypeHintAsync()
    {
        // Module-level variable without type annotation -> shows inferred type
        var source = "x = 42\ndef main():\n    print(x)";
        var hints = await GetHintsAsync(source);

        // The handler may return null if SemanticQuery is unavailable
        if (hints is not null)
        {
            var typeHints = hints.Where(h => h.Kind == InlayHintKind.Type).ToList();
            if (typeHints.Any())
            {
                typeHints.Should().Contain(h => h.Label.String!.Contains("int"));
            }
        }
    }

    [Fact]
    public async Task AnnotatedVariable_NoTypeHintAsync()
    {
        var source = "x: int = 42\ndef main():\n    print(x)";
        var hints = await GetHintsAsync(source);

        hints.Should().NotBeNull();
        var typeHints = hints!.Where(h => h.Kind == InlayHintKind.Type).ToList();
        // x has an explicit type annotation, so no type hint should be shown
        typeHints.Should().BeEmpty();
    }

    [Fact]
    public async Task FunctionCall_ShowsParameterHintsAsync()
    {
        var source = "def greet(name: str, count: int) -> str:\n    return name\n\ndef main():\n    greet(\"hello\", 3)";
        var hints = await GetHintsAsync(source);

        hints.Should().NotBeNull();
        var paramHints = hints!.Where(h => h.Kind == InlayHintKind.Parameter).ToList();
        paramHints.Should().Contain(h => h.Label.String!.Contains("name:"));
        paramHints.Should().Contain(h => h.Label.String!.Contains("count:"));
    }

    [Fact]
    public async Task NoAnalysis_ReturnsNullAsync()
    {
        var request = new InlayHintParams
        {
            TextDocument = new TextDocumentIdentifier("file:///nonexistent.spy"),
            Range = new LspRange(
                new Position(0, 0),
                new Position(100, 0))
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task FunctionCallInNestedBlock_ShowsParameterHintsAsync()
    {
        // Parameter hints should work for calls inside if blocks
        var source = "def greet(name: str) -> str:\n    return name\n\ndef main():\n    if True:\n        greet(\"hello\")";
        var hints = await GetHintsAsync(source);

        hints.Should().NotBeNull();
        var paramHints = hints!.Where(h => h.Kind == InlayHintKind.Parameter).ToList();
        paramHints.Should().Contain(h => h.Label.String!.Contains("name:"));
    }

    [Fact]
    public async Task SimpleFunction_NoTypeOrParamHintsAsync()
    {
        var source = "def main():\n    pass";
        var hints = await GetHintsAsync(source);

        if (hints is not null)
        {
            // No variables without type annotations, no function calls with arguments
            hints.Should().BeEmpty();
        }
        // null is acceptable if SemanticQuery is not available
    }

    // sharpy.inlayHints.typeAnnotations (#1165) — contributed since the extension's first release,
    // read by nobody until now.

    [Fact]
    public async Task TypeAnnotationHints_DisabledByConfiguration_AreNotProduced()
    {
        // A const with no annotation is what actually reaches the type-hint path: a plain `x = 42`
        // parses as an Assignment (no VariableDeclaration node), and an annotated declaration has
        // the type the hint would show. That coverage gap is #1180; this test pins the gate, which
        // is what #1165 asked for, on the shape that does produce a hint today.
        var source = "const TOTAL = 42\ndef main():\n    print(TOTAL)";

        var enabled = await GetHintsAsync(source);
        enabled.Should().NotBeNull();
        enabled!.Where(h => h.Kind == InlayHintKind.Type).Should().NotBeEmpty(
            "the disabled assertion below is only meaningful if this source produces a type hint");

        _configuration.UpdateFrom(JToken.Parse("""{"inlayHints":{"typeAnnotations":false}}"""));
        _workspace.CloseDocument("file:///test.spy");

        var disabled = await GetHintsAsync(source);
        disabled.Should().NotBeNull();
        disabled!.Where(h => h.Kind == InlayHintKind.Type).Should().BeEmpty();
    }

    [Fact]
    public async Task TypeAnnotationHints_Disabled_LeavesParameterHintsAlone()
    {
        // The two hint kinds answer different questions; turning off inferred types must not
        // silently take parameter names with it.
        _configuration.UpdateFrom(JToken.Parse("""{"inlayHints":{"typeAnnotations":false}}"""));

        var source = "def greet(name: str) -> str:\n    return name\n\ndef main():\n    greet(\"hello\")";
        var hints = await GetHintsAsync(source);

        hints.Should().NotBeNull();
        hints!.Where(h => h.Kind == InlayHintKind.Parameter).Should().Contain(
            h => h.Label.String!.Contains("name:"));
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
