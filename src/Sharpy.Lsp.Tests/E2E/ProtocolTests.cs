using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Lsp.Tests.E2E;

/// <summary>
/// End-to-end protocol tests that spawn the actual LSP server process
/// and verify JSON-RPC communication over stdio.
/// </summary>
public class ProtocolTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private LspTestClient _client = null!;

    public ProtocolTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _client = LspTestClient.Start(_output);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _client.ShutdownAsync();
        }
        catch
        {
            // Server may have already exited
        }
        await _client.DisposeAsync();
    }

    [Fact]
    public async Task Initialize_ReturnsServerCapabilities()
    {
        var result = await _client.InitializeAsync();

        result.Should().NotBeNull();

        var capabilities = result!["capabilities"];
        capabilities.Should().NotBeNull("server must return capabilities");

        // Verify text document sync is configured
        var textDocSync = capabilities!["textDocumentSync"];
        textDocSync.Should().NotBeNull("server must support text document sync");

        // Verify hover support
        var hoverProvider = capabilities["hoverProvider"];
        hoverProvider.Should().NotBeNull("server must support hover");

        // Verify completion support
        var completionProvider = capabilities["completionProvider"];
        completionProvider.Should().NotBeNull("server must support completion");

        // Verify definition support
        var definitionProvider = capabilities["definitionProvider"];
        definitionProvider.Should().NotBeNull("server must support go-to-definition");

        // Verify references support
        var referencesProvider = capabilities["referencesProvider"];
        referencesProvider.Should().NotBeNull("server must support find references");

        // Verify rename support
        var renameProvider = capabilities["renameProvider"];
        renameProvider.Should().NotBeNull("server must support rename");

        // Verify document symbol support
        var documentSymbolProvider = capabilities["documentSymbolProvider"];
        documentSymbolProvider.Should().NotBeNull("server must support document symbols");

        // Verify signature help support
        var signatureHelpProvider = capabilities["signatureHelpProvider"];
        signatureHelpProvider.Should().NotBeNull("server must support signature help");
    }

    [Fact]
    public async Task DidOpen_PublishesDiagnostics_ForInvalidCode()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_invalid.spy";
        await _client.DidOpenAsync(uri, "x: int = \"not an int\"");

        var notification = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        notification.Should().NotBeNull();

        var notifUri = notification["uri"]?.GetValue<string>();
        notifUri.Should().Contain("test_invalid.spy");

        var diagnostics = notification["diagnostics"]?.AsArray();
        diagnostics.Should().NotBeNull();
        diagnostics!.Count.Should().BeGreaterThan(0, "invalid code should produce diagnostics");
    }

    [Fact]
    public async Task DidOpen_PublishesDiagnostics_EmptyForValidCode()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_valid.spy";
        await _client.DidOpenAsync(uri, "def main():\n    x: int = 42\n    print(x)");

        var notification = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        notification.Should().NotBeNull();

        var diagnostics = notification["diagnostics"]?.AsArray();
        diagnostics.Should().NotBeNull();
        diagnostics!.Count.Should().Be(0, "valid code should produce no diagnostics");
    }

    [Fact]
    public async Task Hover_ReturnsTypeInfo_ForFunction()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_hover.spy";
        await _client.DidOpenAsync(uri, "def greet(name: str) -> str:\n    return \"hi \" + name\ndef main():\n    greet(\"world\")");

        // Wait for diagnostics to ensure analysis is complete
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Hover over 'greet' at line 3, character 4 (inside "greet" call in main)
        var hover = await _client.HoverAsync(uri, 3, 4);

        hover.Should().NotBeNull("hover over a function call should return information");

        var contents = hover!["contents"];
        contents.Should().NotBeNull();

        // The hover content should mention the function
        var hoverText = contents!.ToJsonString();
        hoverText.Should().ContainAny("greet", "str");
    }

    [Fact]
    public async Task Completion_ReturnsItems()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_completion.spy";
        await _client.DidOpenAsync(uri, "def main():\n    x: int = 42\n    ");

        // Wait for analysis
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Request completions at the end of line 2
        var completion = await _client.CompletionAsync(uri, 2, 4);

        completion.Should().NotBeNull("completion should return results");

        // Response can be CompletionList (object with "items") or CompletionItem[] (array)
        JsonArray? items;
        if (completion is JsonArray arr)
        {
            items = arr;
        }
        else
        {
            items = completion!["items"]?.AsArray();
        }
        items.Should().NotBeNull();
        items!.Count.Should().BeGreaterThan(0, "should have completion items");
    }

    [Fact]
    public async Task DidChange_UpdatesDiagnostics()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_change.spy";

        // Start with invalid code
        await _client.DidOpenAsync(uri, "x: int = \"bad\"");
        var firstDiag = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));
        var firstCount = firstDiag["diagnostics"]?.AsArray()?.Count ?? 0;
        firstCount.Should().BeGreaterThan(0, "invalid code should have diagnostics");

        // Fix the code
        await _client.DidChangeAsync(uri, "def main():\n    x: int = 42\n    print(x)", 2);
        var secondDiag = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));
        var secondCount = secondDiag["diagnostics"]?.AsArray()?.Count ?? 0;
        secondCount.Should().Be(0, "fixed code should have no diagnostics");
    }

    [Fact]
    public async Task Shutdown_RespondsSuccessfully()
    {
        await _client.InitializeAsync();

        // Shutdown should not throw — the lifecycle is handled by DisposeAsync,
        // so we just verify initialize works and the server is responsive
        var result = await _client.SendRequestAsync("shutdown", null);
        // Shutdown returns null per LSP spec
    }
}
