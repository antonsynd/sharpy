using System.CommandLine;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

namespace Sharpy.Cli;

internal class GlobalOptions
{
    public Option<CompilerLogLevel?> LogLevel { get; }
    public Option<FileInfo?> LogFile { get; }
    public Option<string?> MetricsFormat { get; }
    public Option<FileInfo?> MetricsOutput { get; }
    public Option<bool> WarnAsError { get; }
    public Option<string?> Nowarn { get; }
    public Option<int?> MaxErrors { get; }
    public Option<bool> Verbose { get; }
    public Option<string[]> EnableFeature { get; }

    public GlobalOptions()
    {
        LogLevel = new Option<CompilerLogLevel?>("--log-level") { Description = "Set compiler log level (None, Error, Warning, Info, Debug)", Recursive = true };
        LogFile = new Option<FileInfo?>("--log-file") { Description = "Write compiler logs to the specified file", Recursive = true };
        MetricsFormat = new Option<string?>("--metrics-format") { Description = "Output compilation metrics (text or json)", Recursive = true };
        MetricsOutput = new Option<FileInfo?>("--metrics-output") { Description = "Write metrics to the specified file", Recursive = true };
        WarnAsError = new Option<bool>("--warn-as-error") { Description = "Treat all warnings as errors", Recursive = true };
        Nowarn = new Option<string?>("--nowarn") { Description = "Suppress warnings by code (comma-separated, e.g., SPY0451,SPY0452)", Recursive = true };
        MaxErrors = new Option<int?>("--max-errors") { Description = "Maximum number of errors before stopping (default: 25 for parser, 100 for semantic)", Recursive = true };
        Verbose = new Option<bool>("--verbose") { Description = "Enable verbose output (raises --log-level to Info, printing per-phase timing). An explicit --log-level takes precedence.", Recursive = true };
        Verbose.Aliases.Add("-v");
        EnableFeature = new Option<string[]>("--enable-feature") { Description = "Enable an experimental feature flag (repeatable)", Recursive = true, AllowMultipleArgumentsPerToken = true };
        EnableFeature.Validators.Add(result =>
        {
            var values = result.GetValueOrDefault<string[]>() ?? System.Array.Empty<string>();
            foreach (var name in values)
            {
                if (!FeatureFlags.TryValidate(name, out var error))
                    result.AddError(error!);
            }
        });
    }

    public void AddToCommand(Command command)
    {
        command.Options.Add(LogLevel);
        command.Options.Add(LogFile);
        command.Options.Add(MetricsFormat);
        command.Options.Add(MetricsOutput);
        command.Options.Add(WarnAsError);
        command.Options.Add(Nowarn);
        command.Options.Add(MaxErrors);
        command.Options.Add(Verbose);
        command.Options.Add(EnableFeature);
    }

    /// <summary>
    /// Resolves the effective compiler log level from the parse result. An explicit
    /// <c>--log-level</c> always wins; otherwise <c>--verbose</c>/<c>-v</c> raises the
    /// level to <see cref="CompilerLogLevel.Info"/> (which unlocks the per-phase timing
    /// summary). With neither set, the level defaults to <see cref="CompilerLogLevel.None"/>.
    /// </summary>
    public CompilerLogLevel ResolveLogLevel(ParseResult parseResult)
    {
        var explicitLevel = parseResult.GetValue(LogLevel);
        if (explicitLevel != null)
        {
            return explicitLevel.Value;
        }

        return parseResult.GetValue(Verbose) ? CompilerLogLevel.Info : CompilerLogLevel.None;
    }
}
