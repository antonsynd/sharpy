using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles workspace/didChangeWatchedFiles notifications.
/// Reloads .spy files changed externally and reloads project on .spyproj changes.
/// </summary>
internal sealed class FileWatcherHandler : IDidChangeWatchedFilesHandler
{
    private readonly LanguageService _languageService;

    public FileWatcherHandler(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public async Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken ct)
    {
        foreach (var change in request.Changes)
        {
            var path = change.Uri.GetFileSystemPath();

            if (path.EndsWith(".spyproj", StringComparison.OrdinalIgnoreCase))
            {
                await _languageService.ReloadProjectAsync(ct).ConfigureAwait(false);
            }
            else if (path.EndsWith(".spy", StringComparison.OrdinalIgnoreCase))
            {
                await _languageService.OnExternalFileChangedAsync(path, ct).ConfigureAwait(false);
            }
        }

        return Unit.Value;
    }

    public DidChangeWatchedFilesRegistrationOptions GetRegistrationOptions(
        DidChangeWatchedFilesCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DidChangeWatchedFilesRegistrationOptions
        {
            Watchers = new Container<OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher>(
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher
                {
                    GlobPattern = "**/*.spy"!,
                    Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete
                },
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher
                {
                    GlobPattern = "**/*.spyproj"!,
                    Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete
                }
            )
        };
    }
}
