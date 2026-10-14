using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Logging;
using Xunit;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Which route publishes a file's squiggles (#2242, plan-6ce1a5 D2 a/D3): in-process, against a
/// real <see cref="LanguageService"/> and <see cref="SharpyWorkspace"/>, with the notifications
/// captured instead of sent. The route × state matrix against a real server is
/// <c>E2E/ProjectDiagnosticRoutesTests</c>; these pin the rules that matrix cannot drive
/// deterministically — the single-file route's foreign partitions, the pre-indexing order, the
/// cancelled and the thrown analysis.
/// </summary>
public sealed class DiagnosticRoutesTests : IDisposable
{
    private const string Lib =
        "class Widget:\n"
        + "    label: str\n"
        + "\n"
        + "    def __init__(self, label: str) -> None:\n"
        + "        self.label = label\n"
        + "\n"
        + "\n"
        + "def build(label: str) -> Widget:\n"
        + "    return Widget(label)\n";

    // A declaration-phase error (duplicate class) stamped lib.spy, on line 12 — past the end of
    // any short importing file.
    private const string LibDuplicateClass = Lib + "\n\nclass Widget:\n    pass\n";

    private const string MainFrom =
        "from lib import build\n\n\ndef main() -> None:\n    print(build(\"x\").label)\n";

    private readonly HookLogger _compilerLogger = new();
    private readonly CompilerApi _workspaceApi = new();
    private readonly CompilerApi _projectApi;
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _service;
    private readonly DiagnosticRoutes _routes;
    private readonly System.Collections.Generic.List<PublishDiagnosticsParams> _sent = new();
    private readonly string _dir;

    public DiagnosticRoutesTests()
    {
        _projectApi = new CompilerApi(_compilerLogger);
        _workspace = new SharpyWorkspace(_workspaceApi, NullLogger<SharpyWorkspace>.Instance);
        _service = new LanguageService(_workspace, _projectApi, NullLogger<LanguageService>.Instance);
        var publisher = DiagnosticPublisher.ForSink(
            publication =>
            {
                lock (_sent)
                    _sent.Add(publication);
            },
            new LspConfiguration());
        _routes = new DiagnosticRoutes(_service, _workspace, publisher, NullLogger<DiagnosticRoutes>.Instance);

        _dir = IOPath.Combine(IOPath.GetTempPath(), $"sharpy_routes_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    // ---- project route ----------------------------------------------------------------------

    [Fact]
    public async Task Index_OfAFailedProject_PublishesEveryFile_ErrorsOnTheirOwnFile()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", LibDuplicateClass));
        WriteProject("main.spy", "lib.spy");

        var succeeded = await _service.InitializeProjectAsync(_dir);

        succeeded.Should().BeFalse("the duplicate class fails the build");
        Last("lib.spy").Should().Equal(new[] { "SPY0204" }, "the build reports it on lib.spy");
        Last("main.spy").Should().BeEmpty("nothing of the build's is main.spy's");
        Count("main.spy").Should().Be(1, "a failed project still publishes every file, ∅ included");
    }

    [Fact]
    public async Task Index_UnlistedImport_PublishesSpy0313OnTheImportingFile()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", Lib));
        WriteProject("main.spy");

        await _service.InitializeProjectAsync(_dir);

        Last("main.spy").Should().Equal("SPY0313");
        Count("lib.spy").Should().Be(0, "lib.spy is not a project file and no build diagnostic is stamped with it");
    }

    [Fact]
    public async Task FileOpenedBeforeIndexing_GetsASingleFilePublication_ThenTheProjects_ThenNoMoreSingleFile()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", Lib));
        WriteProject("main.spy");
        var main = UriOf("main.spy");
        _workspace.OpenDocument(main, MainFrom, 1);

        // Before indexing completes the project owns no file: the single-file route publishes.
        await PublishSingleFileAsync(main);
        Count("main.spy").Should().Be(1);
        Last("main.spy").Should().BeEmpty("the single-file closure finds the unlisted lib.spy");

        await _service.InitializeProjectAsync(_dir);
        Count("main.spy").Should().Be(2);
        Last("main.spy").Should().Equal(new[] { "SPY0313" }, "the index's publication is the build's set");

        // From now on a single-file analysis of main.spy publishes nothing: the project owns it.
        await PublishSingleFileAsync(main);
        Count("main.spy").Should().Be(2);
        Last("main.spy").Should().Equal("SPY0313");
    }

    [Fact]
    public async Task CloseOfAProjectFile_KeepsTheProjectsPublication()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", Lib));
        WriteProject("main.spy");
        await _service.InitializeProjectAsync(_dir);
        var main = UriOf("main.spy");
        _workspace.OpenDocument(main, MainFrom, 1);
        var before = Count("main.spy");

        Close(main);

        Count("main.spy").Should().Be(before, "the build still reports main.spy's error with the file closed");
        Last("main.spy").Should().Equal("SPY0313");

        // Its buffer equaled the disk text: the publication already is the disk's, nothing reruns.
        var reanalyzed = await _service.OnDocumentClosedAsync(main, MainFrom);
        reanalyzed.Should().BeFalse();
        Count("main.spy").Should().Be(before);
    }

    [Fact]
    public async Task CloseOfAProjectFile_AfterAnUnsavedEdit_RepublishesTheDiskTextsBuild()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", Lib));
        WriteProject("main.spy", "lib.spy");
        await _service.InitializeProjectAsync(_dir);
        var main = UriOf("main.spy");
        var unsaved = MainFrom + "\n\ndef extra() -> int:\n    return \"s\"\n";
        _workspace.OpenDocument(main, unsaved, 2);
        await _service.OnDocumentChangedAsync(main);
        Last("main.spy").Should().Contain("SPY0260", "the project analyzed the unsaved buffer");

        Close(main);
        var reanalyzed = await _service.OnDocumentClosedAsync(main, unsaved);

        reanalyzed.Should().BeTrue();
        Last("main.spy").Should().BeEmpty("the close discarded the buffer; on disk main.spy is clean, as `sharpyc project` reports");
        Last("lib.spy").Should().BeEmpty();
    }

    [Fact]
    public async Task Reload_FileLeavesTheProject_IsCleared_OrRepublishedFromItsOwnAnalysisWhenOpen()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", LibDuplicateClass), ("other.spy", LibDuplicateClass));
        WriteProject("main.spy", "lib.spy", "other.spy");
        await _service.InitializeProjectAsync(_dir);
        Last("lib.spy").Should().Equal("SPY0204");
        Last("other.spy").Should().Equal("SPY0204");
        var other = UriOf("other.spy");
        _workspace.OpenDocument(other, LibDuplicateClass, 1);

        WriteProject("main.spy");
        await _service.ReloadProjectAsync();

        Last("lib.spy").Should().BeEmpty("lib.spy left the project and is not open: its project squiggles go");
        Last("other.spy").Should().Equal(
            new[] { "SPY0204" }, "other.spy left the project while open: its own single-file analysis publishes it");
        Last("main.spy").Should().Contain("SPY0313", "lib.spy is now outside the source set");
    }

    [Fact]
    public async Task ProjectFileDeleted_Unloads_AndClearsEveryProjectFile()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", Lib));
        WriteProject("main.spy");
        await _service.InitializeProjectAsync(_dir);
        Last("main.spy").Should().Equal("SPY0313");

        File.Delete(IOPath.Combine(_dir, "test.spyproj"));
        await _service.ReloadProjectAsync();

        _service.IsProjectFile(UriOf("main.spy")).Should().BeFalse();
        Last("main.spy").Should().BeEmpty("there is no build any more to report SPY0313");
    }

    [Fact]
    public async Task CancelledReanalysis_PublishesNothing_TheSameReanalysisUncancelledPublishesEveryFile()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", Lib));
        WriteProject("main.spy", "lib.spy");
        await _service.InitializeProjectAsync(_dir);
        var main = UriOf("main.spy");
        _workspace.OpenDocument(main, MainFrom + "\n\ndef extra() -> int:\n    return \"s\"\n", 2);
        var before = _sent.Count;

        // Cancelled while the compiler runs — as a newer edit cancels it. The compiler does not
        // throw: it returns a result holding SPY0901 ("Analysis cancelled"), path-less.
        using (var cancelled = new CancellationTokenSource())
        {
            _compilerLogger.OnInfo = message =>
            {
                // The project analysis itself — not the fast path's parse, which logs too.
                if (message.StartsWith("Starting project analysis", StringComparison.Ordinal))
                    cancelled.Cancel();
            };
            var act = () => _service.OnDocumentChangedAsync(main, cancelled.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
            _compilerLogger.OnInfo = null;
        }

        _sent.Count.Should().Be(before, "a superseded analysis publishes nothing — not even the compiler's SPY0901");

        await _service.OnDocumentChangedAsync(main);

        _sent.Count.Should().Be(before + 2, "the uncancelled reanalysis publishes every project file");
        Last("main.spy").Should().NotBeEmpty("the buffer's type error is main.spy's");
        Last("main.spy").Should().NotContain("SPY0901");
        Last("lib.spy").Should().BeEmpty();
    }

    [Fact]
    public async Task AnalysisThatThrows_PublishesAnInternalCompilerErrorOnTheUnattributedTarget()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", Lib));
        WriteProject("main.spy", "lib.spy");
        _compilerLogger.OnInfo = _ => throw new InvalidOperationException("injected analysis failure");

        await _service.InitializeProjectAsync(_dir);

        Last("main.spy").Should().Equal(new[] { "SPY0909" }, "main.spy is the executable's entry (D5)");
        Last("lib.spy").Should().BeEmpty();

        // Positive control: the same project analyzed without the throw is clean.
        _compilerLogger.OnInfo = null;
        await _service.ReloadProjectAsync();
        Last("main.spy").Should().BeEmpty();
    }

    [Fact]
    public async Task Republish_SendsEveryProjectFileAgain_WithoutReanalysis()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", LibDuplicateClass));
        WriteProject("main.spy", "lib.spy");
        await _service.InitializeProjectAsync(_dir);
        var analysis = _service.ProjectAnalysis;
        var before = _sent.Count;

        await _service.RepublishProjectDiagnosticsAsync();

        _sent.Count.Should().Be(before + 2);
        _service.ProjectAnalysis.Should().BeSameAs(analysis, "a render-only change reanalyzes nothing");
        Last("lib.spy").Should().Equal("SPY0204");
    }

    [Fact]
    public async Task WatchedChange_OfAFileOutsideTheSourceSet_ReanalyzesTheProject()
    {
        WriteFiles(("main.spy", MainFrom));
        WriteProject("main.spy");
        await _service.InitializeProjectAsync(_dir);
        Last("main.spy").Should().Equal("SPY0300");

        WriteFiles(("lib.spy", Lib));
        await _service.OnExternalFileChangedAsync(IOPath.Combine(_dir, "lib.spy"));

        Last("main.spy").Should().Equal(
            new[] { "SPY0313" }, "the build now finds lib.spy outside the source set, as `sharpyc project` does");
    }

    // ---- single-file route: foreign partitions ----------------------------------------------

    [Fact]
    public async Task ForeignPartition_IsPublishedForAFileNoOneOwns_AndNotForAnOpenOne()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", LibDuplicateClass));
        var main = UriOf("main.spy");
        _workspace.OpenDocument(main, MainFrom, 1);

        await PublishSingleFileAsync(main);

        Last("main.spy").Should().BeEmpty();
        Last("lib.spy").Should().Equal(
            new[] { "SPY0204" }, "lib.spy is neither open nor a project file: main.spy's closure is all that shows it");

        var lib = UriOf("lib.spy");
        _workspace.OpenDocument(lib, LibDuplicateClass, 1);
        var libBefore = Count("lib.spy");

        await PublishSingleFileAsync(main);

        Count("lib.spy").Should().Be(libBefore, "an open lib.spy is published by its own analysis, not main.spy's");
    }

    [Fact]
    public async Task ForeignPartition_IsNotPublishedForAProjectFile()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", LibDuplicateClass), ("other.spy", MainFrom));
        WriteProject("main.spy", "lib.spy");
        await _service.InitializeProjectAsync(_dir);
        var other = UriOf("other.spy");
        _workspace.OpenDocument(other, MainFrom, 1);
        var libBefore = Count("lib.spy");

        await PublishSingleFileAsync(other);

        Count("other.spy").Should().Be(1, "other.spy is outside the source set: the single-file route owns it");
        Count("lib.spy").Should().Be(libBefore, "lib.spy is a project file: the project route owns it");
        Last("lib.spy").Should().Equal("SPY0204");
    }

    [Fact]
    public async Task ForeignPartition_ThatDisappearsFromTheNextResult_OrGoesWithTheClose_IsCleared()
    {
        WriteFiles(("main.spy", MainFrom), ("lib.spy", LibDuplicateClass));
        var main = UriOf("main.spy");
        _workspace.OpenDocument(main, MainFrom, 1);
        await PublishSingleFileAsync(main);
        Last("lib.spy").Should().Equal("SPY0204");

        _workspace.UpdateDocument(main, "def main() -> None:\n    pass\n", 2);
        await PublishSingleFileAsync(main);
        Last("lib.spy").Should().BeEmpty("main.spy no longer imports lib.spy");

        _workspace.UpdateDocument(main, MainFrom, 3);
        await PublishSingleFileAsync(main);
        Last("lib.spy").Should().Equal("SPY0204");

        Close(main);
        Last("lib.spy").Should().BeEmpty("the document that showed lib.spy's errors closed");
        Last("main.spy").Should().BeEmpty();
    }

    [Fact]
    public async Task ForeignPartition_HeldByTwoDocuments_StaysUntilTheLastOneDropsIt()
    {
        WriteFiles(("main.spy", MainFrom), ("second.spy", MainFrom), ("lib.spy", LibDuplicateClass));
        var main = UriOf("main.spy");
        var second = UriOf("second.spy");
        _workspace.OpenDocument(main, MainFrom, 1);
        _workspace.OpenDocument(second, MainFrom, 1);
        await PublishSingleFileAsync(main);
        await PublishSingleFileAsync(second);

        Close(main);
        Last("lib.spy").Should().Equal(new[] { "SPY0204" }, "second.spy still imports it");

        Close(second);
        Last("lib.spy").Should().BeEmpty();
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task PublishSingleFileAsync(string uri)
    {
        var result = await _workspace.GetAnalysisAsync(uri);
        result.Should().NotBeNull();
        _routes.PublishSingleFile(uri, result!, _workspace.GetSourceText(uri));
    }

    private void Close(string uri)
    {
        _workspace.CloseDocument(uri);
        _routes.OnDocumentClosed(uri);
    }

    private string[] Last(string fileName)
    {
        PublishDiagnosticsParams? last;
        lock (_sent)
            last = _sent.LastOrDefault(p => IsFor(p, fileName));

        last.Should().NotBeNull($"{fileName} must have been published");
        return last!.Diagnostics.Select(d => d.Code?.String ?? "?").OrderBy(c => c, StringComparer.Ordinal).ToArray();
    }

    private int Count(string fileName)
    {
        lock (_sent)
            return _sent.Count(p => IsFor(p, fileName));
    }

    private static bool IsFor(PublishDiagnosticsParams publication, string fileName)
        => string.Equals(IOPath.GetFileName(publication.Uri.GetFileSystemPath()), fileName, StringComparison.OrdinalIgnoreCase);

    private string UriOf(string fileName) => new Uri(IOPath.Combine(_dir, fileName)).AbsoluteUri;

    private void WriteFiles(params (string Name, string Content)[] files)
    {
        foreach (var (name, content) in files)
            File.WriteAllText(IOPath.Combine(_dir, name), content);
    }

    private void WriteProject(params string[] listed)
    {
        var items = string.Concat(listed.Select(f => $"        <SpyFile Include=\"{f}\" />\n"));
        File.WriteAllText(
            IOPath.Combine(_dir, "test.spyproj"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Project>\n    <PropertyGroup>\n"
            + "        <RootNamespace>Test</RootNamespace>\n        <OutputType>exe</OutputType>\n"
            + $"    </PropertyGroup>\n    <ItemGroup>\n{items}    </ItemGroup>\n</Project>\n");
    }

    public void Dispose()
    {
        _routes.Dispose();
        _service.Dispose();
        _workspace.Dispose();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    /// <summary>A compiler logger that runs <see cref="OnInfo"/> on <c>LogInfo</c> — the first call
    /// <c>ProjectCompiler.AnalyzeProject</c> makes, outside its own exception handler: a throw
    /// there escapes the compiler the way an unforeseen setup failure would, and a cancellation
    /// there lands while the analysis runs.</summary>
    private sealed class HookLogger : ICompilerLogger
    {
        public volatile Action<string>? OnInfo;

        public void LogInfo(string message) => OnInfo?.Invoke(message);

        public void LogTokenRead(string tokenType, int line, int column, string value) { }
        public void LogIndentChange(int oldLevel, int newLevel) { }
        public void LogParseEnter(string rule, int tokenPosition) { }
        public void LogParseExit(string rule, bool success) { }
        public void LogError(string message, int line, int column) { }
        public void LogWarning(string message, int line, int column) { }
        public void LogDebug(string message) { }
        public void LogTrace(string message) { }
        public void LogMetrics(string metricsOutput) { }
        public bool IsEnabled(CompilerLogLevel level) => false;
    }
}
