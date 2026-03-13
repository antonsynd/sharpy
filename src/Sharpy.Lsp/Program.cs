using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Sharpy.Lsp.Refactoring;

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
                    services.AddSingleton<SharpyWorkspace>();
                    services.AddSingleton<DiagnosticPublisher>();
                    services.AddSingleton<LanguageService>();
                    // Code action providers
                    services.AddSingleton<ICodeActionProvider, DiagnosticQuickFixProvider>();
                    services.AddSingleton<ICodeActionProvider, OrganizeImportsProvider>();
                    services.AddSingleton<ICodeActionProvider, ImplementInterfaceProvider>();
                    services.AddSingleton<ICodeActionProvider, ExtractVariableProvider>();
                    services.AddSingleton<ICodeActionProvider, ExtractMethodProvider>();
                    services.AddSingleton<ICodeActionProvider, ConvertFormsProvider>();
                    services.AddSingleton<ICodeActionProvider, InlineProvider>();
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
                        var languageService = server.Services.GetRequiredService<LanguageService>();
                        var progressLogger = server.Services.GetRequiredService<ILogger<ProgressReporter>>();
                        var progressReporter = new ProgressReporter(server.WorkDoneManager, progressLogger);
                        languageService.SetProgressReporter(progressReporter);

                        // Start background indexing — returns immediately.
                        // On completion, publish diagnostics for all project files.
                        var diagnosticPublisher = server.Services.GetRequiredService<DiagnosticPublisher>();
                        languageService.StartBackgroundIndexing(rootPath, async () =>
                        {
                            foreach (var fileUri in languageService.GetProjectFileUris())
                            {
                                var result = await languageService.GetAnalysisAsync(fileUri).ConfigureAwait(false);
                                if (result != null)
                                {
                                    diagnosticPublisher.PublishDiagnostics(fileUri, result, sourceText: null);
                                }
                            }
                        });
                    }

                    return Task.CompletedTask;
                })
                .WithHandler<TextDocumentSyncHandler>()
                .WithHandler<SharpyHoverHandler>()
                .WithHandler<SharpyDefinitionHandler>()
                .WithHandler<SharpyCompletionHandler>()
                .WithHandler<SharpyReferencesHandler>()
                .WithHandler<SharpyRenameHandler>()
                .WithHandler<SharpyDocumentSymbolHandler>()
                .WithHandler<SharpySignatureHelpHandler>()
                // Phase 3 handlers
                .WithHandler<SharpySemanticTokensHandler>()
                .WithHandler<SharpyCodeActionHandler>()
                .WithHandler<SharpyFormattingHandler>()
                .WithHandler<SharpyFoldingRangeHandler>()
                .WithHandler<FileWatcherHandler>()
                // Phase 4 handlers
                .WithHandler<SharpyWorkspaceSymbolHandler>()
                .WithHandler<SharpyInlayHintHandler>()
                .WithHandler<SharpyDocumentHighlightHandler>()
                .WithHandler<SharpyCodeLensHandler>()
                // Phase 1 — Deep Structural Navigation handlers
                .WithHandler<SharpyCallHierarchyPrepareHandler>()
                .WithHandler<SharpyCallHierarchyIncomingHandler>()
                .WithHandler<SharpyCallHierarchyOutgoingHandler>()
                .WithHandler<SharpyTypeHierarchyPrepareHandler>()
                .WithHandler<SharpyTypeHierarchySupertypesHandler>()
                .WithHandler<SharpyTypeHierarchySubtypesHandler>()
                .WithHandler<SharpyImplementationHandler>()
        ).ConfigureAwait(false);

        await server.WaitForExit.ConfigureAwait(false);
    }
}
