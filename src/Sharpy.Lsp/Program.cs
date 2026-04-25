extern alias SharpyRT;

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
                    // Register CompilerApi with Sharpy.Core.dll as a default reference
                    // so that stdlib modules (os, bisect, math, etc.) are resolvable
                    // in both project-level and single-file analysis.
                    var sharpyCoreAssembly = typeof(SharpyRT::Sharpy.Builtins).Assembly;
                    var sharpyCorePath = sharpyCoreAssembly.Location;
                    services.AddSingleton(new CompilerApi(null, new[] { sharpyCorePath }));
                    services.AddSingleton<SharpyWorkspace>();
                    services.AddSingleton<DiagnosticPublisher>();
                    services.AddSingleton<LanguageService>();
                    services.AddSingleton<HoverService>();
                    // Code action providers
                    services.AddSingleton<ICodeActionProvider, DiagnosticQuickFixProvider>();
                    services.AddSingleton<ICodeActionProvider, OrganizeImportsProvider>();
                    services.AddSingleton<ICodeActionProvider, ImplementInterfaceProvider>();
                    services.AddSingleton<ICodeActionProvider, ExtractVariableProvider>();
                    services.AddSingleton<ICodeActionProvider, ExtractMethodProvider>();
                    services.AddSingleton<ICodeActionProvider, ConvertFormsProvider>();
                    services.AddSingleton<ICodeActionProvider, InlineProvider>();
                })
                .OnInitialize((server, request, token) =>
                {
                    var rootUri = request.RootUri;
                    if (rootUri is not null)
                        workspaceRootUri = rootUri.ToUri();

                    // Declare workspace folder support so clients send folder notifications.
                    // ServerSettings.Capabilities may be null during OnInitialize on some
                    // OmniSharp versions — guard each level to avoid NullReferenceException.
                    var caps = server.ServerSettings?.Capabilities;
                    if (caps is not null)
                    {
                        caps.Workspace ??= new OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities.WorkspaceServerCapabilities();
                        caps.Workspace.WorkspaceFolders = new DidChangeWorkspaceFolderRegistrationOptions.StaticOptions
                        {
                            Supported = true,
                            ChangeNotifications = true,
                        };
                    }

                    return Task.CompletedTask;
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
                .WithHandler<SharpyRangeFormattingHandler>()
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
                // Phase 5 handlers
                .WithHandler<SharpySelectionRangeHandler>()
                .WithHandler<SharpyLinkedEditingRangeHandler>()
        ).ConfigureAwait(false);

        await server.WaitForExit.ConfigureAwait(false);
    }
}
