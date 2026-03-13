using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class CodeLensTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharpyCodeLensHandler _handler;

    public CodeLensTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyCodeLensHandler(_languageService);
    }

    private async Task<CodeLensContainer?> GetCodeLensesAsync(string source)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new CodeLensParams
        {
            TextDocument = new TextDocumentIdentifier(uri)
        };

        return await _handler.Handle(request, CancellationToken.None);
    }

    [Fact]
    public async Task Function_ShowsReferenceCountAsync()
    {
        var source = "def foo():\n    pass\n\ndef main():\n    foo()";
        var lenses = await GetCodeLensesAsync(source);

        lenses.Should().NotBeNull();
        var refLenses = lenses!.Where(l => l.Command?.Title?.Contains("reference") == true).ToList();
        refLenses.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MainFunction_ShowsRunLensAsync()
    {
        var source = "def main():\n    print(\"hello\")";
        var lenses = await GetCodeLensesAsync(source);

        lenses.Should().NotBeNull();
        var runLenses = lenses!.Where(l => l.Command?.Title == "Run").ToList();
        runLenses.Should().ContainSingle();
    }

    [Fact]
    public async Task MainFunction_ShowsReferenceAndRunLensAsync()
    {
        // main() should show both reference count and Run lens
        var source = "def main():\n    print(\"hello\")";
        var lenses = await GetCodeLensesAsync(source);

        lenses.Should().NotBeNull();
        lenses!.Count().Should().BeGreaterThanOrEqualTo(2);
        lenses.Should().Contain(l => l.Command != null && l.Command.Title == "Run");
        lenses.Should().Contain(l => l.Command != null && l.Command.Title != null && l.Command.Title.Contains("reference"));
    }

    [Fact]
    public async Task ClassDef_ShowsReferenceCountAsync()
    {
        var source = "class Foo:\n    pass\n\ndef main():\n    f: Foo = Foo()";
        var lenses = await GetCodeLensesAsync(source);

        lenses.Should().NotBeNull();
        var refLenses = lenses!.Where(l => l.Command?.Title?.Contains("reference") == true).ToList();
        refLenses.Should().NotBeEmpty();
    }

    [Fact]
    public async Task NoDocument_ReturnsNullAsync()
    {
        var request = new CodeLensParams
        {
            TextDocument = new TextDocumentIdentifier("file:///nonexistent.spy")
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
