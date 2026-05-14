using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Lsp.Tests.E2E;

/// <summary>
/// End-to-end protocol tests for workspace + formatting handlers:
/// CodeLens, Formatting, RangeFormatting, DidChangeConfiguration, FileWatcher.
/// </summary>
public class WorkspaceAndFormattingTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private LspTestClient _client = null!;

    public WorkspaceAndFormattingTests(ITestOutputHelper output)
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

    // ------------------------------------------------------------------
    // CodeLens
    // ------------------------------------------------------------------

    [Fact]
    public async Task CodeLens_ReturnsReferenceLensesForFunctions()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_codelens_funcs.spy";
        // foo is referenced from bar; both should get reference-count lenses.
        var source = "def foo() -> int:\n    return 1\ndef bar() -> int:\n    return foo()";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/codeLens",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri }
            });

        result.Should().NotBeNull("codeLens should return a result for code with functions");
        var lenses = result!.AsArray();
        lenses!.Count.Should().BeGreaterThanOrEqualTo(2,
            "each top-level function should produce at least a reference-count lens");

        // Each lens must include a range and a command
        foreach (var lens in lenses)
        {
            lens!["range"].Should().NotBeNull("each code lens must have a range");
            lens["command"].Should().NotBeNull("each code lens must include a command");
        }
    }

    [Fact]
    public async Task CodeLens_ProducesRunLensForMain()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_codelens_main.spy";
        var source = "def main():\n    print(1)";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/codeLens",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri }
            });

        result.Should().NotBeNull();
        var lenses = result!.AsArray();
        lenses!.Count.Should().BeGreaterThan(0);

        var titles = lenses.Select(l => l!["command"]!["title"]!.GetValue<string>()).ToList();
        titles.Should().Contain("Run",
            "main function should produce a 'Run' code lens");
    }

    [Fact]
    public async Task CodeLens_EmptyDocument_ReturnsEmptyOrNull()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_codelens_empty.spy";
        await _client.DidOpenAsync(uri, "");

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/codeLens",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri }
            });

        if (result != null)
        {
            var lenses = result.AsArray();
            lenses!.Count.Should().Be(0,
                "empty document should produce no code lenses");
        }
    }

    [Fact]
    public async Task CodeLens_ClassDefinition_ReturnsReferenceLens()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_codelens_class.spy";
        var source = "class Animal:\n    def __init__(self):\n        pass\ndef main():\n    a = Animal()";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/codeLens",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri }
            });

        result.Should().NotBeNull();
        var lenses = result!.AsArray();
        lenses!.Count.Should().BeGreaterThan(0,
            "class definitions should produce a reference-count code lens");

        var titles = lenses.Select(l => l!["command"]!["title"]!.GetValue<string>()).ToList();
        titles.Should().Contain(t => t.Contains("reference"),
            "should include at least one reference-count lens");
    }

    // ------------------------------------------------------------------
    // Formatting
    // ------------------------------------------------------------------

    [Fact]
    public async Task Formatting_NormalizesInconsistentIndentation()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_format_basic.spy";
        // Body uses 2 spaces; default formatting should renormalize to 4.
        var source = "def main():\n  x: int = 1\n  print(x)";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/formatting",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["options"] = new JsonObject
                {
                    ["tabSize"] = 4,
                    ["insertSpaces"] = true
                }
            });

        result.Should().NotBeNull(
            "formatting should return text edits when source needs normalization");

        var edits = result!.AsArray();
        edits!.Count.Should().BeGreaterThan(0,
            "inconsistent indentation should produce at least one edit");

        // Each edit must include a range and newText payload
        foreach (var edit in edits)
        {
            edit!["range"].Should().NotBeNull("each text edit must have a range");
            edit["newText"].Should().NotBeNull("each text edit must include newText");
        }
    }

    [Fact]
    public async Task Formatting_AlreadyFormatted_ReturnsNullOrEmpty()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_format_noop.spy";
        // Pretty formatter emits trailing newline; include it to match.
        var source = "def main():\n    x: int = 1\n    print(x)\n";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/formatting",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["options"] = new JsonObject
                {
                    ["tabSize"] = 4,
                    ["insertSpaces"] = true
                }
            });

        // Handler returns null when no changes are needed
        if (result != null)
        {
            var edits = result.AsArray();
            edits!.Count.Should().Be(0,
                "already-formatted code should produce no edits");
        }
    }

    [Fact]
    public async Task Formatting_WithTabs_AcceptsTabPreference()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_format_tabs.spy";
        var source = "def main():\n  x: int = 1\n  print(x)";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Server must accept insertSpaces=false (tab-based indentation) without error
        var result = await _client.SendRequestAsync(
            "textDocument/formatting",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["options"] = new JsonObject
                {
                    ["tabSize"] = 4,
                    ["insertSpaces"] = false
                }
            });

        // Tab-mode formatting may produce edits or nothing depending on lexer state,
        // but the server must respond without throwing a protocol error.
        if (result != null)
        {
            result.AsArray().Should().NotBeNull(
                "formatting response must be a TextEdit array when present");
        }
    }

    [Fact]
    public async Task Formatting_EmptyDocument_ReturnsNullOrEmpty()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_format_empty.spy";
        await _client.DidOpenAsync(uri, "");

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/formatting",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["options"] = new JsonObject
                {
                    ["tabSize"] = 4,
                    ["insertSpaces"] = true
                }
            });

        // Empty document needs no formatting -> null or empty
        if (result != null)
        {
            var edits = result.AsArray();
            edits!.Count.Should().Be(0,
                "empty document should produce no edits");
        }
    }

    // ------------------------------------------------------------------
    // RangeFormatting
    // ------------------------------------------------------------------

    [Fact]
    public async Task RangeFormatting_NormalizesIndentationInRange()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_range_format_basic.spy";
        var source = "def main():\n  x: int = 1\n  print(x)";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Format only lines 1-2 (the body)
        var result = await _client.SendRequestAsync(
            "textDocument/rangeFormatting",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["range"] = new JsonObject
                {
                    ["start"] = new JsonObject { ["line"] = 1, ["character"] = 0 },
                    ["end"] = new JsonObject { ["line"] = 2, ["character"] = 0 }
                },
                ["options"] = new JsonObject
                {
                    ["tabSize"] = 4,
                    ["insertSpaces"] = true
                }
            });

        result.Should().NotBeNull(
            "range formatting should return a (possibly empty) edit container");
        var edits = result!.AsArray();
        edits!.Count.Should().BeGreaterThan(0,
            "indented body lines in the range should produce edits");
    }

    [Fact]
    public async Task RangeFormatting_AlreadyFormatted_ReturnsEmptyEdits()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_range_format_noop.spy";
        var source = "def main():\n    x: int = 1\n    print(x)\n";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/rangeFormatting",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["range"] = new JsonObject
                {
                    ["start"] = new JsonObject { ["line"] = 0, ["character"] = 0 },
                    ["end"] = new JsonObject { ["line"] = 2, ["character"] = 0 }
                },
                ["options"] = new JsonObject
                {
                    ["tabSize"] = 4,
                    ["insertSpaces"] = true
                }
            });

        result.Should().NotBeNull(
            "rangeFormatting always returns a TextEditContainer (may be empty)");
        var edits = result!.AsArray();
        edits!.Count.Should().Be(0,
            "already-formatted source within the range should produce no edits");
    }

    // ------------------------------------------------------------------
    // DidChangeConfiguration
    // ------------------------------------------------------------------

    [Fact]
    public async Task DidChangeConfiguration_DoesNotErrorOnUpdate()
    {
        await _client.InitializeAsync();

        // Send a configuration change as a notification — server must accept it.
        await _client.SendNotificationAsync(
            "workspace/didChangeConfiguration",
            new JsonObject
            {
                ["settings"] = new JsonObject
                {
                    ["sharpy"] = new JsonObject
                    {
                        ["transitionHints"] = new JsonObject
                        {
                            ["enabled"] = true
                        }
                    }
                }
            });

        // Sanity check — server is still responsive after the notification.
        var hover = await _client.HoverAsync(
            "file:///nonexistent_for_hover.spy", 0, 0);
        // Hover on an unopened doc returns null (no error)
        hover.Should().BeNull();
    }

    [Fact]
    public async Task DidChangeConfiguration_AcceptsEmptySettings()
    {
        await _client.InitializeAsync();

        // Sending didChangeConfiguration with an empty settings object should be a
        // safe no-op — useful for clients that briefly reset configuration.
        await _client.SendNotificationAsync(
            "workspace/didChangeConfiguration",
            new JsonObject
            {
                ["settings"] = new JsonObject()
            });

        // Sanity check — server still responds to subsequent requests.
        var result = await _client.SendRequestAsync(
            "workspace/symbol",
            new JsonObject { ["query"] = "foo" });

        result.Should().NotBeNull(
            "server should remain responsive after empty configuration update");
    }

    // ------------------------------------------------------------------
    // FileWatcher
    // ------------------------------------------------------------------

    [Fact]
    public async Task DidChangeWatchedFiles_AcceptsSpyFileChanges()
    {
        await _client.InitializeAsync();

        // Send a watched-file-change notification for a .spy file. The server
        // does not need the file to exist on disk for this test — we only
        // verify the handler accepts the notification without crashing.
        await _client.SendNotificationAsync(
            "workspace/didChangeWatchedFiles",
            new JsonObject
            {
                ["changes"] = new JsonArray(
                    new JsonObject
                    {
                        ["uri"] = "file:///nonexistent_watched.spy",
                        ["type"] = 2 // FileChangeType.Changed
                    })
            });

        // Sanity check — server is still responsive afterwards.
        var result = await _client.SendRequestAsync(
            "workspace/symbol",
            new JsonObject { ["query"] = "main" });

        result.Should().NotBeNull(
            "server should remain responsive after didChangeWatchedFiles notification");
    }

    [Fact]
    public async Task DidChangeWatchedFiles_AcceptsEmptyChanges()
    {
        await _client.InitializeAsync();

        // Send an empty changes list — the handler should be a no-op.
        await _client.SendNotificationAsync(
            "workspace/didChangeWatchedFiles",
            new JsonObject
            {
                ["changes"] = new JsonArray()
            });

        // Sanity check — server is still responsive.
        var result = await _client.SendRequestAsync(
            "workspace/symbol",
            new JsonObject { ["query"] = "main" });

        result.Should().NotBeNull(
            "server should remain responsive after empty didChangeWatchedFiles notification");
    }
}
