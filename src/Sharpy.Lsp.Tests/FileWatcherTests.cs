using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class FileWatcherTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly FileWatcherHandler _handler;

    public FileWatcherTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new FileWatcherHandler(_workspace, _languageService);
    }

    [Fact]
    public async Task SpyFileChange_DoesNotThrowAsync()
    {
        // Handler should gracefully handle file change events for unknown files
        var request = new DidChangeWatchedFilesParams
        {
            Changes = new Container<FileEvent>(
                new FileEvent
                {
                    Uri = DocumentUri.FromFileSystemPath("/tmp/test.spy"),
                    Type = FileChangeType.Changed
                })
        };

        var act = async () => await _handler.Handle(request, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SpyprojFileChange_DoesNotThrowAsync()
    {
        // Handler should gracefully handle .spyproj change events even with no project loaded
        var request = new DidChangeWatchedFilesParams
        {
            Changes = new Container<FileEvent>(
                new FileEvent
                {
                    Uri = DocumentUri.FromFileSystemPath("/tmp/project.spyproj"),
                    Type = FileChangeType.Changed
                })
        };

        var act = async () => await _handler.Handle(request, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void RegistrationOptions_IncludesBothPatterns()
    {
        var options = _handler.GetRegistrationOptions(
            new DidChangeWatchedFilesCapability(),
            new ClientCapabilities());

        options.Watchers.Should().HaveCount(2);
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
