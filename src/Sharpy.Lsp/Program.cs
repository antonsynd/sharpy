using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;

namespace Sharpy.Lsp;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Store the workspace root URI from initialization for use in OnInitialized.
        Uri? workspaceRootUri = null;

        var server = await LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .WithServices(services =>
                {
                    services.AddSingleton<CompilerApi>();
                    services.AddSingleton<SharplyWorkspace>();
                    services.AddSingleton<DiagnosticPublisher>();
                })
                .OnInitialize((server, request, token) =>
                {
                    var rootUri = request.RootUri;
                    if (rootUri is not null)
                        workspaceRootUri = rootUri.ToUri();

                    return Task.CompletedTask;
                })
                .OnInitialized((server, request, response, token) =>
                {
                    var rootPath = workspaceRootUri?.LocalPath ?? request.RootPath;
                    if (rootPath != null)
                    {
                        var workspace = server.Services.GetRequiredService<SharplyWorkspace>();
                        workspace.LoadProject(rootPath);
                    }

                    return Task.CompletedTask;
                })
                .WithHandler<TextDocumentSyncHandler>()
                .WithHandler<SharplyHoverHandler>()
                .WithHandler<SharplyDefinitionHandler>()
                .WithHandler<SharplyCompletionHandler>()
                .WithHandler<SharplyReferencesHandler>()
                .WithHandler<SharplyRenameHandler>()
                .WithHandler<SharplyDocumentSymbolHandler>()
                .WithHandler<SharplySignatureHelpHandler>()
                // Phase 3 handlers
                .WithHandler<SharplySemanticTokensHandler>()
                .WithHandler<SharplyCodeActionHandler>()
                .WithHandler<SharplyFormattingHandler>()
                .WithHandler<SharplyFoldingRangeHandler>()
                .WithHandler<FileWatcherHandler>()
                // Phase 4 handlers
                .WithHandler<SharplyWorkspaceSymbolHandler>()
                .WithHandler<SharplyInlayHintHandler>()
                .WithHandler<SharplyDocumentHighlightHandler>()
                .WithHandler<SharplyCodeLensHandler>()
        ).ConfigureAwait(false);

        await server.WaitForExit.ConfigureAwait(false);
    }
}
