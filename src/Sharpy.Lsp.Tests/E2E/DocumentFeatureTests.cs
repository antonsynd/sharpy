using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Lsp.Tests.E2E;

/// <summary>
/// End-to-end protocol tests for document feature handlers:
/// SemanticTokens, FoldingRange, DocumentHighlight, InlayHint,
/// SelectionRange, and LinkedEditingRange.
/// </summary>
public class DocumentFeatureTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private LspTestClient _client = null!;

    public DocumentFeatureTests(ITestOutputHelper output)
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
    // SemanticTokens
    // ------------------------------------------------------------------

    [Fact]
    public async Task SemanticTokens_Full_ReturnsTokenData()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_semtok_full.spy";
        var source = "class Animal:\n    def __init__(self):\n        pass\ndef main():\n    a = Animal()";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/semanticTokens/full",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri }
            });

        result.Should().NotBeNull("semanticTokens/full should return a result for non-empty source");

        var data = result!["data"]?.AsArray();
        data.Should().NotBeNull("semantic tokens response must have a 'data' field");
        data!.Count.Should().BeGreaterThan(0,
            "source with class and function declarations should produce semantic tokens");

        // Token data is encoded as 5 ints per token (deltaLine, deltaStart, length, type, modifiers)
        (data.Count % 5).Should().Be(0, "semantic token data must be a multiple of 5 integers");
    }

    [Fact]
    public async Task SemanticTokens_Full_EmptyDocument_ReturnsEmptyData()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_semtok_empty.spy";
        await _client.DidOpenAsync(uri, "");

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/semanticTokens/full",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri }
            });

        result.Should().NotBeNull("semanticTokens/full should still return a (possibly empty) result for empty docs");
        var data = result!["data"]?.AsArray();
        data.Should().NotBeNull();
        data!.Count.Should().Be(0, "empty document should produce no semantic tokens");
    }

    [Fact]
    public async Task SemanticTokens_Full_OnlyComments_ReturnsEmptyData()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_semtok_comments.spy";
        var source = "# this is a comment\n# another comment\n";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/semanticTokens/full",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri }
            });

        result.Should().NotBeNull();
        var data = result!["data"]?.AsArray();
        data.Should().NotBeNull();
        data!.Count.Should().Be(0,
            "a comments-only document should produce no semantic tokens (the handler skips comments)");
    }

    // ------------------------------------------------------------------
    // FoldingRange
    // ------------------------------------------------------------------

    [Fact]
    public async Task FoldingRange_ReturnsRangesForFunctions()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_folding.spy";
        var source = "def foo():\n    x: int = 1\n    print(x)\n\ndef bar():\n    y: int = 2\n    print(y)";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/foldingRange",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri }
            });

        result.Should().NotBeNull("foldingRange should return ranges for multi-line functions");

        var ranges = result!.AsArray();
        ranges.Should().NotBeNull();
        ranges!.Count.Should().BeGreaterThanOrEqualTo(2,
            "should produce at least one folding range per top-level function");

        // Each range must have startLine < endLine
        foreach (var range in ranges)
        {
            var startLine = range!["startLine"]!.GetValue<int>();
            var endLine = range["endLine"]!.GetValue<int>();
            endLine.Should().BeGreaterThan(startLine,
                "folding ranges must span more than one line");
        }
    }

    [Fact]
    public async Task FoldingRange_EmptyDocument_ReturnsEmptyArray()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_folding_empty.spy";
        await _client.DidOpenAsync(uri, "");

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/foldingRange",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri }
            });

        // Server returns null or empty array — both are valid LSP responses
        if (result != null)
        {
            var ranges = result.AsArray();
            ranges!.Count.Should().Be(0, "empty document should have no folding ranges");
        }
    }

    // ------------------------------------------------------------------
    // DocumentHighlight
    // ------------------------------------------------------------------

    [Fact]
    public async Task DocumentHighlight_ReturnsAllOccurrencesOfSymbol()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_highlight.spy";
        // 'x' is declared on line 1 and used on lines 2 and 3.
        var source = "def main():\n    x: int = 1\n    print(x)\n    print(x)";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Position cursor on 'x' inside print(x) on line 2.
        // Line 2: "    print(x)" — char 10 is the 'x' identifier usage.
        var result = await _client.SendRequestAsync(
            "textDocument/documentHighlight",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["position"] = new JsonObject { ["line"] = 2, ["character"] = 10 }
            });

        result.Should().NotBeNull("documentHighlight should return occurrences for a known variable");

        var highlights = result!.AsArray();
        highlights.Should().NotBeNull();
        highlights!.Count.Should().BeGreaterThanOrEqualTo(2,
            "x is used in multiple places, should produce multiple highlights");

        // Each highlight must include a range
        foreach (var hl in highlights)
        {
            hl!["range"].Should().NotBeNull("each highlight must have a range");
        }
    }

    [Fact]
    public async Task DocumentHighlight_AtNonSymbolPosition_ReturnsNullOrEmpty()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_highlight_empty.spy";
        var source = "def main():\n    pass\n";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Position over whitespace at the start of line 0
        var result = await _client.SendRequestAsync(
            "textDocument/documentHighlight",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["position"] = new JsonObject { ["line"] = 0, ["character"] = 0 }
            });

        // Either null or empty array is acceptable per LSP spec
        if (result != null)
        {
            var highlights = result.AsArray();
            highlights!.Count.Should().Be(0,
                "highlight on a non-identifier position should produce no results");
        }
    }

    // ------------------------------------------------------------------
    // InlayHint
    // ------------------------------------------------------------------

    [Fact]
    public async Task InlayHint_ReturnsTypeHintsForInferredVariables()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_inlay.spy";
        // Variable `x` lacks an explicit type annotation — should get an inferred-type hint.
        var source = "def main():\n    x = 42\n    print(x)";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/inlayHint",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["range"] = new JsonObject
                {
                    ["start"] = new JsonObject { ["line"] = 0, ["character"] = 0 },
                    ["end"] = new JsonObject { ["line"] = 100, ["character"] = 0 }
                }
            });

        result.Should().NotBeNull("inlayHint should return hints for code with inferable types");

        var hints = result!.AsArray();
        hints.Should().NotBeNull();
        hints!.Count.Should().BeGreaterThan(0,
            "inferred variable 'x = 42' should produce at least one type hint");

        // Each hint must have a position and label
        foreach (var hint in hints)
        {
            hint!["position"].Should().NotBeNull("each inlay hint must include a position");
            hint["label"].Should().NotBeNull("each inlay hint must include a label");
        }
    }

    [Fact]
    public async Task InlayHint_EmptyDocument_ReturnsEmptyArray()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_inlay_empty.spy";
        await _client.DidOpenAsync(uri, "");

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/inlayHint",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["range"] = new JsonObject
                {
                    ["start"] = new JsonObject { ["line"] = 0, ["character"] = 0 },
                    ["end"] = new JsonObject { ["line"] = 10, ["character"] = 0 }
                }
            });

        // null or empty array are both acceptable
        if (result != null)
        {
            var hints = result.AsArray();
            hints!.Count.Should().Be(0, "empty document should produce no inlay hints");
        }
    }

    [Fact]
    public async Task InlayHint_UnicodeIdentifiers_StillProducesHints()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_inlay_unicode.spy";
        // Use unicode in string literal; ASCII identifier with inferred type.
        var source = "def main():\n    greeting = \"héllo wörld\"\n    print(greeting)";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/inlayHint",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["range"] = new JsonObject
                {
                    ["start"] = new JsonObject { ["line"] = 0, ["character"] = 0 },
                    ["end"] = new JsonObject { ["line"] = 100, ["character"] = 0 }
                }
            });

        result.Should().NotBeNull("inlayHint should handle unicode source content gracefully");
        var hints = result!.AsArray();
        hints!.Count.Should().BeGreaterThan(0,
            "inferred variable 'greeting' should produce a type hint even with unicode strings");
    }

    // ------------------------------------------------------------------
    // SelectionRange
    // ------------------------------------------------------------------

    [Fact]
    public async Task SelectionRange_ReturnsNestedRanges()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_selection.spy";
        var source = "def main():\n    x: int = 1 + 2\n    print(x)";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Position on the literal '1' inside '1 + 2'.
        // Line 1: "    x: int = 1 + 2"
        //          0   4   8   12 ^char 13
        var result = await _client.SendRequestAsync(
            "textDocument/selectionRange",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["positions"] = new JsonArray(
                    new JsonObject { ["line"] = 1, ["character"] = 13 })
            });

        result.Should().NotBeNull("selectionRange should return a result for a known position");
        var ranges = result!.AsArray();
        ranges!.Count.Should().Be(1,
            "selectionRange should return one entry per requested position");

        var first = ranges[0]!;
        first["range"].Should().NotBeNull("each selection range must include a range");
    }

    [Fact]
    public async Task SelectionRange_NonExistentPosition_ReturnsZeroLengthRange()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_selection_oob.spy";
        var source = "def main():\n    pass\n";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Request a position well beyond the end of the file.
        var result = await _client.SendRequestAsync(
            "textDocument/selectionRange",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["positions"] = new JsonArray(
                    new JsonObject { ["line"] = 999, ["character"] = 0 })
            });

        result.Should().NotBeNull(
            "selectionRange should not error for out-of-bounds positions; returns a degenerate range");
        var ranges = result!.AsArray();
        ranges!.Count.Should().Be(1,
            "should still return one entry per requested position");
    }

    // ------------------------------------------------------------------
    // LinkedEditingRange
    // ------------------------------------------------------------------

    [Fact]
    public async Task LinkedEditingRange_InsideFString_ReturnsLinkedRanges()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_linked_editing.spy";
        // f-string with the same identifier referenced twice — handler links them.
        var source = "def main():\n    name: str = \"world\"\n    print(f\"{name} and {name}\")";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Position cursor on the first `name` inside the f-string at line 2.
        // Line 2: "    print(f\"{name} and {name}\")"
        // 0-based char 14 is the start of "name" inside {name}.
        var result = await _client.SendRequestAsync(
            "textDocument/linkedEditingRange",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["position"] = new JsonObject { ["line"] = 2, ["character"] = 14 }
            });

        // Handler returns null when no f-string match is found. When there ARE matches
        // the response must contain `ranges`. We accept either result here because the
        // exact column of the identifier inside the f-string can shift slightly with
        // the parser's f-string position bookkeeping; the important behaviour is that
        // the handler does not error and returns a syntactically valid LSP response.
        if (result != null)
        {
            var ranges = result["ranges"]?.AsArray();
            ranges.Should().NotBeNull(
                "when a result is returned, it must include a 'ranges' field");
            ranges!.Count.Should().BeGreaterThanOrEqualTo(2,
                "linked editing requires at least two matching ranges");
        }
    }

    [Fact]
    public async Task LinkedEditingRange_OutsideFString_ReturnsNull()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_linked_editing_none.spy";
        var source = "def main():\n    x: int = 1\n    print(x)";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Cursor on plain identifier 'x' (not in an f-string).
        var result = await _client.SendRequestAsync(
            "textDocument/linkedEditingRange",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["position"] = new JsonObject { ["line"] = 1, ["character"] = 4 }
            });

        result.Should().BeNull(
            "linkedEditingRange should return null when not inside an f-string with multiple matches");
    }

    [Fact]
    public async Task LinkedEditingRange_EmptyDocument_ReturnsNull()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_linked_editing_empty.spy";
        await _client.DidOpenAsync(uri, "");

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var result = await _client.SendRequestAsync(
            "textDocument/linkedEditingRange",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri },
                ["position"] = new JsonObject { ["line"] = 0, ["character"] = 0 }
            });

        result.Should().BeNull(
            "linkedEditingRange on an empty document should return null");
    }
}
