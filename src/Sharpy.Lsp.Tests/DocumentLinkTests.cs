using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests the textDocument/documentLink handler, which turns import statements
/// into clickable links to the imported module's source file.
/// </summary>
public class DocumentLinkTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _service;
    private readonly SharpyDocumentLinkHandler _handler;
    private readonly string _tempDir;

    public DocumentLinkTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _service = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyDocumentLinkHandler(_service);

        _tempDir = IOPath.GetFullPath(IOPath.Combine(
            IOPath.GetTempPath(),
            $"sharpy_doclink_test_{Guid.NewGuid()}"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task RelativeFromImport_ProducesLinkToModuleFile()
    {
        CreateProjectFiles(
            ("main.spy", "from helpers import greet\ndef main():\n    print(greet())"),
            ("helpers.spy", "def greet() -> str:\n    return \"hi\""));
        var initResult = await _service.InitializeProjectAsync(_tempDir);
        initResult.Should().BeTrue("project initialization must succeed for import resolution");

        var links = await GetLinksAsync("main.spy");

        links.Should().NotBeNull();
        links.Should().ContainSingle();

        var link = links!.Single();
        link.Target.Should().NotBeNull();
        link.Target!.GetFileSystemPath().Should().Be(IOPath.Combine(_tempDir, "helpers.spy"));
        // Range covers the "helpers" module name on line 0 (0-based), after "from ".
        link.Range.Start.Line.Should().Be(0);
        link.Range.Start.Character.Should().Be(5);
        link.Range.End.Character.Should().Be(5 + "helpers".Length);
    }

    [Fact]
    public async Task PlainImport_ProducesLinkToModuleFile()
    {
        CreateProjectFiles(
            ("main.spy", "import helpers\ndef main():\n    print(helpers.greet())"),
            ("helpers.spy", "def greet() -> str:\n    return \"hi\""));
        await _service.InitializeProjectAsync(_tempDir);

        var links = await GetLinksAsync("main.spy");

        links.Should().NotBeNull();
        links.Should().ContainSingle();

        var link = links!.Single();
        link.Target!.GetFileSystemPath().Should().Be(IOPath.Combine(_tempDir, "helpers.spy"));
    }

    [Fact]
    public async Task NonExistentImport_ProducesNoLink()
    {
        CreateProjectFiles(
            ("main.spy", "from does_not_exist import thing\ndef main():\n    pass"));
        await _service.InitializeProjectAsync(_tempDir);

        var links = await GetLinksAsync("main.spy");

        links.Should().NotBeNull();
        links.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleImports_ProduceMultipleLinks()
    {
        CreateProjectFiles(
            ("main.spy",
                "from helpers import greet\nimport util\ndef main():\n    print(greet())"),
            ("helpers.spy", "def greet() -> str:\n    return \"hi\""),
            ("util.spy", "def noop():\n    pass"));
        await _service.InitializeProjectAsync(_tempDir);

        var links = await GetLinksAsync("main.spy");

        links.Should().NotBeNull();
        links.Should().HaveCount(2);

        var targets = links!.Select(l => l.Target!.GetFileSystemPath()).ToList();
        targets.Should().Contain(IOPath.Combine(_tempDir, "helpers.spy"));
        targets.Should().Contain(IOPath.Combine(_tempDir, "util.spy"));

        // The second import is on line index 1.
        var utilLink = links!.Single(l =>
            l.Target!.GetFileSystemPath() == IOPath.Combine(_tempDir, "util.spy"));
        utilLink.Range.Start.Line.Should().Be(1);
    }

    [Fact]
    public async Task StdlibImport_ProducesNoLink()
    {
        CreateProjectFiles(
            ("main.spy", "import math\ndef main():\n    print(math.sqrt(4.0))"));
        await _service.InitializeProjectAsync(_tempDir);

        var links = await GetLinksAsync("main.spy");

        links.Should().NotBeNull();
        links.Should().BeEmpty("standard-library modules are not user-navigable source files");
    }

    [Fact]
    public async Task NoImports_ProducesNoLinks()
    {
        CreateProjectFiles(
            ("main.spy", "def main():\n    print(\"hello\")"));
        await _service.InitializeProjectAsync(_tempDir);

        var links = await GetLinksAsync("main.spy");

        links.Should().NotBeNull();
        links.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownDocument_ReturnsNull()
    {
        var request = new DocumentLinkParams
        {
            TextDocument = new TextDocumentIdentifier("file:///nonexistent.spy")
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        result.Should().BeNull();
    }

    private async Task<DocumentLinkContainer?> GetLinksAsync(string fileName)
    {
        var path = IOPath.Combine(_tempDir, fileName);
        var uri = new Uri(path).ToString();

        var request = new DocumentLinkParams
        {
            TextDocument = new TextDocumentIdentifier(uri)
        };

        return await _handler.Handle(request, CancellationToken.None);
    }

    private void CreateProjectFiles(params (string Name, string Content)[] files)
    {
        var spyFiles = string.Join("\n        ",
            files.Select(f => $"<SpyFile Include=\"{f.Name}\" />"));

        var projectContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>Test</RootNamespace>
        <OutputType>exe</OutputType>
    </PropertyGroup>
    <ItemGroup>
        {spyFiles}
    </ItemGroup>
</Project>";

        File.WriteAllText(IOPath.Combine(_tempDir, "test.spyproj"), projectContent);

        foreach (var (name, content) in files)
        {
            var filePath = IOPath.Combine(_tempDir, name);
            Directory.CreateDirectory(IOPath.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, content);
        }
    }

    public void Dispose()
    {
        _service.Dispose();
        _workspace.Dispose();

        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
