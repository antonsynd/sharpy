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
    private readonly SharplyWorkspace _workspace;

    public FileWatcherHandler(SharplyWorkspace workspace)
    {
        _workspace = workspace;
    }

    public Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken ct)
    {
        foreach (var change in request.Changes)
        {
            var path = change.Uri.GetFileSystemPath();

            if (path.EndsWith(".spyproj", StringComparison.OrdinalIgnoreCase))
            {
                _workspace.ReloadProject();
            }
            else if (path.EndsWith(".spy", StringComparison.OrdinalIgnoreCase))
            {
                _workspace.OnExternalFileChanged(path);
            }
        }

        return Unit.Task;
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
