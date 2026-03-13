using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class FormattingTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly SharpyFormattingHandler _handler;

    public FormattingTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _handler = new SharpyFormattingHandler(_workspace);
    }

    private async Task<string?> FormatAsync(string source, int tabSize = 4, bool insertSpaces = true)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new DocumentFormattingParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Options = new FormattingOptions
            {
                TabSize = tabSize,
                InsertSpaces = insertSpaces
            }
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        if (result is null || !result.Any())
            return null; // no changes needed

        return result.Single().NewText;
    }

    [Fact]
    public async Task AlreadyFormatted_ReturnsNullAsync()
    {
        var source = "def foo():\n    x: int = 1\n    return x";
        var formatted = await FormatAsync(source);

        formatted.Should().BeNull();
    }

    [Fact]
    public async Task EmptyFile_ReturnsNullAsync()
    {
        var formatted = await FormatAsync("");
        formatted.Should().BeNull();
    }

    [Fact]
    public async Task SingleLine_NoChangeAsync()
    {
        var source = "x: int = 42";
        var formatted = await FormatAsync(source);

        formatted.Should().BeNull();
    }

    [Fact]
    public async Task BlankLines_PreservedAsEmptyAsync()
    {
        var source = "def foo():\n    x: int = 1\n\n    return x";
        var formatted = await FormatAsync(source);

        // Already correctly formatted, blank line preserved
        formatted.Should().BeNull();
    }

    [Fact]
    public async Task TabSize2_ReformatsIndentAsync()
    {
        // Source uses standard 4-space indent; reformat with tabSize=2
        var source = "def foo():\n    x: int = 1\n    return x";
        var formatted = await FormatAsync(source, tabSize: 2);

        formatted.Should().NotBeNull();
        formatted.Should().Contain("  x: int = 1");
        formatted.Should().Contain("  return x");
    }

    [Fact]
    public async Task TabSize2_NestedAsync()
    {
        var source = "def foo():\n    if True:\n        x: int = 1";
        var formatted = await FormatAsync(source, tabSize: 2);

        formatted.Should().NotBeNull();
        formatted.Should().Contain("  if True:");
        formatted.Should().Contain("    x: int = 1");
    }

    [Fact]
    public async Task InsertTabs_UsesTabCharacterAsync()
    {
        var source = "def foo():\n    x: int = 1";
        var formatted = await FormatAsync(source, insertSpaces: false);

        formatted.Should().NotBeNull();
        formatted.Should().Contain("\tx: int = 1");
    }

    [Fact]
    public async Task StringWithColon_DoesNotAffectIndentAsync()
    {
        // A colon inside a string should not be confused with a block-starting colon
        var source = "def foo():\n    x: str = \"hello: world\"\n    return x";
        var formatted = await FormatAsync(source);

        // Already correctly formatted
        formatted.Should().BeNull();
    }

    [Fact]
    public async Task IfElifElse_CorrectlyIndentedAsync()
    {
        // Already valid 4-space indent; reformat with tabSize=2 to verify structure
        var source = "def foo():\n    if True:\n        x: int = 1\n    elif False:\n        x = 2\n    else:\n        x = 3";
        var formatted = await FormatAsync(source, tabSize: 2);

        formatted.Should().NotBeNull();
        formatted.Should().Contain("  if True:");
        formatted.Should().Contain("    x: int = 1");
        formatted.Should().Contain("  elif False:");
        formatted.Should().Contain("    x = 2");
        formatted.Should().Contain("  else:");
        formatted.Should().Contain("    x = 3");
    }

    [Fact]
    public async Task ClassWithDecorator_FormattedCorrectlyAsync()
    {
        // Reformat 4-space indented code with tabSize=2 to verify decorator indent tracking
        var source = "class Foo:\n    @staticmethod\n    def bar() -> int:\n        return 1";
        var formatted = await FormatAsync(source, tabSize: 2);

        formatted.Should().NotBeNull();
        formatted.Should().Contain("  @staticmethod");
        formatted.Should().Contain("  def bar() -> int:");
        formatted.Should().Contain("    return 1");
    }

    [Fact]
    public async Task MultipleTopLevelDeclarations_StayAtLevel0Async()
    {
        var source = "class A:\n    x: int = 1\nclass B:\n    y: int = 2";
        var formatted = await FormatAsync(source);

        // Already correctly formatted
        formatted.Should().BeNull();
    }

    [Fact]
    public async Task ExtraWhitespace_NormalizedAsync()
    {
        // Source has 8-space indent where 4 is expected (valid to lexer since it's a multiple of 4)
        var source = "def foo():\n        x: int = 1";
        var formatted = await FormatAsync(source);

        // The lexer sees one INDENT (8 spaces = one indent level), formatter normalizes to 4 spaces
        formatted.Should().NotBeNull();
        formatted.Should().Contain("    x: int = 1");
    }

    [Fact]
    public async Task UnknownDocument_ReturnsNullAsync()
    {
        var request = new DocumentFormattingParams
        {
            TextDocument = new TextDocumentIdentifier("file:///nonexistent.spy"),
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            }
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
