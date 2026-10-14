using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles workspace/didChangeWatchedFiles notifications.
/// Reanalyzes the project on .spy files changed externally and reloads it on .spyproj changes;
/// the project route publishes the results.
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

            // Both calls publish through the project route when their analysis completes (#2242).
            try
            {
                if (path.EndsWith(".spyproj", StringComparison.OrdinalIgnoreCase))
                {
                    await _languageService.ReloadProjectAsync(ct).ConfigureAwait(false);
                }
                else if (path.EndsWith(".spy", StringComparison.OrdinalIgnoreCase))
                {
                    await _languageService.OnExternalFileChangedAsync(path, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer change to the same file, which publishes.
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
