using System.CommandLine;
using Sharpy.Compiler.Logging;

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

    public GlobalOptions()
    {
        LogLevel = new Option<CompilerLogLevel?>("--log-level") { Description = "Set compiler log level (None, Error, Warning, Info, Debug)", Recursive = true };
        LogFile = new Option<FileInfo?>("--log-file") { Description = "Write compiler logs to the specified file", Recursive = true };
        MetricsFormat = new Option<string?>("--metrics-format") { Description = "Output compilation metrics (text or json)", Recursive = true };
        MetricsOutput = new Option<FileInfo?>("--metrics-output") { Description = "Write metrics to the specified file", Recursive = true };
        WarnAsError = new Option<bool>("--warn-as-error") { Description = "Treat all warnings as errors", Recursive = true };
        Nowarn = new Option<string?>("--nowarn") { Description = "Suppress warnings by code (comma-separated, e.g., SPY0451,SPY0452)", Recursive = true };
        MaxErrors = new Option<int?>("--max-errors") { Description = "Maximum number of errors before stopping (default: 25 for parser, 100 for semantic)", Recursive = true };
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
    }
}
