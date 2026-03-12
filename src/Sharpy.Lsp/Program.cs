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
        try
        {
            await RunServerAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(
                $"[Fatal] Sharpy LSP server crashed: {ex}").ConfigureAwait(false);
            Environment.ExitCode = 1;
        }
    }

    private static async Task RunServerAsync()
    {
        // Store the workspace root URI from initialization for use in OnInitialized.
        Uri? workspaceRootUri = null;

        var server = await LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .WithServerInfo(new ServerInfo
                {
                    Name = "sharpyc",
                    Version = Compiler.VersionInfo.InformationalVersion,
                })
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .WithServices(services =>
                {
                    services.AddSingleton<CompilerApi>();
                    services.AddSingleton<SharplyWorkspace>();
                    services.AddSingleton<DiagnosticPublisher>();
                    services.AddSingleton<LanguageService>();
                })
                .OnInitialize(async (server, request, token) =>
                {
                    var rootUri = request.RootUri;
                    if (rootUri is not null)
                        workspaceRootUri = rootUri.ToUri();

                    // Declare workspace folder support so clients send folder notifications.
                    try
                    {
                        var caps = server.ServerSettings.Capabilities;
                        caps.Workspace ??= new OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities.WorkspaceServerCapabilities();
                        caps.Workspace.WorkspaceFolders = new DidChangeWorkspaceFolderRegistrationOptions.StaticOptions
                        {
                            Supported = true,
                            ChangeNotifications = true,
                        };
                    }
                    catch (Exception ex)
                    {
                        // ServerSettings may not be fully initialized during OnInitialize.
                        await Console.Error.WriteLineAsync($"[Warning] Workspace capability setup failed: {ex.Message}").ConfigureAwait(false);
                    }
                })
                .OnInitialized((server, request, response, token) =>
                {
                    var rootPath = workspaceRootUri?.LocalPath ?? request.RootPath;
                    if (rootPath != null)
                    {
                        var workspace = server.Services.GetRequiredService<SharplyWorkspace>();
                        workspace.LoadProject(rootPath);

                        var languageService = server.Services.GetRequiredService<LanguageService>();
                        var progressLogger = server.Services.GetRequiredService<ILogger<ProgressReporter>>();
                        var progressReporter = new ProgressReporter(server.WorkDoneManager, progressLogger);
                        languageService.SetProgressReporter(progressReporter);

                        // Start background indexing — returns immediately
                        languageService.StartBackgroundIndexing(rootPath, token);
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
