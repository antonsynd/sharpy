using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Xunit;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Unit tests for <see cref="LanguageService"/>.
/// Tests project initialization, single-file fallback, dependency cascade,
/// per-file result extraction, and project reload.
/// </summary>
public class LanguageServiceTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;
    private readonly LanguageService _service;
    private readonly string _tempDir;

    public LanguageServiceTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
        _service = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);

        _tempDir = IOPath.Combine(
            IOPath.GetTempPath(),
            $"sharpy_ls_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task InitializeProject_WithValidProject_LoadsAndAnalyzes()
    {
        CreateProjectFiles(
            ("main.spy", "def main():\n    print(\"hello\")"));

        var result = await _service.InitializeProjectAsync(_tempDir);

        result.Should().BeTrue();
        _service.HasProject.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeProject_NoProjectFile_ReturnsFalse()
    {
        // Empty directory with no .spyproj
        var result = await _service.InitializeProjectAsync(_tempDir);

        result.Should().BeFalse();
        _service.HasProject.Should().BeFalse();
    }

    [Fact]
    public async Task GetAnalysis_ProjectFile_ReturnsProjectResult()
    {
        CreateProjectFiles(
            ("main.spy", "def main():\n    x: int = 42\n    print(x)"));

        await _service.InitializeProjectAsync(_tempDir);

        var mainPath = IOPath.Combine(_tempDir, "main.spy");
        var uri = new Uri(mainPath).ToString();

        var analysis = await _service.GetAnalysisAsync(uri);
        analysis.Should().NotBeNull();
        analysis!.Success.Should().BeTrue();
        analysis.Ast.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAnalysis_NonProjectFile_FallsBackToWorkspace()
    {
        // Initialize project but ask for a file not in the project
        CreateProjectFiles(
            ("main.spy", "def main():\n    print(\"hello\")"));
        await _service.InitializeProjectAsync(_tempDir);

        // Open a standalone file in the workspace
        _workspace.OpenDocument("file:///standalone.spy", "x: int = 1", 1);

        var analysis = await _service.GetAnalysisAsync("file:///standalone.spy");
        analysis.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAnalysis_NoProject_FallsBackToWorkspace()
    {
        // No project initialized
        _workspace.OpenDocument("file:///test.spy", "x: int = 1", 1);

        var analysis = await _service.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();
    }

    [Fact]
    public async Task IsProjectFile_ProjectFile_ReturnsTrue()
    {
        CreateProjectFiles(
            ("main.spy", "def main():\n    print(\"hello\")"));
        await _service.InitializeProjectAsync(_tempDir);

        var mainPath = IOPath.Combine(_tempDir, "main.spy");
        var uri = new Uri(mainPath).ToString();

        _service.IsProjectFile(uri).Should().BeTrue();
    }

    [Fact]
    public async Task IsProjectFile_NonProjectFile_ReturnsFalse()
    {
        CreateProjectFiles(
            ("main.spy", "def main():\n    print(\"hello\")"));
        await _service.InitializeProjectAsync(_tempDir);

        _service.IsProjectFile("file:///other.spy").Should().BeFalse();
    }

    [Fact]
    public async Task GetProjectFileUris_ReturnsAllProjectFiles()
    {
        CreateProjectFiles(
            ("main.spy", "def main():\n    print(\"hello\")"),
            ("lib.spy", "def helper() -> str:\n    return \"hello\""));
        await _service.InitializeProjectAsync(_tempDir);

        var uris = _service.GetProjectFileUris();
        uris.Should().HaveCount(2);
    }

    [Fact]
    public async Task OnDocumentChanged_ProjectFile_ReturnsAffectedUris()
    {
        CreateProjectFiles(
            ("main.spy", "from lib import helper\ndef main():\n    print(helper())"),
            ("lib.spy", "def helper() -> str:\n    return \"hello\""));
        await _service.InitializeProjectAsync(_tempDir);

        var mainPath = IOPath.Combine(_tempDir, "main.spy");
        var mainUri = new Uri(mainPath).ToString();

        var affected = await _service.OnDocumentChangedAsync(mainUri);
        affected.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OnDocumentChanged_NonProjectFile_ReturnsEmpty()
    {
        CreateProjectFiles(
            ("main.spy", "def main():\n    print(\"hello\")"));
        await _service.InitializeProjectAsync(_tempDir);

        var affected = await _service.OnDocumentChangedAsync("file:///other.spy");
        affected.Should().BeEmpty();
    }

    [Fact]
    public async Task OnDocumentChanged_NoProject_ReturnsEmpty()
    {
        var affected = await _service.OnDocumentChangedAsync("file:///test.spy");
        affected.Should().BeEmpty();
    }

    [Fact]
    public async Task ReloadProject_AfterInit_ReloadsSuccessfully()
    {
        CreateProjectFiles(
            ("main.spy", "def main():\n    print(\"hello\")"));
        await _service.InitializeProjectAsync(_tempDir);

        // Add a new file to the project
        File.WriteAllText(
            IOPath.Combine(_tempDir, "lib.spy"),
            "def helper() -> str:\n    return \"world\"");
        UpdateProjectFile(("main.spy", null!), ("lib.spy", null!));

        var reloaded = await _service.ReloadProjectAsync();
        reloaded.Should().BeTrue();

        var uris = _service.GetProjectFileUris();
        uris.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReloadProject_WithoutInit_ReturnsFalse()
    {
        var result = await _service.ReloadProjectAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task OnExternalFileChanged_ProjectFile_TriggersReanalysis()
    {
        CreateProjectFiles(
            ("main.spy", "def main():\n    print(\"hello\")"));
        await _service.InitializeProjectAsync(_tempDir);

        var mainPath = IOPath.Combine(_tempDir, "main.spy");
        var affected = await _service.OnExternalFileChangedAsync(mainPath);
        affected.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OnExternalFileChanged_NonProjectFile_ReturnsEmpty()
    {
        CreateProjectFiles(
            ("main.spy", "def main():\n    print(\"hello\")"));
        await _service.InitializeProjectAsync(_tempDir);

        var affected = await _service.OnExternalFileChangedAsync("/nonexistent/file.spy");
        affected.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeProject_WithErrors_StillCachesResults()
    {
        CreateProjectFiles(
            ("main.spy", "x: int = \"not an int\""));

        var result = await _service.InitializeProjectAsync(_tempDir);
        // Project may fail due to type errors but should still cache file results
        _service.HasProject.Should().BeTrue();

        var mainPath = IOPath.Combine(_tempDir, "main.spy");
        var uri = new Uri(mainPath).ToString();
        var analysis = await _service.GetAnalysisAsync(uri);
        analysis.Should().NotBeNull();
    }

    [Fact]
    public async Task MultiFile_CrossFileTypes_Resolved()
    {
        CreateProjectFiles(
            ("main.spy", "from lib import Point\ndef main():\n    p: Point = Point(1, 2)\n    print(p.x)"),
            ("lib.spy", "class Point:\n    x: int\n    y: int\n    def __init__(self, x: int, y: int):\n        self.x = x\n        self.y = y"));
        await _service.InitializeProjectAsync(_tempDir);

        var mainPath = IOPath.Combine(_tempDir, "main.spy");
        var uri = new Uri(mainPath).ToString();

        var analysis = await _service.GetAnalysisAsync(uri);
        analysis.Should().NotBeNull();
        analysis!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Dependencies_AfterInit_IsAvailable()
    {
        CreateProjectFiles(
            ("main.spy", "from lib import helper\ndef main():\n    print(helper())"),
            ("lib.spy", "def helper() -> str:\n    return \"hello\""));
        await _service.InitializeProjectAsync(_tempDir);

        _service.Dependencies.Should().NotBeNull();
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

    private void UpdateProjectFile(params (string Name, string? _)[] files)
    {
        var allFiles = Directory.GetFiles(_tempDir, "*.spy")
            .Select(f => IOPath.GetFileName(f));

        var spyFiles = string.Join("\n        ",
            allFiles.Select(f => $"<SpyFile Include=\"{f}\" />"));

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
