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
    public async Task DecoratedPlainImport_ProducesSameLinkAsUndecoratedTwin()
    {
        // A statement-scoped @suppress wraps the import in a DecoratedStatement (#1124/#1125).
        // The decorated import must produce the same link as its undecorated twin — same target
        // and same column span, only shifted down by the one decorator line.
        var helper = ("helpers.spy", "def greet() -> str:\n    return \"hi\"");
        var undecorated = await LinksForIsolatedProjectAsync(
            "import helpers\ndef main():\n    print(helpers.greet())", helper);
        var decorated = await LinksForIsolatedProjectAsync(
            "@suppress(\"SPY0452\")\nimport helpers\ndef main():\n    print(helpers.greet())", helper);

        AssertLinksMatchWithLineOffset(undecorated, decorated, lineOffset: 1);
    }

    [Fact]
    public async Task DecoratedFromImport_ProducesSameLinkAsUndecoratedTwin()
    {
        // From-import variant of the decorated-import guard (#1125).
        var helper = ("helpers.spy", "def greet() -> str:\n    return \"hi\"");
        var undecorated = await LinksForIsolatedProjectAsync(
            "from helpers import greet\ndef main():\n    print(greet())", helper);
        var decorated = await LinksForIsolatedProjectAsync(
            "@suppress(\"SPY0452\")\nfrom helpers import greet\ndef main():\n    print(greet())", helper);

        AssertLinksMatchWithLineOffset(undecorated, decorated, lineOffset: 1);
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

    /// <summary>
    /// Computes document links for <paramref name="mainContent"/> (as main.spy) plus any supporting
    /// files, in an isolated project + service so a decorated source can be compared against its
    /// undecorated twin without shared-state contamination. Returns a detached list; the backing
    /// files are deleted before returning, but link ranges and target file names remain comparable.
    /// </summary>
    private async Task<IReadOnlyList<DocumentLink>> LinksForIsolatedProjectAsync(
        string mainContent, params (string Name, string Content)[] supporting)
    {
        var dir = IOPath.GetFullPath(IOPath.Combine(
            IOPath.GetTempPath(), $"sharpy_doclink_twin_{Guid.NewGuid()}"));
        Directory.CreateDirectory(dir);
        try
        {
            var files = new List<(string Name, string Content)> { ("main.spy", mainContent) };
            foreach (var f in supporting)
                files.Add(f);

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
            File.WriteAllText(IOPath.Combine(dir, "test.spyproj"), projectContent);
            foreach (var (name, content) in files)
                File.WriteAllText(IOPath.Combine(dir, name), content);

            using var workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
            using var service = new LanguageService(workspace, _api, NullLogger<LanguageService>.Instance);
            var handler = new SharpyDocumentLinkHandler(service);
            await service.InitializeProjectAsync(dir);

            var uri = new Uri(IOPath.Combine(dir, "main.spy")).ToString();
            var result = await handler.Handle(
                new DocumentLinkParams { TextDocument = new TextDocumentIdentifier(uri) },
                CancellationToken.None);

            return result is null ? new List<DocumentLink>() : result.ToList();
        }
        finally
        {
            try
            { Directory.Delete(dir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// Asserts the <paramref name="decorated"/> links are the same as the <paramref name="undecorated"/>
    /// twin's: same count, same target file per link, same column span, with the line shifted down by
    /// exactly <paramref name="lineOffset"/> decorator line(s).
    /// </summary>
    private static void AssertLinksMatchWithLineOffset(
        IReadOnlyList<DocumentLink> undecorated, IReadOnlyList<DocumentLink> decorated, int lineOffset)
    {
        undecorated.Should().NotBeEmpty("the undecorated twin must produce at least one link");
        decorated.Should().HaveCount(undecorated.Count,
            "a decorated import must produce the same number of links as its undecorated twin");

        for (var i = 0; i < undecorated.Count; i++)
        {
            var u = undecorated[i];
            var d = decorated[i];

            IOPath.GetFileName(d.Target!.GetFileSystemPath())
                .Should().Be(IOPath.GetFileName(u.Target!.GetFileSystemPath()),
                    "the decorated import must link to the same module file");

            // Columns identical; only the line shifts down by the decorator line(s).
            d.Range.Start.Character.Should().Be(u.Range.Start.Character);
            d.Range.End.Character.Should().Be(u.Range.End.Character);
            d.Range.Start.Line.Should().Be(u.Range.Start.Line + lineOffset);
            d.Range.End.Line.Should().Be(u.Range.End.Line + lineOffset);
        }
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
