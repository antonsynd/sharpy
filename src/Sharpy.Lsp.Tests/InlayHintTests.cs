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

    private static IReadOnlyList<InlayHint> TypeHints(InlayHintContainer? hints)
    {
        hints.Should().NotBeNull("the handler must produce a hint container for a well-formed document");
        return hints!.Where(h => h.Kind == InlayHintKind.Type).ToList();
    }

    [Fact]
    public async Task ModuleLevelInferredVariable_ShowsTypeHintAsync()
    {
        // Module-level variable without type annotation -> shows inferred type
        var source = "x = 42\ndef main():\n    print(x)";
        var hints = await GetHintsAsync(source);

        var typeHints = TypeHints(hints);
        typeHints.Should().ContainSingle().Which.Label.String.Should().Be(": int");
        // Immediately after the name `x` on line 1, so the hint reads `x: int = 42`.
        typeHints[0].Position.Should().Be(new Position(0, 1));
    }

    [Fact]
    public async Task FunctionLocalInferredVariable_ShowsTypeHintAsync()
    {
        // The pre-#1180 handler resolved the name through the module-scope symbol table,
        // which could never see a function-local binding.
        var source = "def main():\n    local = \"x\"\n    print(local)";
        var hints = await GetHintsAsync(source);

        var typeHints = TypeHints(hints);
        typeHints.Should().ContainSingle().Which.Label.String.Should().Be(": str");
        typeHints[0].Position.Should().Be(new Position(1, 9));
    }

    [Fact]
    public async Task Reassignment_ShowsOnlyOneTypeHintAsync()
    {
        // The declaring binding is worth annotating; every later rebinding is noise.
        var source = "x = 42\nx = 43\ndef main():\n    print(x)";
        var hints = await GetHintsAsync(source);

        var typeHints = TypeHints(hints);
        typeHints.Should().ContainSingle().Which.Position.Should().Be(new Position(0, 1));
    }

    [Fact]
    public async Task ShadowedName_HintsAtEachDeclaringBindingAsync()
    {
        // A nested scope's binding is a different symbol with a different type; both declare.
        var source = "x = 42\ndef main():\n    x = \"hello\"\n    print(x)";
        var hints = await GetHintsAsync(source);

        var typeHints = TypeHints(hints);
        typeHints.Should().HaveCount(2);
        typeHints.Should().ContainSingle(h => h.Position == new Position(0, 1))
            .Which.Label.String.Should().Be(": int");
        typeHints.Should().ContainSingle(h => h.Position == new Position(2, 5))
            .Which.Label.String.Should().Be(": str");
    }

    [Fact]
    public async Task AssignmentToParameter_ShowsNoTypeHintAsync()
    {
        // The parameter is the declaring binding; assigning to it is a rebinding.
        var source = "def main(count: int) -> None:\n    count = 5\n    print(count)";
        var hints = await GetHintsAsync(source);

        TypeHints(hints).Should().BeEmpty();
    }

    [Fact]
    public async Task AssignmentRightHandSide_ShowsParameterHintsAsync()
    {
        // `Assignment.Value` was never fed to the call-hint walk, so a call bound to a
        // variable got no parameter names.
        var source = "def compute(value: int) -> int:\n    return value\n\ndef main():\n    total = compute(1)\n    print(total)";
        var hints = await GetHintsAsync(source);

        hints.Should().NotBeNull();
        hints!.Where(h => h.Kind == InlayHintKind.Parameter).Should()
            .Contain(h => h.Label.String!.Contains("value:"));
    }

    [Fact]
    public async Task PlainAssignmentHints_DisabledByConfiguration_AreNotProduced()
    {
        _configuration.UpdateFrom(JToken.Parse("""{"inlayHints":{"typeAnnotations":false}}"""));

        var source = "x = 42\ndef main():\n    print(x)";
        var hints = await GetHintsAsync(source);

        TypeHints(hints).Should().BeEmpty();
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

        // No variables without type annotations, no function calls with arguments
        hints.Should().NotBeNull();
        hints!.Should().BeEmpty();
    }

    // sharpy.inlayHints.typeAnnotations (#1165) — contributed since the extension's first release,
    // read by nobody until now.

    [Fact]
    public async Task TypeAnnotationHints_DisabledByConfiguration_AreNotProduced()
    {
        // An unannotated const is the one VariableDeclaration shape that reaches the type-hint
        // path (an annotated declaration already shows the type the hint would carry), so it
        // pins the #1165 gate on the declaration arm — plain assignments are covered by
        // PlainAssignmentHints_DisabledByConfiguration_AreNotProduced.
        var source = "const TOTAL = 42\ndef main():\n    print(TOTAL)";

        var enabled = await GetHintsAsync(source);
        var enabledTypeHints = TypeHints(enabled);
        enabledTypeHints.Should().ContainSingle(
            "the disabled assertion below is only meaningful if this source produces a type hint")
            .Which.Label.String.Should().Be(": int");
        // After the name `TOTAL`, not after the `const` keyword: `const TOTAL: int = 42`.
        enabledTypeHints[0].Position.Should().Be(new Position(0, 11));

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
