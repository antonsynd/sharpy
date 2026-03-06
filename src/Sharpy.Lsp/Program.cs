using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;

namespace Sharpy.Lsp;

public class Program
{
    public static async Task Main(string[] args)
    {
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
                .WithHandler<TextDocumentSyncHandler>()
                .WithHandler<SharplyHoverHandler>()
                .WithHandler<SharplyDefinitionHandler>()
                .WithHandler<SharplyCompletionHandler>()
                .WithHandler<SharplyReferencesHandler>()
                .WithHandler<SharplyRenameHandler>()
                .WithHandler<SharplyDocumentSymbolHandler>()
                .WithHandler<SharplySignatureHelpHandler>()
        ).ConfigureAwait(false);

        await server.WaitForExit.ConfigureAwait(false);
    }
}
