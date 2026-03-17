using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Lsp.Tests.E2E;

/// <summary>
/// End-to-end tests that verify the LSP server handles multi-file projects
/// with .spyproj files, cross-file diagnostics, and cross-file references.
/// </summary>
public class MultiFileTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private LspTestClient _client = null!;
    private string _tempDir = null!;

    public MultiFileTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"sharpy_e2e_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

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

        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    [Fact]
    public async Task MultiFile_OpenProject_IndividualFilesAnalyze()
    {
        // Create a multi-file project with self-contained files
        CreateProjectFiles(
            ("main.spy", "def main():\n    print(\"hello\")"),
            ("lib.spy", "def helper() -> str:\n    return \"hello\""));

        // Initialize with workspace root pointing to temp dir
        var rootUri = new Uri(_tempDir).AbsoluteUri;
        await _client.InitializeAsync(rootUri);

        // Open the main file
        var mainUri = new Uri(System.IO.Path.Combine(_tempDir, "main.spy")).AbsoluteUri;
        var mainText = File.ReadAllText(System.IO.Path.Combine(_tempDir, "main.spy"));
        await _client.DidOpenAsync(mainUri, mainText);

        // Wait for diagnostics on the main file
        var notification = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        notification.Should().NotBeNull();

        var diagnostics = notification["diagnostics"]?.AsArray();
        diagnostics.Should().NotBeNull();
        var errors = diagnostics!
            .Where(d => d!["severity"]?.GetValue<int>() == 1) // 1 = Error
            .ToList();
        errors.Should().BeEmpty("self-contained file in a project should have no errors");

        // Also open the lib file and verify it analyzes independently
        var libUri = new Uri(System.IO.Path.Combine(_tempDir, "lib.spy")).AbsoluteUri;
        var libText = File.ReadAllText(System.IO.Path.Combine(_tempDir, "lib.spy"));
        await _client.DidOpenAsync(libUri, libText);

        var libNotification = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var libDiagnostics = libNotification["diagnostics"]?.AsArray();
        libDiagnostics.Should().NotBeNull();
        var libErrors = libDiagnostics!
            .Where(d => d!["severity"]?.GetValue<int>() == 1)
            .ToList();
        libErrors.Should().BeEmpty("lib file should also have no errors");
    }

    [Fact]
    public async Task MultiFile_OpenFile_WithoutProject_StillAnalyzes()
    {
        // Initialize without a workspace root (single-file mode)
        await _client.InitializeAsync();

        var uri = "file:///standalone.spy";
        await _client.DidOpenAsync(uri, "def main():\n    x: int = 42\n    print(x)");

        var notification = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        notification.Should().NotBeNull();

        var diagnostics = notification["diagnostics"]?.AsArray();
        diagnostics.Should().NotBeNull();
        diagnostics!.Count.Should().Be(0, "valid standalone file should have no diagnostics");
    }

    [Fact]
    public async Task MultiFile_CrossFileError_ProducesDiagnostic()
    {
        // Create a project where main.spy imports a non-existent symbol
        CreateProjectFiles(
            ("main.spy", "from lib import nonexistent\ndef main():\n    print(nonexistent())"),
            ("lib.spy", "def helper() -> str:\n    return \"hello\""));

        var rootUri = new Uri(_tempDir).AbsoluteUri;
        await _client.InitializeAsync(rootUri);

        var mainUri = new Uri(System.IO.Path.Combine(_tempDir, "main.spy")).AbsoluteUri;
        var mainText = File.ReadAllText(System.IO.Path.Combine(_tempDir, "main.spy"));
        await _client.DidOpenAsync(mainUri, mainText);

        var notification = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        notification.Should().NotBeNull();

        var diagnostics = notification["diagnostics"]?.AsArray();
        diagnostics.Should().NotBeNull();
        diagnostics!.Count.Should().BeGreaterThan(0,
            "importing a non-existent symbol should produce a diagnostic");
    }

    [Fact]
    public async Task MultiFile_CrossFileTypeError_ProducesDiagnostic()
    {
        // main.spy uses helper() result as int, but it returns str
        CreateProjectFiles(
            ("main.spy", "from lib import helper\ndef main():\n    x: int = helper()\n    print(x)"),
            ("lib.spy", "def helper() -> str:\n    return \"hello\""));

        var rootUri = new Uri(_tempDir).AbsoluteUri;
        await _client.InitializeAsync(rootUri);

        var mainUri = new Uri(System.IO.Path.Combine(_tempDir, "main.spy")).AbsoluteUri;
        var mainText = File.ReadAllText(System.IO.Path.Combine(_tempDir, "main.spy"));
        await _client.DidOpenAsync(mainUri, mainText);

        var notification = await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        var diagnostics = notification["diagnostics"]?.AsArray();
        diagnostics.Should().NotBeNull();
        diagnostics!.Count.Should().BeGreaterThan(0,
            "type mismatch from cross-file import should produce a diagnostic");
    }

    [Fact]
    public async Task MultiFile_EditIntroducesError_ThenFixes()
    {
        // Start with valid code
        CreateProjectFiles(
            ("main.spy", "def main():\n    x: int = 42\n    print(x)"));

        var rootUri = new Uri(_tempDir).AbsoluteUri;
        await _client.InitializeAsync(rootUri);

        var mainUri = new Uri(System.IO.Path.Combine(_tempDir, "main.spy")).AbsoluteUri;
        var mainText = File.ReadAllText(System.IO.Path.Combine(_tempDir, "main.spy"));
        await _client.DidOpenAsync(mainUri, mainText);

        // Wait for a clean diagnostic notification. Background project indexing and
        // single-file analysis can both publish diagnostics, so drain until we see
        // the expected result (skipping any stale or duplicate notifications).
        var firstDiag = await WaitForDiagnosticsMatchingAsync(
            n => n["diagnostics"]!.AsArray()!.Count == 0,
            TimeSpan.FromSeconds(15),
            "initial valid code should have no diagnostics");

        // Introduce a type error
        await _client.DidChangeAsync(mainUri, "def main():\n    x: int = \"bad\"\n    print(x)", 2);

        // Drain stale notifications (e.g. from background indexing finishing after
        // didOpen) and wait for the notification with actual error diagnostics.
        var secondDiag = await WaitForDiagnosticsMatchingAsync(
            n => n["diagnostics"]!.AsArray()!.Count > 0,
            TimeSpan.FromSeconds(15),
            "type error should produce diagnostics");

        // Fix the error
        await _client.DidChangeAsync(mainUri, "def main():\n    x: int = 99\n    print(x)", 3);

        var thirdDiag = await WaitForDiagnosticsMatchingAsync(
            n => n["diagnostics"]!.AsArray()!.Count == 0,
            TimeSpan.FromSeconds(15),
            "fixed code should clear diagnostics");
    }

    [Fact]
    public async Task MultiFile_HoverOnImportedSymbol_ReturnsInfo()
    {
        CreateProjectFiles(
            ("main.spy", "from lib import helper\ndef main():\n    helper()"),
            ("lib.spy", "def helper() -> str:\n    return \"hello\""));

        var rootUri = new Uri(_tempDir).AbsoluteUri;
        await _client.InitializeAsync(rootUri);

        var mainUri = new Uri(System.IO.Path.Combine(_tempDir, "main.spy")).AbsoluteUri;
        var mainText = File.ReadAllText(System.IO.Path.Combine(_tempDir, "main.spy"));
        await _client.DidOpenAsync(mainUri, mainText);

        // Wait for initial didOpen diagnostics
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Poll hover until background indexing completes and project-level analysis
        // resolves the imported symbol. Single-file analysis returns null for cross-file
        // symbols, but once project indexing finishes, the cached result includes imports.
        JsonNode? hover = null;
        var timeout = TimeSpan.FromSeconds(30);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            // Drain any pending diagnostics notifications (from background indexing)
            try
            {
                await _client.WaitForNotificationAsync(
                    "textDocument/publishDiagnostics",
                    TimeSpan.FromMilliseconds(500));
            }
            catch (TimeoutException)
            {
                // No pending notifications
            }

            hover = await _client.HoverAsync(mainUri, 2, 4);
            if (hover != null)
                break;

            await Task.Delay(200);
        }

        hover.Should().NotBeNull("hovering over an imported function should return info after project indexing completes");
    }

    [Fact]
    public async Task MultiFile_CallHierarchy_CrossFileCalls()
    {
        CreateProjectFiles(
            ("lib.spy", "def helper() -> int:\n    return 42"),
            ("main.spy", "from lib import helper\ndef main():\n    helper()"));

        var rootUri = new Uri(_tempDir).AbsoluteUri;
        await _client.InitializeAsync(rootUri);

        var libUri = new Uri(System.IO.Path.Combine(_tempDir, "lib.spy")).AbsoluteUri;
        var libText = File.ReadAllText(System.IO.Path.Combine(_tempDir, "lib.spy"));
        await _client.DidOpenAsync(libUri, libText);

        var mainUri = new Uri(System.IO.Path.Combine(_tempDir, "main.spy")).AbsoluteUri;
        var mainText = File.ReadAllText(System.IO.Path.Combine(_tempDir, "main.spy"));
        await _client.DidOpenAsync(mainUri, mainText);

        // Wait for initial diagnostics from both files
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Poll for call hierarchy — background indexing may need time
        JsonNode? prepareResult = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            try
            {
                await _client.WaitForNotificationAsync(
                    "textDocument/publishDiagnostics",
                    TimeSpan.FromMilliseconds(500));
            }
            catch (TimeoutException)
            {
                // No pending notifications
            }

            prepareResult = await _client.SendRequestAsync(
                "textDocument/prepareCallHierarchy",
                new JsonObject
                {
                    ["textDocument"] = new JsonObject { ["uri"] = libUri },
                    ["position"] = new JsonObject { ["line"] = 0, ["character"] = 4 }
                });

            if (prepareResult is JsonArray { Count: > 0 })
                break;

            await Task.Delay(200);
        }

        prepareResult.Should().NotBeNull("prepareCallHierarchy should return items for 'helper'");
        var prepareArray = prepareResult as JsonArray;
        prepareArray.Should().NotBeNull();
        prepareArray!.Count.Should().BeGreaterThan(0, "should have at least one call hierarchy item");

        var item = prepareArray[0]!;

        // Now request incoming calls
        JsonNode? incomingResult = null;
        sw.Restart();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            try
            {
                await _client.WaitForNotificationAsync(
                    "textDocument/publishDiagnostics",
                    TimeSpan.FromMilliseconds(500));
            }
            catch (TimeoutException)
            {
                // No pending notifications
            }

            incomingResult = await _client.SendRequestAsync(
                "callHierarchy/incomingCalls",
                new JsonObject
                {
                    ["item"] = JsonNode.Parse(item.ToJsonString())
                });

            if (incomingResult is JsonArray { Count: > 0 })
                break;

            await Task.Delay(200);
        }

        incomingResult.Should().NotBeNull("incomingCalls should return results");
        var incomingArray = incomingResult as JsonArray;
        incomingArray.Should().NotBeNull();
        incomingArray!.Count.Should().BeGreaterThan(0,
            "helper() is called from main.spy, so there should be at least one incoming call");

        // Verify at least one incoming call comes from main.spy
        var hasCallFromMain = incomingArray.Any(call =>
        {
            var fromUri = call?["from"]?["uri"]?.GetValue<string>();
            return fromUri != null && fromUri.Contains("main.spy");
        });
        hasCallFromMain.Should().BeTrue(
            "incoming calls should include a call from main.spy");
    }

    [Fact]
    public async Task MultiFile_Implementation_CrossFile()
    {
        CreateProjectFiles(
            ("interfaces.spy", "interface IService:\n    def run(self) -> None: ...\n"),
            ("impl.spy", "from interfaces import IService\nclass MyService(IService):\n    def run(self) -> None:\n        pass\ndef main():\n    pass\n"));

        var rootUri = new Uri(_tempDir).AbsoluteUri;
        await _client.InitializeAsync(rootUri);

        var interfacesUri = new Uri(System.IO.Path.Combine(_tempDir, "interfaces.spy")).AbsoluteUri;
        var interfacesText = File.ReadAllText(System.IO.Path.Combine(_tempDir, "interfaces.spy"));
        await _client.DidOpenAsync(interfacesUri, interfacesText);

        var implUri = new Uri(System.IO.Path.Combine(_tempDir, "impl.spy")).AbsoluteUri;
        var implText = File.ReadAllText(System.IO.Path.Combine(_tempDir, "impl.spy"));
        await _client.DidOpenAsync(implUri, implText);

        // Best-effort wait for initial diagnostics — continue to the polling loop
        // even if none arrive yet (background indexing or single-file analysis may
        // still be in progress).
        try
        {
            await _client.WaitForNotificationAsync(
                "textDocument/publishDiagnostics",
                TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException)
        {
            // Diagnostics may arrive during the polling loop below
        }

        // Poll for implementation results — background project indexing may need
        // time before the cross-file symbol table is available.
        JsonNode? implResult = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            try
            {
                await _client.WaitForNotificationAsync(
                    "textDocument/publishDiagnostics",
                    TimeSpan.FromMilliseconds(500));
            }
            catch (TimeoutException)
            {
                // No pending notifications
            }

            try
            {
                implResult = await _client.SendRequestAsync(
                    "textDocument/implementation",
                    new JsonObject
                    {
                        ["textDocument"] = new JsonObject { ["uri"] = interfacesUri },
                        ["position"] = new JsonObject { ["line"] = 0, ["character"] = 10 }
                    });
            }
            catch (Exception ex) when (ex is System.IO.IOException or TimeoutException)
            {
                // Server may crash from a pre-existing Debug.Assert bug
                // when analyzing interface-only files in single-file mode.
                break;
            }

            // Response can be a JsonArray of Locations, a single Location JsonObject,
            // or null. Break on any non-null result.
            if (implResult is JsonArray { Count: > 0 } || implResult is JsonObject)
                break;

            await Task.Delay(200);
        }

        // Verify the server returned the MyService implementation location.
        // The response can be a JsonArray of Locations or a single Location JsonObject.
        implResult.Should().NotBeNull(
            "textDocument/implementation should return results for a cross-file interface");

        // Normalize to a list of locations for uniform checking.
        var locations = new System.Collections.Generic.List<JsonNode?>();
        if (implResult is JsonArray arr)
        {
            foreach (var item in arr)
                locations.Add(item);
        }
        else
        {
            locations.Add(implResult);
        }

        locations.Should().NotBeEmpty(
            "IService has an implementing class MyService in impl.spy");

        var hasMyService = locations.Any(loc =>
        {
            var locUri = loc?["uri"]?.GetValue<string>();
            return locUri != null && locUri.Contains("impl.spy");
        });
        hasMyService.Should().BeTrue(
            "implementation results should include MyService from impl.spy");
    }

    [Fact]
    public async Task MultiFile_WorkspaceSymbol_CrossFile()
    {
        CreateProjectFiles(
            ("models.spy", "class Animal:\n    name: str\n    def speak(self) -> str:\n        return self.name\ndef main():\n    pass"),
            ("utils.spy", "def format_name(name: str) -> str:\n    return name"));

        var rootUri = new Uri(_tempDir).AbsoluteUri;
        await _client.InitializeAsync(rootUri);

        // Only open models.spy — workspace symbols should still work
        var modelsUri = new Uri(System.IO.Path.Combine(_tempDir, "models.spy")).AbsoluteUri;
        var modelsText = File.ReadAllText(System.IO.Path.Combine(_tempDir, "models.spy"));
        await _client.DidOpenAsync(modelsUri, modelsText);

        // Wait for initial diagnostics
        await _client.WaitForNotificationAsync(
            "textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(15));

        // Poll for workspace symbols — background indexing may need time
        JsonNode? symbolResult = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            try
            {
                await _client.WaitForNotificationAsync(
                    "textDocument/publishDiagnostics",
                    TimeSpan.FromMilliseconds(500));
            }
            catch (TimeoutException)
            {
                // No pending notifications
            }

            symbolResult = await _client.SendRequestAsync(
                "workspace/symbol",
                new JsonObject
                {
                    ["query"] = "Animal"
                });

            if (symbolResult is JsonArray { Count: > 0 })
                break;

            await Task.Delay(200);
        }

        symbolResult.Should().NotBeNull("workspace/symbol should return results");
        var symbolArray = symbolResult as JsonArray;
        symbolArray.Should().NotBeNull();
        symbolArray!.Count.Should().BeGreaterThan(0,
            "querying 'Animal' should find the class from models.spy");

        // Verify at least one result has the name "Animal"
        var hasAnimal = symbolArray.Any(sym =>
        {
            var name = sym?["name"]?.GetValue<string>();
            return name != null && name.Contains("Animal");
        });
        hasAnimal.Should().BeTrue(
            "workspace symbols should include 'Animal' from models.spy");
    }

    private void CreateProjectFiles(params (string Name, string Content)[] files)
    {
        // Create .spyproj
        var spyFiles = string.Join("\n        ",
            files.Select(f => $"<SpyFile Include=\"{f.Name}\" />"));

        var projectContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>E2ETest</RootNamespace>
        <OutputType>exe</OutputType>
    </PropertyGroup>
    <ItemGroup>
        {spyFiles}
    </ItemGroup>
</Project>";

        File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "test.spyproj"),
            projectContent);

        foreach (var (name, content) in files)
        {
            var filePath = System.IO.Path.Combine(_tempDir, name);
            var dir = System.IO.Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, content);
        }
    }

    /// <summary>
    /// Waits for a publishDiagnostics notification matching the given predicate,
    /// skipping any stale or duplicate notifications that don't match.
    /// This handles the race between background project indexing and single-file
    /// analysis, which can both publish diagnostic notifications.
    /// </summary>
    private async Task<JsonNode> WaitForDiagnosticsMatchingAsync(
        Func<JsonNode, bool> predicate,
        TimeSpan timeout,
        string because)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var remaining = timeout - sw.Elapsed;
            if (remaining <= TimeSpan.Zero)
                break;

            try
            {
                var notification = await _client.WaitForNotificationAsync(
                    "textDocument/publishDiagnostics",
                    remaining < TimeSpan.FromSeconds(2) ? remaining : TimeSpan.FromSeconds(2));

                if (predicate(notification))
                    return notification;

                _output.WriteLine(
                    $"Skipped stale diagnostic notification ({notification["diagnostics"]?.AsArray()?.Count ?? 0} diagnostics)");
            }
            catch (TimeoutException) when (sw.Elapsed < timeout)
            {
                // No notification yet, keep polling
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"Expected diagnostics matching condition because {because}, but timed out after {timeout.TotalSeconds}s");
    }
}
