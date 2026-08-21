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
            if (ServerCommandLine.IsHelpRequested(args))
            {
                await Console.Out.WriteAsync(ServerCommandLine.UsageText).ConfigureAwait(false);
                return;
            }

            var logging = ServerCommandLine.ResolveLogging(
                args,
                Environment.GetEnvironmentVariable(ServerCommandLine.EnvironmentVariable),
                Console.Error);

            await RunServerAsync(logging).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(
                $"[Fatal] Sharpy LSP server crashed: {ex}").ConfigureAwait(false);
            Environment.ExitCode = 1;
        }
    }

    private static async Task RunServerAsync(ServerLogging logging)
    {
        // Store the workspace root URI from initialization for use in OnInitialized.
        Uri? workspaceRootUri = null;

        // A log destination is attached only when a level was asked for (#1225). OmniSharp registers
        // no logging provider of its own, so an unconfigured server discards its records exactly as
        // it always has — which is what keeps the per-keystroke instrumentation free when nobody is
        // watching (#1140). The factory has to be supplied here: neither ConfigureLogging nor the
        // service collection reaches the ILoggerFactory the container resolves ILogger<T> from.
        var loggerFactory = logging.IsConfigured
            ? LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(logging.Level);
                builder.AddProvider(new StandardErrorLoggerProvider(Console.Error));
            })
            : null;

        var server = await LanguageServer.From(options =>
        {
            if (loggerFactory != null)
                options.WithLoggerFactory(loggerFactory);

            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .WithServerInfo(new ServerInfo
                {
                    Name = "sharpyc",
                    Version = Compiler.VersionInfo.InformationalVersion,
                })
                .ConfigureLogging(builder =>
                {
                    // Not the #1225 knob. OmniSharp's DI-configured logging never reaches the
                    // ILoggerFactory the container resolves ILogger<T> from (see the factory
                    // comment above), so the attached destination and its level are governed
                    // solely by the factory built at the top of this method. This call only keeps
                    // OmniSharp's internal builder aligned with the same level, as a harmless
                    // belt-and-suspenders.
                    builder.SetMinimumLevel(logging.Level);
                })
                .WithServices(services =>
                {
                    var sharpyCoreAssembly = typeof(SharpyRT::Sharpy.Builtins).Assembly;
                    var resolved = DefaultReferenceSet.Resolve(sharpyCoreAssembly.Location);
                    foreach (var warning in resolved.Warnings)
                    {
                        Console.Error.WriteLine($"[Warning] {warning}");
                        if (loggerFactory != null)
                            loggerFactory.CreateLogger(nameof(DefaultReferenceSet)).LogWarning(warning);
                    }
                    services.AddSingleton(new CompilerApi(null, resolved.References.ToArray()));
                    services.AddSingleton<LspConfiguration>();
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
                .WithHandler<SharpyFoldingRangeHandler>()
                .WithHandler<FileWatcherHandler>()
                .WithHandler<SharpyDidChangeConfigurationHandler>()
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
                .WithHandler<SharpyTypeDefinitionHandler>()
                // Phase 5 handlers
                .WithHandler<SharpyRangeFormattingHandler>()
                .WithHandler<SharpyOnTypeFormattingHandler>()
                .WithHandler<SharpySelectionRangeHandler>()
                .WithHandler<SharpyLinkedEditingRangeHandler>()
                .WithHandler<SharpyDocumentLinkHandler>();
        }).ConfigureAwait(false);

        await server.WaitForExit.ConfigureAwait(false);
    }
}
