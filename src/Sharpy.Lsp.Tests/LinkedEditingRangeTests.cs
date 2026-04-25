using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class LinkedEditingRangeTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharpyLinkedEditingRangeHandler _handler;

    public LinkedEditingRangeTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyLinkedEditingRangeHandler(_languageService);
    }

    private async Task<LinkedEditingRanges?> GetLinkedRangesAsync(string source, int line, int col)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new LinkedEditingRangeParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Position = new Position(line, col)
        };

        // Handler returns null! for "no linked ranges" (OmniSharp base class is non-nullable)
        LinkedEditingRanges? result = await _handler.Handle(request, CancellationToken.None);
        return result;
    }

    [Fact]
    public async Task FString_TwoSameIdentifiers_ReturnsBothRangesAsync()
    {
        // name appears twice in the f-string
        var source = "name: str = \"world\"\nx: str = f\"Hello {name}, bye {name}\"";
        // Cursor on first 'name' inside f-string — line 1, within the first {name}
        // f"Hello {name}, bye {name}"
        // 0123456789...
        // 'name' starts after 'f"Hello {' = position 9 in the string, but we need absolute column
        // x: str = f"Hello {name}, bye {name}"
        // 0         1         2         3
        // 0123456789012345678901234567890123456
        // 'name' at col 18 (after 'f"Hello {')
        var result = await GetLinkedRangesAsync(source, 1, 18);

        result.Should().NotBeNull();
        result!.Ranges.Should().HaveCount(2);
        result.WordPattern.Should().Be("[a-zA-Z_][a-zA-Z0-9_]*");
    }

    [Fact]
    public async Task FString_DifferentIdentifiers_ReturnsNullAsync()
    {
        // 'name' only appears once, 'age' only appears once
        var source = "name: str = \"world\"\nage: int = 25\nx: str = f\"Hello {name}, age {age}\"";
        // Cursor on 'name' — only one occurrence, so no linked editing
        // x: str = f"Hello {name}, age {age}"
        // col 18 for 'name'
        var result = await GetLinkedRangesAsync(source, 2, 18);

        // Single occurrence → null
        result.Should().BeNull();
    }

    [Fact]
    public async Task FString_ComplexExpression_ReturnsNullAsync()
    {
        // {x + y} is a BinaryOp, not an Identifier — no linked editing
        var source = "x: int = 1\ny: int = 2\nz: str = f\"{x + y}\"";
        // Cursor on 'x' inside the binary expression
        // z: str = f"{x + y}"
        // col 12 for 'x'
        var result = await GetLinkedRangesAsync(source, 2, 12);

        // x inside a BinaryOp is not a direct FStringPart.Expression Identifier
        // The innermost node would be the Identifier 'x', but the FStringPart.Expression is the BinaryOp
        result.Should().BeNull();
    }

    [Fact]
    public async Task FString_SingleIdentifier_ReturnsNullAsync()
    {
        var source = "name: str = \"world\"\nx: str = f\"Hello {name}\"";
        // Only one occurrence of 'name' → nothing to link
        var result = await GetLinkedRangesAsync(source, 1, 18);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CursorOnTextPart_ReturnsNullAsync()
    {
        var source = "name: str = \"world\"\nx: str = f\"Hello {name}\"";
        // Cursor on 'H' of "Hello" — text part, not an identifier
        var result = await GetLinkedRangesAsync(source, 1, 11);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CursorOutsideFString_ReturnsNullAsync()
    {
        var source = "name: str = \"world\"\nx: str = f\"Hello {name}\"";
        // Cursor on 'name' at line 0 (the variable declaration, not inside f-string)
        var result = await GetLinkedRangesAsync(source, 0, 0);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RegularString_ReturnsNullAsync()
    {
        var source = "x: str = \"hello world\"";
        // Cursor inside a regular string, not an f-string
        var result = await GetLinkedRangesAsync(source, 0, 12);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EmptyFile_ReturnsNullAsync()
    {
        var result = await GetLinkedRangesAsync("", 0, 0);
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
