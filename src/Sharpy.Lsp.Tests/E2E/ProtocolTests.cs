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

        // Phase 1 — Structural Navigation capabilities
        var callHierarchyProvider = capabilities["callHierarchyProvider"];
        callHierarchyProvider.Should().NotBeNull("server must support call hierarchy");

        var typeHierarchyProvider = capabilities["typeHierarchyProvider"];
        typeHierarchyProvider.Should().NotBeNull("server must support type hierarchy");

        var implementationProvider = capabilities["implementationProvider"];
        implementationProvider.Should().NotBeNull("server must support go-to-implementation");

        var workspaceSymbolProvider = capabilities["workspaceSymbolProvider"];
        workspaceSymbolProvider.Should().NotBeNull("server must support workspace symbols");

        // Phase 2 — Refactoring & Code Actions capability
        var codeActionProvider = capabilities["codeActionProvider"];
        codeActionProvider.Should().NotBeNull("server must support code actions");
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
    public async Task RapidDidChange_ProducesLatestDiagnostics()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_cancel.spy";

        // Open with valid code
        await _client.DidOpenAsync(uri, "def main():\n    x: int = 1\n    print(x)");

        // Wait for initial diagnostics
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Send rapid successive changes — only the final state matters
        for (var i = 2; i <= 5; i++)
        {
            await _client.DidChangeAsync(uri, $"def main():\n    x: int = {i}\n    print(x)", i);
        }

        // The last diagnostics we receive should be for valid code (zero diagnostics)
        JsonNode? lastNotification = null;
        while (true)
        {
            try
            {
                lastNotification = await _client.WaitForNotificationAsync(
                    "textDocument/publishDiagnostics",
                    TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                break;
            }
        }

        lastNotification.Should().NotBeNull("should receive at least one diagnostic notification after changes");
        var diagnostics = lastNotification!["diagnostics"]?.AsArray();
        diagnostics.Should().NotBeNull();
        diagnostics!.Count.Should().Be(0, "final valid code should produce no diagnostics");
    }

    [Fact]
    public async Task IncrementalDidChange_RangeBasedEdit_UpdatesDiagnostics()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_incremental.spy";

        // Open with invalid code: x: int = "bad"
        await _client.DidOpenAsync(uri, "x: int = \"bad\"");
        var firstDiag = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));
        firstDiag["diagnostics"]!.AsArray()!.Count.Should().BeGreaterThan(0,
            "invalid code should have diagnostics");

        // Incrementally replace the entire content with valid code
        // Original text: x: int = "bad"  (14 chars, 1 line)
        // Replace range [0:0 .. 0:14] with valid code
        await _client.DidChangeIncrementalAsync(uri, 2,
        [
            (0, 0, 0, 14, "def main():\n    x: int = 42\n    print(x)")
        ]);

        var secondDiag = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));
        secondDiag["diagnostics"]!.AsArray()!.Count.Should().Be(0,
            "fixed code should have no diagnostics");
    }

    [Fact]
    public async Task IncrementalDidChange_SmallEdit_PreservesContext()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_small_edit.spy";

        // Open with valid code
        await _client.DidOpenAsync(uri, "def main():\n    x: int = 1\n    print(x)");
        var firstDiag = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));
        firstDiag["diagnostics"]!.AsArray()!.Count.Should().Be(0,
            "valid code should have no diagnostics");

        // Change just the number: replace "1" at line 1, char 13 with "99"
        // Line 1: "    x: int = 1" — char 13 is the digit '1'
        await _client.DidChangeIncrementalAsync(uri, 2,
        [
            (1, 13, 1, 14, "99")
        ]);

        var secondDiag = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));
        secondDiag["diagnostics"]!.AsArray()!.Count.Should().Be(0,
            "small edit should preserve validity");
    }

    [Fact]
    public async Task IncrementalDidChange_RapidSequentialEdits_ProducesCorrectResult()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_rapid_incremental.spy";

        // Open with valid code
        await _client.DidOpenAsync(uri, "def main():\n    x: int = 1\n    print(x)");
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Send multiple rapid incremental changes
        // Line 1: "    x: int = 1" — char 13 is the digit
        for (var i = 2; i <= 5; i++)
        {
            var prevLen = (i - 1).ToString().Length;
            await _client.DidChangeIncrementalAsync(uri, i,
            [
                (1, 13, 1, 13 + prevLen, i.ToString())
            ]);
        }

        // Drain all diagnostics and verify the last one is clean
        JsonNode? lastNotification = null;
        while (true)
        {
            try
            {
                lastNotification = await _client.WaitForNotificationAsync(
                    "textDocument/publishDiagnostics",
                    TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                break;
            }
        }

        lastNotification.Should().NotBeNull();
        lastNotification!["diagnostics"]!.AsArray()!.Count.Should().Be(0,
            "final valid code should produce no diagnostics");
    }

    [Fact]
    public async Task IncrementalDidChange_FullSyncFallback_Works()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_full_fallback.spy";

        // Open with valid code
        await _client.DidOpenAsync(uri, "def main():\n    x: int = 1\n    print(x)");
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Send a full-content change (no range) — this is the fallback path
        await _client.DidChangeAsync(uri, "def main():\n    y: str = \"hello\"\n    print(y)", 2);

        var diag = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));
        diag["diagnostics"]!.AsArray()!.Count.Should().Be(0,
            "full-sync fallback should work correctly");
    }

    [Fact]
    public async Task CallHierarchy_PrepareCallHierarchy_ReturnsFunctionItem()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_callhierarchy_prepare.spy";
        var source = "def foo() -> int:\n    return 1\ndef bar() -> int:\n    return foo()\ndef main():\n    bar()";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Prepare call hierarchy on the call to "foo" at line 3, char 11
        // Source layout:
        //   Line 0: def foo() -> int:
        //   Line 1:     return 1
        //   Line 2: def bar() -> int:
        //   Line 3:     return foo()    <- "foo" starts at character 11
        //   Line 4: def main():
        //   Line 5:     bar()           <- "bar" starts at character 4
        var prepareParams = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["position"] = new JsonObject { ["line"] = 3, ["character"] = 11 }
        };
        var result = await _client.SendRequestAsync("textDocument/prepareCallHierarchy", prepareParams);

        result.Should().NotBeNull("prepareCallHierarchy should return items for a function reference");

        var items = result!.AsArray();
        items.Should().NotBeNull();
        items!.Count.Should().BeGreaterThan(0, "should return at least one call hierarchy item");

        var fooItem = items[0]!;
        fooItem["name"]!.GetValue<string>().Should().Be("foo");
    }

    [Fact]
    public async Task CallHierarchy_IncomingCalls_ReturnsCallers()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_callhierarchy_incoming.spy";
        var source = "def foo() -> int:\n    return 1\ndef bar() -> int:\n    return foo()\ndef main():\n    bar()";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Prepare call hierarchy on the call to "foo" at line 3, char 11
        var prepareParams = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["position"] = new JsonObject { ["line"] = 3, ["character"] = 11 }
        };
        var prepareResult = await _client.SendRequestAsync("textDocument/prepareCallHierarchy", prepareParams);
        prepareResult.Should().NotBeNull();
        var fooItem = prepareResult!.AsArray()![0]!;

        // Request incoming calls for foo
        var incomingParams = new JsonObject
        {
            ["item"] = JsonNode.Parse(fooItem.ToJsonString())
        };
        var incomingResult = await _client.SendRequestAsync("callHierarchy/incomingCalls", incomingParams);

        incomingResult.Should().NotBeNull("incoming calls should return results");

        var incomingCalls = incomingResult!.AsArray();
        incomingCalls.Should().NotBeNull();
        incomingCalls!.Count.Should().BeGreaterThan(0, "foo is called by bar, so there should be incoming calls");

        // Verify that bar appears as a caller
        var callerNames = incomingCalls.Select(c => c!["from"]!["name"]!.GetValue<string>()).ToList();
        callerNames.Should().Contain("bar", "bar calls foo so it should appear in incoming calls");
    }

    [Fact]
    public async Task CallHierarchy_OutgoingCalls_ReturnsCallees()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_callhierarchy_outgoing.spy";
        var source = "def foo() -> int:\n    return 1\ndef bar() -> int:\n    return foo()\ndef main():\n    bar()";
        await _client.DidOpenAsync(uri, source);

        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Prepare call hierarchy on the call to "bar" at line 5, char 4 (inside main's body)
        var prepareParams = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["position"] = new JsonObject { ["line"] = 5, ["character"] = 4 }
        };
        var prepareResult = await _client.SendRequestAsync("textDocument/prepareCallHierarchy", prepareParams);
        prepareResult.Should().NotBeNull();
        var barItem = prepareResult!.AsArray()![0]!;
        barItem["name"]!.GetValue<string>().Should().Be("bar");

        // Request outgoing calls for bar
        var outgoingParams = new JsonObject
        {
            ["item"] = JsonNode.Parse(barItem.ToJsonString())
        };
        var outgoingResult = await _client.SendRequestAsync("callHierarchy/outgoingCalls", outgoingParams);

        outgoingResult.Should().NotBeNull("outgoing calls should return results");

        var outgoingCalls = outgoingResult!.AsArray();
        outgoingCalls.Should().NotBeNull();
        outgoingCalls!.Count.Should().BeGreaterThan(0, "bar calls foo, so there should be outgoing calls");

        // Verify that foo appears as a callee
        var calleeNames = outgoingCalls.Select(c => c!["to"]!["name"]!.GetValue<string>()).ToList();
        calleeNames.Should().Contain("foo", "bar calls foo so it should appear in outgoing calls");
    }

    [Fact]
    public async Task TypeHierarchy_PrepareTypeHierarchy_ReturnsClassItem()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_typehierarchy.spy";
        var source = "class Animal:\n    def __init__(self):\n        pass\nclass Dog(Animal):\n    def __init__(self):\n        super().__init__()\ndef main():\n    a = Animal()\n    d = Dog()";
        await _client.DidOpenAsync(uri, source);

        // The prepareTypeHierarchy handler calls GetAnalysisAsync which triggers
        // analysis on-demand, so we don't need to wait for diagnostics separately.

        var prepareParams = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["position"] = new JsonObject { ["line"] = 0, ["character"] = 6 }
        };

        var result = await _client.SendRequestAsync("textDocument/prepareTypeHierarchy", prepareParams);

        result.Should().NotBeNull("prepareTypeHierarchy should return items for a class");
        var items = result!.AsArray();
        items.Count.Should().BeGreaterThan(0, "should return at least one type hierarchy item");

        var animalItem = items.FirstOrDefault(i => i!["name"]?.GetValue<string>() == "Animal");
        animalItem.Should().NotBeNull("should return an item with name 'Animal'");
    }

    [Fact]
    public async Task TypeHierarchy_Supertypes_ReturnsParentClass()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_typehierarchy_supertypes.spy";
        var source = "class Animal:\n    def __init__(self):\n        pass\nclass Dog(Animal):\n    def __init__(self):\n        super().__init__()\ndef main():\n    a = Animal()\n    d = Dog()";
        await _client.DidOpenAsync(uri, source);

        // Prepare on Dog (line 3, char 6) -- handler triggers analysis on-demand
        var prepareParams = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["position"] = new JsonObject { ["line"] = 3, ["character"] = 6 }
        };

        var prepareResult = await _client.SendRequestAsync("textDocument/prepareTypeHierarchy", prepareParams);
        prepareResult.Should().NotBeNull("prepareTypeHierarchy should return items for Dog");
        var preparedItems = prepareResult!.AsArray();
        preparedItems.Count.Should().BeGreaterThan(0);

        var dogItem = preparedItems.FirstOrDefault(i => i!["name"]?.GetValue<string>() == "Dog");
        dogItem.Should().NotBeNull("should find Dog in prepared items");

        // Request supertypes of Dog
        var supertypesParams = new JsonObject
        {
            ["item"] = JsonNode.Parse(dogItem!.ToJsonString())
        };

        var supertypesResult = await _client.SendRequestAsync("typeHierarchy/supertypes", supertypesParams);
        supertypesResult.Should().NotBeNull("supertypes should return results for Dog");
        var supertypes = supertypesResult!.AsArray();
        supertypes.Count.Should().BeGreaterThan(0, "Dog should have at least one supertype");

        var animalSupertype = supertypes.FirstOrDefault(i => i!["name"]?.GetValue<string>() == "Animal");
        animalSupertype.Should().NotBeNull("Animal should appear as a supertype of Dog");
    }

    [Fact]
    public async Task TypeHierarchy_Subtypes_ReturnsChildClass()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_typehierarchy_subtypes.spy";
        var source = "class Animal:\n    def __init__(self):\n        pass\nclass Dog(Animal):\n    def __init__(self):\n        super().__init__()\ndef main():\n    a = Animal()\n    d = Dog()";
        await _client.DidOpenAsync(uri, source);

        // Prepare on Animal (line 0, char 6) -- analysis triggered by handler
        var prepareParams = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["position"] = new JsonObject { ["line"] = 0, ["character"] = 6 }
        };

        var prepareResult = await _client.SendRequestAsync("textDocument/prepareTypeHierarchy", prepareParams);
        prepareResult.Should().NotBeNull("prepareTypeHierarchy should return items for Animal");
        var preparedItems = prepareResult!.AsArray();
        preparedItems.Count.Should().BeGreaterThan(0);

        var animalItem = preparedItems.FirstOrDefault(i => i!["name"]?.GetValue<string>() == "Animal");
        animalItem.Should().NotBeNull("should find Animal in prepared items");

        // Request subtypes of Animal
        var subtypesParams = new JsonObject
        {
            ["item"] = JsonNode.Parse(animalItem!.ToJsonString())
        };

        var subtypesResult = await _client.SendRequestAsync("typeHierarchy/subtypes", subtypesParams);
        subtypesResult.Should().NotBeNull("subtypes should return results for Animal");
        var subtypes = subtypesResult!.AsArray();
        subtypes.Count.Should().BeGreaterThan(0, "Animal should have at least one subtype");

        var dogSubtype = subtypes.FirstOrDefault(i => i!["name"]?.GetValue<string>() == "Dog");
        dogSubtype.Should().NotBeNull("Dog should appear as a subtype of Animal");
    }

    [Fact]
    public async Task CodeAction_ExtractVariable_ReturnsAction()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_extract_variable.spy";
        var source = "def main():\n    x: int = 1 + 2 + 3\n    print(x)";
        await _client.DidOpenAsync(uri, source);

        // Wait for diagnostics to ensure analysis is complete
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Request code actions for the range covering "1 + 2 + 3" (line 1, chars 13-22)
        var codeActionParams = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["range"] = new JsonObject
            {
                ["start"] = new JsonObject { ["line"] = 1, ["character"] = 13 },
                ["end"] = new JsonObject { ["line"] = 1, ["character"] = 22 }
            },
            ["context"] = new JsonObject
            {
                ["diagnostics"] = new JsonArray()
            }
        };

        var result = await _client.SendRequestAsync("textDocument/codeAction", codeActionParams);

        result.Should().NotBeNull("code action response should not be null");

        var actions = result!.AsArray();
        actions.Should().NotBeNull("code action response should be an array");
        actions!.Count.Should().BeGreaterThan(0,
            "should return at least one code action for a non-trivial expression");

        var titles = actions.Select(a => a!["title"]!.GetValue<string>()).ToList();
        titles.Should().Contain(t => t.Contains("Extract variable"),
            "should offer an 'Extract variable' code action");
    }

    [Fact]
    public async Task CodeAction_ImplementInterface_ReturnsAction()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_implement_interface.spy";
        // HelloGreeter implements greet() but is missing farewell(), triggering the
        // "Implement interface" code action for the missing method.
        var source = "interface IGreeter:\n    def greet(self) -> str: ...\n    def farewell(self) -> str: ...\nclass HelloGreeter(IGreeter):\n    def greet(self) -> str:\n        return \"hello\"\ndef main():\n    pass";
        await _client.DidOpenAsync(uri, source);

        // Wait for diagnostics to ensure analysis is complete
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Request code actions at the class definition line (line 3, char 6 — inside "HelloGreeter")
        var codeActionParams = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["range"] = new JsonObject
            {
                ["start"] = new JsonObject { ["line"] = 3, ["character"] = 6 },
                ["end"] = new JsonObject { ["line"] = 3, ["character"] = 6 }
            },
            ["context"] = new JsonObject
            {
                ["diagnostics"] = new JsonArray()
            }
        };

        var result = await _client.SendRequestAsync("textDocument/codeAction", codeActionParams);

        result.Should().NotBeNull("code action response should not be null");

        var actions = result!.AsArray();
        actions.Should().NotBeNull("code action response should be an array");
        actions!.Count.Should().BeGreaterThan(0,
            "should return at least one code action for a class with unimplemented interface members");

        var titles = actions.Select(a => a!["title"]!.GetValue<string>()).ToList();
        titles.Should().Contain(
            t => t.Contains("Implement interface") || t.Contains("Implement all interfaces"),
            "should offer an 'Implement interface' code action");
    }

    [Fact]
    public async Task CodeAction_OrganizeImports_ReturnsAction()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_organize_imports.spy";
        var source = "import math\nimport sys\n\ndef main():\n    print(math.pi)\n    print(len(sys.argv))";
        await _client.DidOpenAsync(uri, source);

        // Wait for diagnostics to ensure analysis is complete
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Request code actions at line 0 (the first import)
        var codeActionParams = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["range"] = new JsonObject
            {
                ["start"] = new JsonObject { ["line"] = 0, ["character"] = 0 },
                ["end"] = new JsonObject { ["line"] = 0, ["character"] = 0 }
            },
            ["context"] = new JsonObject
            {
                ["diagnostics"] = new JsonArray()
            }
        };

        var result = await _client.SendRequestAsync("textDocument/codeAction", codeActionParams);

        result.Should().NotBeNull("code action response should not be null");

        var actions = result!.AsArray();
        actions.Should().NotBeNull("code action response should be an array");
        actions!.Count.Should().BeGreaterThan(0,
            "should return at least one code action when imports are present");

        var titles = actions.Select(a => a!["title"]!.GetValue<string>()).ToList();
        titles.Should().Contain(t => t.Contains("Organize Imports"),
            "should offer an 'Organize Imports' code action");
    }

    [Fact]
    public async Task CodeAction_DiagnosticQuickFix_ReturnsAction()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_quickfix_unused.spy";
        var source = "def main():\n    x: int = 42\n    print(1)";
        await _client.DidOpenAsync(uri, source);

        // Wait for diagnostics — should include SPY0451 for unused variable 'x'
        var notification = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        notification.Should().NotBeNull();
        var diagnostics = notification["diagnostics"]?.AsArray();
        diagnostics.Should().NotBeNull();
        diagnostics!.Count.Should().BeGreaterThan(0,
            "unused variable 'x' should produce at least one diagnostic");

        // Find the SPY0451 diagnostic for the unused variable
        JsonNode? unusedVarDiag = null;
        foreach (var diag in diagnostics)
        {
            var code = diag!["code"];
            var codeStr = code is JsonValue val ? val.GetValue<string>() : code?.ToJsonString();
            if (codeStr != null && codeStr.Contains("SPY0451"))
            {
                unusedVarDiag = diag;
                break;
            }
        }

        unusedVarDiag.Should().NotBeNull(
            "should find a SPY0451 diagnostic for unused variable 'x'");

        // Request code actions at the diagnostic range, passing the diagnostic in context
        var diagRange = unusedVarDiag!["range"]!;
        var codeActionParams = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["range"] = JsonNode.Parse(diagRange.ToJsonString()),
            ["context"] = new JsonObject
            {
                ["diagnostics"] = new JsonArray(
                    JsonNode.Parse(unusedVarDiag.ToJsonString())
                )
            }
        };

        var result = await _client.SendRequestAsync("textDocument/codeAction", codeActionParams);

        result.Should().NotBeNull("code action response should not be null");

        var actions = result!.AsArray();
        actions.Should().NotBeNull("code action response should be an array");
        actions!.Count.Should().BeGreaterThan(0,
            "should return at least one quick fix for an unused variable diagnostic");

        var titles = actions.Select(a => a!["title"]!.GetValue<string>()).ToList();
        titles.Should().Contain(t => t.Contains("Prefix with '_'"),
            "should offer a 'Prefix with _' quick fix for unused variables");
    }

    [Fact]
    public async Task Shutdown_RespondsSuccessfully()
    {
        await _client.InitializeAsync();

        // Shutdown should not throw — the lifecycle is handled by DisposeAsync,
        // so we just verify initialize works and the server is responsive
        var result = await _client.SendRequestAsync("shutdown", null);
        result.Should().BeNull("shutdown returns null per LSP spec");
    }

    [Fact]
    public async Task Implementation_ReturnsImplementingClass()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_implementation.spy";
        var source = "interface IGreeter:\n    def greet(self) -> str: ...\nclass HelloGreeter(IGreeter):\n    def greet(self) -> str:\n        return \"hello\"\ndef main():\n    pass";
        await _client.DidOpenAsync(uri, source);

        // Wait for diagnostics to ensure analysis is complete
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Request implementation at IGreeter (line 0, char 10)
        var result = await _client.SendRequestAsync("textDocument/implementation", new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["position"] = new JsonObject { ["line"] = 0, ["character"] = 10 }
        });

        result.Should().NotBeNull("go-to-implementation should return results for an interface");

        // Response can be a single Location or an array of Locations.
        // Normalize to an array for uniform handling.
        JsonArray locations;
        if (result is JsonArray arr)
        {
            locations = arr;
        }
        else
        {
            // Single Location object — wrap in array
            locations = new JsonArray(JsonNode.Parse(result!.ToJsonString()));
        }

        locations.Count.Should().BeGreaterThan(0, "should find at least one implementing class");

        // HelloGreeter is defined at line 2 (0-based), so the range should point there.
        var hasLine2 = locations.Any(loc =>
            loc!["range"]?["start"]?["line"]?.GetValue<int>() == 2);
        hasLine2.Should().BeTrue(
            "at least one implementation location should point to line 2 where HelloGreeter is defined");
    }

    [Fact]
    public async Task WorkspaceSymbol_ReturnsMatchingSymbols()
    {
        await _client.InitializeAsync();

        var uri = "file:///test_workspace_symbol.spy";
        var source = "def animal_count() -> int:\n    return 42\ndef main():\n    x: int = animal_count()\n    print(x)";
        await _client.DidOpenAsync(uri, source);

        // Wait for diagnostics to ensure analysis is complete
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Query workspace symbols for "animal"
        var result = await _client.SendRequestAsync("workspace/symbol", new JsonObject
        {
            ["query"] = "animal"
        });

        result.Should().NotBeNull("workspace/symbol should return results");

        var symbols = result!.AsArray();
        symbols.Should().NotBeNull("workspace/symbol should return an array");
        symbols!.Count.Should().BeGreaterThan(0, "should find at least one symbol matching 'animal'");

        // Verify at least one result has a name containing "animal"
        var hasAnimal = symbols.Any(s =>
            s!["name"]?.GetValue<string>()?.Contains("animal", StringComparison.OrdinalIgnoreCase) == true);
        hasAnimal.Should().BeTrue("at least one symbol should have a name containing 'animal'");
    }
}
