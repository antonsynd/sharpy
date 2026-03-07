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
}
