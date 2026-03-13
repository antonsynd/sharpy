using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests;

public class WorkspaceSymbolTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharpyWorkspaceSymbolHandler _handler;
    private readonly string _tempDir;

    public WorkspaceSymbolTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyWorkspaceSymbolHandler(_workspace, _languageService);

        _tempDir = IOPath.Combine(
            IOPath.GetTempPath(),
            $"sharpy_ws_sym_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    private async Task<Container<WorkspaceSymbol>?> SearchSymbolsAsync(string query)
    {
        var request = new WorkspaceSymbolParams { Query = query };
        return await _handler.Handle(request, CancellationToken.None);
    }

    [Fact]
    public async Task EmptyQuery_ReturnsAllSymbolsAsync()
    {
        _workspace.OpenDocument("file:///a.spy", "def foo():\n    pass\n\ndef main():\n    foo()", 1);
        _workspace.OpenDocument("file:///b.spy", "class Bar:\n    x: int = 1", 1);

        var results = await SearchSymbolsAsync("");

        results.Should().NotBeNull();
        results!.Should().Contain(s => s.Name == "foo");
        results.Should().Contain(s => s.Name == "main");
        results.Should().Contain(s => s.Name == "Bar");
    }

    [Fact]
    public async Task FilteredQuery_ReturnsMatchingSymbolsAsync()
    {
        _workspace.OpenDocument("file:///a.spy", "def foo():\n    pass\n\ndef main():\n    foo()", 1);
        _workspace.OpenDocument("file:///b.spy", "class Bar:\n    x: int = 1", 1);

        var results = await SearchSymbolsAsync("foo");

        results.Should().NotBeNull();
        results!.Should().Contain(s => s.Name == "foo");
        results.Should().NotContain(s => s.Name == "Bar");
    }

    [Fact]
    public async Task SubstringSearch_FindsMatchAsync()
    {
        _workspace.OpenDocument("file:///a.spy", "def foo():\n    pass\n\ndef main():\n    foo()", 1);

        var results = await SearchSymbolsAsync("fo");

        results.Should().NotBeNull();
        results!.Should().Contain(s => s.Name == "foo");
    }

    [Fact]
    public async Task NoDocuments_ReturnsEmptyAsync()
    {
        var results = await SearchSymbolsAsync("");

        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithProject_ReturnsSymbolsFromAllFilesAsync()
    {
        // Only open main.spy in the workspace, but lib.spy is in the project.
        CreateProjectFiles(
            ("main.spy", "def main():\n    pass"),
            ("lib.spy", "def helper() -> str:\n    return \"hello\""));
        await _languageService.InitializeProjectAsync(_tempDir);

        var mainPath = IOPath.Combine(_tempDir, "main.spy");
        _workspace.OpenDocument(new Uri(mainPath).ToString(), "def main():\n    pass", 1);

        var results = await SearchSymbolsAsync("");

        results.Should().NotBeNull();
        results!.Should().Contain(s => s.Name == "main");
        results.Should().Contain(s => s.Name == "helper",
            "helper is in a project file that is not open in the workspace");
    }

    [Fact]
    public async Task Handle_WithProject_NoDuplicatesAsync()
    {
        CreateProjectFiles(
            ("main.spy", "def main():\n    pass"));
        await _languageService.InitializeProjectAsync(_tempDir);

        // Also open the same file in the workspace
        var mainPath = IOPath.Combine(_tempDir, "main.spy");
        _workspace.OpenDocument(new Uri(mainPath).ToString(), "def main():\n    pass", 1);

        var results = await SearchSymbolsAsync("main");

        results.Should().NotBeNull();
        results!.Where(s => s.Name == "main").Should().HaveCount(1,
            "the same file should not produce duplicate symbols");
    }

    [Fact]
    public async Task Handle_FuzzyMatch_CamelCaseQueryAsync()
    {
        _workspace.OpenDocument("file:///a.spy",
            "class FileBasedTest:\n    x: int = 1", 1);

        var results = await SearchSymbolsAsync("FBT");

        results.Should().NotBeNull();
        results!.Should().Contain(s => s.Name == "FileBasedTest");
    }

    [Theory]
    [InlineData("FileBasedTest", "FBT", true)]
    [InlineData("FileBasedTest", "FB", true)]
    [InlineData("FileBasedTest", "FT", true)]
    [InlineData("my_helper_func", "mhf", true)]
    [InlineData("my_helper_func", "mf", true)]
    [InlineData("foo", "f", true)]
    [InlineData("foo", "F", true)]
    [InlineData("Foo", "f", true)]
    [InlineData("", "f", false)]
    [InlineData("foo", "", true)]
    public void MatchesCamelCase_VariousCases(string symbolName, string query, bool expected)
    {
        SharpyWorkspaceSymbolHandler.MatchesCamelCase(symbolName, query)
            .Should().Be(expected);
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

        File.WriteAllText(
            IOPath.Combine(_tempDir, "test.spyproj"),
            projectContent);

        foreach (var (name, content) in files)
        {
            var filePath = IOPath.Combine(_tempDir, name);
            var dir = IOPath.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, content);
        }
    }

    public void Dispose()
    {
        _languageService.Dispose();
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
