extern alias SharpyRT;
using System.CommandLine;
using System.Runtime.InteropServices;
using Sharpy.Compiler;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Diagnostics;
using System.Text.Json;
using Sharpy.Compiler.Text;

namespace Sharpy.Cli;

class Program
{
    // Rich diagnostic renderer for CLI output
    private static readonly DiagnosticRenderer _renderer = new(DiagnosticRenderer.IsColorSupported());
    private static readonly bool _useColor = DiagnosticRenderer.IsColorSupported();

    static int Main(string[] args)
    {
        // Handle --version before System.CommandLine to show our custom format
        if (args.Length == 1 && args[0] == "--version")
        {
            Console.WriteLine(VersionInfo.GetDetailedDisplayString());
            return 0;
        }

        var rootCommand = new RootCommand("sharpyc - Sharpy Compiler");

        // === Global Options (Recursive = true so they propagate to all subcommands) ===
        var logLevelOption = new Option<CompilerLogLevel?>("--log-level") { Description = "Set compiler log level (None, Error, Warning, Info, Debug)", Recursive = true };
        var logFileOption = new Option<FileInfo?>("--log-file") { Description = "Write compiler logs to the specified file", Recursive = true };
        var metricsFormatOption = new Option<string?>("--metrics-format") { Description = "Output compilation metrics (text or json)", Recursive = true };
        var metricsOutputOption = new Option<FileInfo?>("--metrics-output") { Description = "Write metrics to the specified file", Recursive = true };
        var warnAsErrorOption = new Option<bool>("--warn-as-error") { Description = "Treat all warnings as errors", Recursive = true };
        var nowarnOption = new Option<string?>("--nowarn") { Description = "Suppress warnings by code (comma-separated, e.g., SPY0451,SPY0452)", Recursive = true };
        var maxErrorsOption = new Option<int?>("--max-errors") { Description = "Maximum number of errors before stopping (default: 25 for parser, 100 for semantic)", Recursive = true };

        rootCommand.Options.Add(logLevelOption);
        rootCommand.Options.Add(logFileOption);
        rootCommand.Options.Add(metricsFormatOption);
        rootCommand.Options.Add(metricsOutputOption);
        rootCommand.Options.Add(warnAsErrorOption);
        rootCommand.Options.Add(nowarnOption);
        rootCommand.Options.Add(maxErrorsOption);

        // === Build Command ===
        var buildCommand = new Command("build", "Compile a Sharpy source file to a binary or library");

        var buildInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file to compile" };
        var buildTypeOpt = new Option<string?>("--type") { Description = "Output type: 'exe' or 'library' (default: exe)" };
        buildTypeOpt.Aliases.Add("-t");
        var buildOutputOpt = new Option<FileInfo?>("--output") { Description = "Output file path" };
        buildOutputOpt.Aliases.Add("-o");
        var buildRefOpt = new Option<string[]>("--reference") { Description = "Add .NET assembly references", AllowMultipleArgumentsPerToken = true };
        buildRefOpt.Aliases.Add("-r");
        var buildProjRefOpt = new Option<string[]>("--project-reference") { Description = "Add .NET project references", AllowMultipleArgumentsPerToken = true };
        buildProjRefOpt.Aliases.Add("-p");
        var buildModPathOpt = new Option<string[]>("--module-path") { Description = "Additional paths to search for modules", AllowMultipleArgumentsPerToken = true };
        buildModPathOpt.Aliases.Add("-m");

        buildCommand.Arguments.Add(buildInputArg);
        buildCommand.Options.Add(buildTypeOpt);
        buildCommand.Options.Add(buildOutputOpt);
        buildCommand.Options.Add(buildRefOpt);
        buildCommand.Options.Add(buildProjRefOpt);
        buildCommand.Options.Add(buildModPathOpt);

        buildCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(buildInputArg)!;
            var type = parseResult.GetValue(buildTypeOpt) ?? "exe";
            var output = parseResult.GetValue(buildOutputOpt);
            var reference = parseResult.GetValue(buildRefOpt) ?? Array.Empty<string>();
            var projectReference = parseResult.GetValue(buildProjRefOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(buildModPathOpt) ?? Array.Empty<string>();
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var metricsFormat = parseResult.GetValue(metricsFormatOption);
            var metricsOutput = parseResult.GetValue(metricsOutputOption);
            var warnAsError = parseResult.GetValue(warnAsErrorOption);
            var nowarn = parseResult.GetValue(nowarnOption);
            var maxErrors = parseResult.GetValue(maxErrorsOption);

            var logger = CreateLogger(logLevel, logFile);
            HandleBuildCommand(input, type, output, reference, projectReference, modulePath, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors);
        });

        // === Run Command ===
        var runCommand = new Command("run", "Compile and execute a Sharpy source file");

        var runInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file to run" };
        var runOutputOpt = new Option<FileInfo?>("--output") { Description = "Output file path (temporary if not specified)" };
        runOutputOpt.Aliases.Add("-o");
        var runRefOpt = new Option<string[]>("--reference") { Description = "Add .NET assembly references", AllowMultipleArgumentsPerToken = true };
        runRefOpt.Aliases.Add("-r");
        var runProjRefOpt = new Option<string[]>("--project-reference") { Description = "Add .NET project references", AllowMultipleArgumentsPerToken = true };
        runProjRefOpt.Aliases.Add("-p");
        var runModPathOpt = new Option<string[]>("--module-path") { Description = "Additional paths to search for modules", AllowMultipleArgumentsPerToken = true };
        runModPathOpt.Aliases.Add("-m");
        var runArgsOpt = new Option<string[]>("--args") { Description = "Arguments to pass to the program", AllowMultipleArgumentsPerToken = true };
        var runSelfContainedOpt = new Option<bool>("--self-contained") { Description = "Publish as a self-contained executable (no .NET runtime required)" };

        runCommand.Arguments.Add(runInputArg);
        runCommand.Options.Add(runOutputOpt);
        runCommand.Options.Add(runRefOpt);
        runCommand.Options.Add(runProjRefOpt);
        runCommand.Options.Add(runModPathOpt);
        runCommand.Options.Add(runArgsOpt);
        runCommand.Options.Add(runSelfContainedOpt);

        runCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(runInputArg)!;
            var output = parseResult.GetValue(runOutputOpt);
            var reference = parseResult.GetValue(runRefOpt) ?? Array.Empty<string>();
            var projectReference = parseResult.GetValue(runProjRefOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(runModPathOpt) ?? Array.Empty<string>();
            var progArgs = parseResult.GetValue(runArgsOpt) ?? Array.Empty<string>();
            var selfContained = parseResult.GetValue(runSelfContainedOpt);
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var metricsFormat = parseResult.GetValue(metricsFormatOption);
            var metricsOutput = parseResult.GetValue(metricsOutputOption);
            var warnAsError = parseResult.GetValue(warnAsErrorOption);
            var nowarn = parseResult.GetValue(nowarnOption);
            var maxErrors = parseResult.GetValue(maxErrorsOption);

            var logger = CreateLogger(logLevel, logFile);
            HandleRunCommand(input, output, reference, projectReference, modulePath, progArgs, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors, selfContained);
        });

        // === Project Command ===
        var projectCommand = new Command("project", "Build a Sharpy project from a .spyproj file");

        var projFileArg = new Argument<FileInfo?>("project") { Description = "Path to .spyproj file (auto-discovers if not specified)", Arity = ArgumentArity.ZeroOrOne };
        var projConfigOpt = new Option<string?>("--configuration") { Description = "Build configuration (Debug or Release)" };
        projConfigOpt.Aliases.Add("-c");
        var projCleanOpt = new Option<bool>("--clean") { Description = "Delete bin/ and obj/ directories before building" };
        var projEmitCsOpt = new Option<DirectoryInfo?>("--emit-cs-to") { Description = "Save generated C# code to the specified directory" };
        var projIncrementalOpt = new Option<bool>("--incremental") { Description = "Enable incremental compilation (only recompile changed files)" };

        projectCommand.Arguments.Add(projFileArg);
        projectCommand.Options.Add(projConfigOpt);
        projectCommand.Options.Add(projCleanOpt);
        projectCommand.Options.Add(projEmitCsOpt);
        projectCommand.Options.Add(projIncrementalOpt);

        projectCommand.SetAction((parseResult) =>
        {
            var project = parseResult.GetValue(projFileArg);
            var configuration = parseResult.GetValue(projConfigOpt) ?? "Debug";
            var clean = parseResult.GetValue(projCleanOpt);
            var emitCsTo = parseResult.GetValue(projEmitCsOpt);
            var incremental = parseResult.GetValue(projIncrementalOpt);
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var metricsFormat = parseResult.GetValue(metricsFormatOption);
            var metricsOutput = parseResult.GetValue(metricsOutputOption);
            var warnAsError = parseResult.GetValue(warnAsErrorOption);
            var nowarn = parseResult.GetValue(nowarnOption);
            var maxErrors = parseResult.GetValue(maxErrorsOption);

            var logger = CreateLogger(logLevel, logFile);
            HandleProjectCommand(project, configuration, clean, incremental, emitCsTo, logger, logLevel, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors);
        });

        // === Emit Command (with subcommands) ===
        // Note: emit tokens/ast/parse do NOT support --warn-as-error/--nowarn (no semantic analysis),
        // but DO support --max-errors (lexer/parser have MaxErrors). emit csharp supports all three.
        var emitCommand = new Command("emit", "Emit compiler intermediate representations");

        var emitTokensCommand = new Command("tokens", "Emit tokenized output");
        var emitTokensInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        emitTokensCommand.Arguments.Add(emitTokensInputArg);
        emitTokensCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(emitTokensInputArg)!;
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var maxErrors = parseResult.GetValue(maxErrorsOption);
            var logger = CreateLogger(logLevel, logFile);
            EmitTokens(input, logger, maxErrors);
        });

        var emitAstCommand = new Command("ast", "Emit abstract syntax tree");
        var emitAstInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        emitAstCommand.Arguments.Add(emitAstInputArg);
        emitAstCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(emitAstInputArg)!;
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var maxErrors = parseResult.GetValue(maxErrorsOption);
            var logger = CreateLogger(logLevel, logFile);
            EmitAst(input, logger, maxErrors);
        });

        var emitCsharpCommand = new Command("csharp", "Emit generated C# code");
        var emitCsharpInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        var emitCsharpOutputOpt = new Option<FileInfo?>("--output") { Description = "Output file path" };
        emitCsharpOutputOpt.Aliases.Add("-o");
        var emitCsharpRefOpt = new Option<string[]>("--reference") { Description = "Add .NET assembly references", AllowMultipleArgumentsPerToken = true };
        emitCsharpRefOpt.Aliases.Add("-r");
        var emitCsharpModPathOpt = new Option<string[]>("--module-path") { Description = "Additional paths to search for modules", AllowMultipleArgumentsPerToken = true };
        emitCsharpModPathOpt.Aliases.Add("-m");
        var emitCsharpLineDirectivesOpt = new Option<bool>("--show-line-directives") { Description = "Include #line directives for source mapping (default: stripped for clean output)" };
        var emitCsharpTypeOpt = new Option<string?>("--type") { Description = "Output type: 'exe' or 'library' (default: exe)" };
        emitCsharpTypeOpt.Aliases.Add("-t");
        emitCsharpCommand.Arguments.Add(emitCsharpInputArg);
        emitCsharpCommand.Options.Add(emitCsharpOutputOpt);
        emitCsharpCommand.Options.Add(emitCsharpRefOpt);
        emitCsharpCommand.Options.Add(emitCsharpModPathOpt);
        emitCsharpCommand.Options.Add(emitCsharpLineDirectivesOpt);
        emitCsharpCommand.Options.Add(emitCsharpTypeOpt);
        emitCsharpCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(emitCsharpInputArg)!;
            var output = parseResult.GetValue(emitCsharpOutputOpt);
            var reference = parseResult.GetValue(emitCsharpRefOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(emitCsharpModPathOpt) ?? Array.Empty<string>();
            var showLineDirectives = parseResult.GetValue(emitCsharpLineDirectivesOpt);
            var emitType = parseResult.GetValue(emitCsharpTypeOpt) ?? "exe";
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var warnAsError = parseResult.GetValue(warnAsErrorOption);
            var nowarn = parseResult.GetValue(nowarnOption);
            var maxErrors = parseResult.GetValue(maxErrorsOption);
            var logger = CreateLogger(logLevel, logFile);
            EmitCSharp(input, output, reference, modulePath, logger, warnAsError, nowarn, maxErrors, showLineDirectives, emitType);
        });

        var emitParseCommand = new Command("parse", "Validate lexing and parsing only");
        var emitParseInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        emitParseCommand.Arguments.Add(emitParseInputArg);
        emitParseCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(emitParseInputArg)!;
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var maxErrors = parseResult.GetValue(maxErrorsOption);
            var logger = CreateLogger(logLevel, logFile);
            EmitParse(input, logger, maxErrors);
        });

        var emitDiagnosticsCommand = new Command("diagnostics", "Emit compiler diagnostics");
        var emitDiagnosticsInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        var emitDiagnosticsFormatOpt = new Option<string?>("--format") { Description = "Output format: text or json" };
        emitDiagnosticsFormatOpt.Aliases.Add("-f");
        var emitDiagnosticsIncludeCodegenOpt = new Option<bool>("--include-codegen") { Description = "Include code generation phase (uses Compile instead of Analyze)" };
        emitDiagnosticsCommand.Arguments.Add(emitDiagnosticsInputArg);
        emitDiagnosticsCommand.Options.Add(emitDiagnosticsFormatOpt);
        emitDiagnosticsCommand.Options.Add(emitDiagnosticsIncludeCodegenOpt);
        emitDiagnosticsCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(emitDiagnosticsInputArg)!;
            var format = parseResult.GetValue(emitDiagnosticsFormatOpt) ?? "text";
            if (format != "text" && format != "json")
            {
                Console.Error.WriteLine($"Error: --format must be 'text' or 'json', got '{format}'");
                Environment.Exit(1);
                return;
            }
            var includeCodegen = parseResult.GetValue(emitDiagnosticsIncludeCodegenOpt);
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var warnAsError = parseResult.GetValue(warnAsErrorOption);
            var nowarn = parseResult.GetValue(nowarnOption);
            var maxErrors = parseResult.GetValue(maxErrorsOption);
            var logger = CreateLogger(logLevel, logFile);
            EmitDiagnostics(input, logger, format, warnAsError, nowarn, maxErrors, includeCodegen);
        });

        emitCommand.Subcommands.Add(emitTokensCommand);
        emitCommand.Subcommands.Add(emitAstCommand);
        emitCommand.Subcommands.Add(emitCsharpCommand);
        emitCommand.Subcommands.Add(emitParseCommand);
        emitCommand.Subcommands.Add(emitDiagnosticsCommand);

        // === Cache Command (with subcommands) ===
        var cacheCommand = new Command("cache", "Manage the overload discovery cache");

        var cacheClearCommand = new Command("clear", "Clear the cache");
        var cacheClearDirOpt = new Option<string?>("--cache-dir") { Description = "Custom cache directory" };
        cacheClearCommand.Options.Add(cacheClearDirOpt);
        cacheClearCommand.SetAction((parseResult) =>
        {
            var cacheDir = parseResult.GetValue(cacheClearDirOpt);
            ClearCache(cacheDir);
        });

        var cacheInfoCommand = new Command("info", "Display cache information");
        var cacheInfoDirOpt = new Option<string?>("--cache-dir") { Description = "Custom cache directory" };
        cacheInfoCommand.Options.Add(cacheInfoDirOpt);
        cacheInfoCommand.SetAction((parseResult) =>
        {
            var cacheDir = parseResult.GetValue(cacheInfoDirOpt);
            ShowCacheInfo(cacheDir);
        });

        cacheCommand.Subcommands.Add(cacheClearCommand);
        cacheCommand.Subcommands.Add(cacheInfoCommand);

        // === Explain Command ===
        var explainCommand = new Command("explain", "Show detailed explanation for a diagnostic code");

        var explainCodeArg = new Argument<string?>("code") { Description = "Diagnostic code to explain (e.g. SPY0200)", Arity = ArgumentArity.ZeroOrOne };
        var explainListOpt = new Option<bool>("--list") { Description = "List all documented diagnostic codes" };

        explainCommand.Arguments.Add(explainCodeArg);
        explainCommand.Options.Add(explainListOpt);

        explainCommand.SetAction((parseResult) =>
        {
            var code = parseResult.GetValue(explainCodeArg);
            var list = parseResult.GetValue(explainListOpt);
            HandleExplainCommand(code, list);
        });

        // === LSP Command ===
        var lspCommand = new Command("lsp", "Start the Sharpy Language Server (stdio transport)");
        // Accept --stdio flag for compatibility with LSP clients that pass it (e.g., vscode-languageclient)
        var lspStdioOpt = new Option<bool>("--stdio") { Description = "Use stdio transport (default, accepted for compatibility)" };
        lspCommand.Options.Add(lspStdioOpt);
        lspCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            await Sharpy.Lsp.Program.Main(Array.Empty<string>()).ConfigureAwait(false);
        });

        // === Add all commands to root ===
        rootCommand.Subcommands.Add(buildCommand);
        rootCommand.Subcommands.Add(runCommand);
        rootCommand.Subcommands.Add(projectCommand);
        rootCommand.Subcommands.Add(emitCommand);
        rootCommand.Subcommands.Add(cacheCommand);
        rootCommand.Subcommands.Add(explainCommand);
        rootCommand.Subcommands.Add(lspCommand);

        return rootCommand.Parse(args).Invoke();
    }

    static ICompilerLogger CreateLogger(CompilerLogLevel logLevel, FileInfo? logFile)
    {
        if (logLevel == CompilerLogLevel.None)
        {
            return NullLogger.Instance;
        }
        else if (logFile != null)
        {
            var stream = new StreamWriter(logFile.FullName, append: false);
            return new ConsoleCompilerLogger(logLevel, stream, stream);
        }
        else
        {
            return new ConsoleCompilerLogger(logLevel);
        }
    }

    /// <summary>
    /// Writes a compilation timing summary to stderr when the logger is at Info level or higher.
    /// At Debug level, also includes per-validator timing breakdown.
    /// This is separate from --metrics-format, which writes to stdout or a file.
    /// </summary>
    static void OutputVerboseTimingSummary(CompilationMetrics? metrics, ICompilerLogger logger)
    {
        if (metrics == null || !logger.IsEnabled(CompilerLogLevel.Info))
            return;

        Console.Error.WriteLine();
        Console.Error.WriteLine("--- Compilation Timing ---");

        foreach (var phase in metrics.Phases)
        {
            Console.Error.WriteLine($"  {phase.Name,-30} {phase.Duration.TotalMilliseconds,8:F2} ms");
        }

        Console.Error.WriteLine($"  {"TOTAL",-30} {metrics.TotalDuration.TotalMilliseconds,8:F2} ms");

        // Artifact counts
        if (metrics.TokenCount > 0 || metrics.AstNodeCount > 0 || metrics.SymbolCount > 0)
        {
            Console.Error.WriteLine();
            if (metrics.TokenCount > 0)
                Console.Error.WriteLine($"  Tokens: {metrics.TokenCount:N0}");
            if (metrics.AstNodeCount > 0)
                Console.Error.WriteLine($"  AST Nodes: {metrics.AstNodeCount:N0}");
            if (metrics.SymbolCount > 0)
                Console.Error.WriteLine($"  Symbols: {metrics.SymbolCount:N0}");
        }

        // Per-validator timing at Debug level
        if (logger.IsEnabled(CompilerLogLevel.Debug) && metrics.ValidatorTimes.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Validator Breakdown:");

            foreach (var (validator, duration) in metrics.ValidatorTimes.OrderByDescending(kvp => kvp.Value))
            {
                Console.Error.WriteLine($"    {validator,-38} {duration.TotalMilliseconds,8:F2} ms");
            }
        }

        Console.Error.WriteLine();
    }

    /// <summary>
    /// Writes a project compilation timing summary to stderr when the logger is at Info level or higher.
    /// </summary>
    static void OutputVerboseTimingSummary(ProjectCompilationMetrics? metrics, ICompilerLogger logger)
    {
        if (metrics == null || !logger.IsEnabled(CompilerLogLevel.Info))
            return;

        Console.Error.WriteLine();
        Console.Error.WriteLine("--- Project Compilation Timing ---");
        Console.Error.WriteLine($"  Files compiled: {metrics.TotalFiles}");

        if (metrics.SkippedFileCount > 0)
        {
            Console.Error.WriteLine($"  Files skipped (incremental): {metrics.SkippedFileCount}");
        }

        var aggregates = metrics.AggregatePhaseMetrics;
        foreach (var (phase, data) in aggregates.OrderBy(kvp => kvp.Key))
        {
            Console.Error.WriteLine($"  {phase,-30} {data.Duration.TotalMilliseconds,8:F2} ms");
        }

        Console.Error.WriteLine($"  {"TOTAL",-30} {metrics.TotalDuration.TotalMilliseconds,8:F2} ms");

        // Per-validator timing at Debug level (aggregate across all files)
        if (logger.IsEnabled(CompilerLogLevel.Debug) && metrics.FileMetrics.Count > 0)
        {
            var aggregatedValidatorTimes = new Dictionary<string, TimeSpan>();
            foreach (var fileMetric in metrics.FileMetrics)
            {
                foreach (var (validator, duration) in fileMetric.ValidatorTimes)
                {
                    if (!aggregatedValidatorTimes.ContainsKey(validator))
                        aggregatedValidatorTimes[validator] = TimeSpan.Zero;
                    aggregatedValidatorTimes[validator] += duration;
                }
            }

            if (aggregatedValidatorTimes.Count > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("  Validator Breakdown (aggregate):");

                foreach (var (validator, duration) in aggregatedValidatorTimes.OrderByDescending(kvp => kvp.Value))
                {
                    Console.Error.WriteLine($"    {validator,-38} {duration.TotalMilliseconds,8:F2} ms");
                }
            }
        }

        Console.Error.WriteLine();
    }

    static void OutputMetrics(CompilationMetrics? metrics, string? metricsFormat, FileInfo? metricsOutput)
    {
        if (metrics == null || metricsFormat == null)
            return;

        var format = metricsFormat.ToLowerInvariant();
        if (format != "text" && format != "json")
        {
            Console.Error.WriteLine($"Invalid metrics format: {metricsFormat}. Use 'text' or 'json'.");
            return;
        }

        var output = format == "json" ? metrics.FormatAsJson() : metrics.FormatAsText();

        if (metricsOutput != null)
        {
            try
            {
                File.WriteAllText(metricsOutput.FullName, output);
                Console.WriteLine($"Metrics written to: {metricsOutput.FullName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write metrics to file: {ex.Message}");
                Console.WriteLine(output);
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(output);
        }
    }

    static void OutputProjectMetrics(ProjectCompilationMetrics? metrics, string? metricsFormat, FileInfo? metricsOutput)
    {
        if (metrics == null || metricsFormat == null)
            return;

        var format = metricsFormat.ToLowerInvariant();
        if (format != "text" && format != "json")
        {
            Console.Error.WriteLine($"Invalid metrics format: {metricsFormat}. Use 'text' or 'json'.");
            return;
        }

        var output = format == "json" ? metrics.FormatAsJson() : metrics.FormatAsText();

        if (metricsOutput != null)
        {
            try
            {
                File.WriteAllText(metricsOutput.FullName, output);
                Console.WriteLine($"Metrics written to: {metricsOutput.FullName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write metrics to file: {ex.Message}");
                Console.WriteLine(output);
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(output);
        }
    }

    static void HandleBuildCommand(
        FileInfo inputFile,
        string outputType,
        FileInfo? output,
        string[] references,
        string[] projectReferences,
        string[] modulePaths,
        ICompilerLogger logger,
        string? metricsFormat,
        FileInfo? metricsOutput,
        bool warnAsError = false,
        string? nowarn = null,
        int? maxErrors = null)
    {
        ValidateInputFile(inputFile);
        CompileToBinary(inputFile, outputType, output, references, projectReferences, modulePaths, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors);
    }

    static void HandleRunCommand(
        FileInfo inputFile,
        FileInfo? output,
        string[] references,
        string[] projectReferences,
        string[] modulePaths,
        string[] args,
        ICompilerLogger logger,
        string? metricsFormat,
        FileInfo? metricsOutput,
        bool warnAsError = false,
        string? nowarn = null,
        int? maxErrors = null,
        bool selfContained = false)
    {
        ValidateInputFile(inputFile);

        // Determine output path - use temp file if not specified
        var outputPath = output?.FullName;
        var isTempOutput = false;
        var tempBaseName = "";

        if (outputPath == null)
        {
            var tempDir = Path.GetTempPath();
            var inputFileName = Path.GetFileNameWithoutExtension(inputFile.Name);
            tempBaseName = $"{inputFileName}_{Guid.NewGuid():N}";
            outputPath = Path.Combine(tempDir, tempBaseName + ".exe");
            isTempOutput = true;
        }

        try
        {
            // Compile to executable
            CompileToBinary(inputFile, "exe", new FileInfo(outputPath), references, projectReferences, modulePaths, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors);

            // Copy Sharpy.Core.dll to the output directory so the executable can find it
            var sharpyCoreAssembly = typeof(SharpyRT::Sharpy.Builtins).Assembly;
            var sharpyCorePath = sharpyCoreAssembly.Location;
            var outputDir = Path.GetDirectoryName(outputPath)!;
            var sharpyCoreDestPath = Path.Combine(outputDir, "Sharpy.Core.dll");
            File.Copy(sharpyCorePath, sharpyCoreDestPath, overwrite: true);

            if (selfContained)
            {
                HandleSelfContainedRun(inputFile, outputPath, sharpyCorePath, args, isTempOutput);
                return;
            }

            // Run the compiled executable
            Console.WriteLine();
            Console.WriteLine("=== Running Program ===");
            Console.WriteLine();

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { outputPath },
                UseShellExecute = false
            };

            // Add program arguments
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit();

                // Clean up temp files after execution if needed
                if (isTempOutput)
                {
                    try
                    {
                        var basePath = Path.GetDirectoryName(outputPath)!;
                        File.Delete(outputPath);
                        File.Delete(Path.Combine(basePath, tempBaseName + ".runtimeconfig.json"));
                        File.Delete(Path.Combine(basePath, tempBaseName + ".deps.json"));
                        File.Delete(Path.Combine(basePath, tempBaseName + ".pdb"));
                        File.Delete(sharpyCoreDestPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }

                Environment.Exit(process.ExitCode);
            }
        }
        catch (Exception)
        {
            // Clean up temp files on error if needed
            if (isTempOutput && File.Exists(outputPath))
            {
                try
                {
                    var basePath = Path.GetDirectoryName(outputPath)!;
                    File.Delete(outputPath);
                    File.Delete(Path.Combine(basePath, tempBaseName + ".runtimeconfig.json"));
                    File.Delete(Path.Combine(basePath, tempBaseName + ".deps.json"));
                    File.Delete(Path.Combine(basePath, tempBaseName + ".pdb"));

                    // Also clean up Sharpy.Core.dll
                    var sharpyCoreDestPath = Path.Combine(basePath, "Sharpy.Core.dll");
                    if (File.Exists(sharpyCoreDestPath))
                    {
                        File.Delete(sharpyCoreDestPath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            throw;
        }
    }

    static void HandleSelfContainedRun(
        FileInfo inputFile,
        string compiledExePath,
        string sharpyCorePath,
        string[] args,
        bool isTempOutput)
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var assemblyName = Path.GetFileNameWithoutExtension(inputFile.Name);
        var publishDir = Path.Combine(Path.GetTempPath(), $"sharpy_publish_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(publishDir);

            // Create a temporary .csproj that references the compiled assembly and Sharpy.Core
            var tempProjDir = Path.Combine(Path.GetTempPath(), $"sharpy_proj_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempProjDir);
            var csprojPath = Path.Combine(tempProjDir, $"{assemblyName}.csproj");

            var compiledDir = Path.GetDirectoryName(compiledExePath)!;
            var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>{assemblyName}</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""{Path.GetFileNameWithoutExtension(compiledExePath)}"">
      <HintPath>{compiledExePath}</HintPath>
    </Reference>
    <Reference Include=""Sharpy.Core"">
      <HintPath>{sharpyCorePath}</HintPath>
    </Reference>
  </ItemGroup>
</Project>";

            // Write a minimal Program.cs that calls the compiled entry point
            File.WriteAllText(csprojPath, csprojContent);
            File.WriteAllText(
                Path.Combine(tempProjDir, "Program.cs"),
                $"// Auto-generated entry point\n{assemblyName}.Main();\n");

            // Run dotnet publish --self-contained
            Console.WriteLine($"Publishing self-contained executable for {rid}...");
            var publishInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList =
                {
                    "publish",
                    csprojPath,
                    "--self-contained",
                    "-r", rid,
                    "-o", publishDir,
                    "--nologo",
                    "-v", "q"
                },
                UseShellExecute = false,
                RedirectStandardError = true
            };

            var publishProcess = System.Diagnostics.Process.Start(publishInfo);
            if (publishProcess != null)
            {
                var stderr = publishProcess.StandardError.ReadToEnd();
                publishProcess.WaitForExit();

                if (publishProcess.ExitCode != 0)
                {
                    Console.Error.WriteLine("Self-contained publish failed:");
                    Console.Error.WriteLine(stderr);
                    Environment.Exit(1);
                }
            }

            // Find and run the published executable
            var publishedExe = Path.Combine(publishDir, assemblyName);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                publishedExe += ".exe";

            if (!File.Exists(publishedExe))
            {
                Console.Error.WriteLine($"Published executable not found: {publishedExe}");
                Environment.Exit(1);
            }

            Console.WriteLine($"Published to: {publishDir}");
            Console.WriteLine();
            Console.WriteLine("=== Running Program ===");
            Console.WriteLine();

            var runInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = publishedExe,
                UseShellExecute = false
            };

            foreach (var arg in args)
                runInfo.ArgumentList.Add(arg);

            var runProcess = System.Diagnostics.Process.Start(runInfo);
            if (runProcess != null)
            {
                runProcess.WaitForExit();
                Environment.Exit(runProcess.ExitCode);
            }

            // Clean up temp project directory
            try
            { Directory.Delete(tempProjDir, recursive: true); }
            catch { }
        }
        finally
        {
            // Clean up if using temp output
            if (isTempOutput)
            {
                try
                {
                    var basePath = Path.GetDirectoryName(compiledExePath)!;
                    var tempBaseName = Path.GetFileNameWithoutExtension(compiledExePath);
                    File.Delete(compiledExePath);
                    File.Delete(Path.Combine(basePath, tempBaseName + ".runtimeconfig.json"));
                    File.Delete(Path.Combine(basePath, tempBaseName + ".deps.json"));
                    File.Delete(Path.Combine(basePath, tempBaseName + ".pdb"));
                    File.Delete(Path.Combine(basePath, "Sharpy.Core.dll"));
                }
                catch { }
            }

            // Note: publishDir is intentionally NOT cleaned up since the user likely
            // wants the self-contained executable. If we wanted auto-cleanup, the user
            // should specify --output.
        }
    }

    static void HandleProjectCommand(
        FileInfo? projectFile,
        string configuration,
        bool clean,
        bool incremental,
        DirectoryInfo? emitCsTo,
        ICompilerLogger logger,
        CompilerLogLevel logLevel,
        string? metricsFormat,
        FileInfo? metricsOutput,
        bool warnAsError = false,
        string? nowarn = null,
        int? maxErrors = null)
    {
        FileInfo? resolvedProjectFile = projectFile;

        if (resolvedProjectFile == null)
        {
            // Auto-discover .spyproj file in current directory
            var currentDir = Directory.GetCurrentDirectory();
            var discoveredPath = ProjectFileParser.FindProjectFile(currentDir);

            if (discoveredPath == null)
            {
                Console.Error.WriteLine("Error: No .spyproj file found in current directory.");
                Console.Error.WriteLine("Specify a project file with the first argument, or use 'sharpyc build' for single-file compilation.");
                Environment.Exit(1);
                return;
            }

            resolvedProjectFile = new FileInfo(discoveredPath);
            Console.WriteLine($"Building project: {Path.GetFileName(discoveredPath)}");
        }

        CompileProject(resolvedProjectFile, configuration, clean, incremental, emitCsTo, logger, logLevel, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors);
    }

    static void ValidateInputFile(FileInfo inputFile)
    {
        if (!inputFile.Exists)
        {
            Console.Error.WriteLine($"Error: Input file '{inputFile.FullName}' does not exist.");
            Environment.Exit(1);
        }

        if (inputFile.Extension != ".spy")
        {
            Console.Error.WriteLine($"Warning: Input file '{inputFile.Name}' does not have .spy extension.");
        }
    }

    static void EmitTokens(FileInfo inputFile, ICompilerLogger logger, int? maxErrors = null)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var sourceText = new SourceText(source, inputFile.FullName);
            var lexer = new Lexer(sourceText, logger);
            if (maxErrors is > 0)
            {
                lexer.MaxErrors = maxErrors.Value;
            }
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
            {
                RenderDiagnostics(lexer.Diagnostics.GetErrors(), sourceText, Console.Error);
                Environment.Exit(1);
            }

            Console.WriteLine($"Tokens for {inputFile.Name}:");
            Console.WriteLine(new string('=', 80));

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                var value = string.IsNullOrEmpty(token.Value) ? "" : $" = '{token.Value}'";
                Console.WriteLine($"{i,4}: {token.Type,-20} @ L{token.Line}:C{token.Column}{value}");
            }

            Console.WriteLine(new string('=', 80));
            Console.WriteLine($"Total tokens: {tokens.Count}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void EmitAst(FileInfo inputFile, ICompilerLogger logger, int? maxErrors = null)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var sourceText = new SourceText(source, inputFile.FullName);
            var lexer = new Lexer(sourceText, logger);
            if (maxErrors is > 0)
            {
                lexer.MaxErrors = maxErrors.Value;
            }
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
            {
                RenderDiagnostics(lexer.Diagnostics.GetErrors(), sourceText, Console.Error);
                Environment.Exit(1);
            }

            var parserMaxErrors = maxErrors is > 0 ? maxErrors.Value : 25;
            var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger, parserMaxErrors);
            var module = parser.ParseModule();

            if (parser.Diagnostics.HasErrors)
            {
                RenderDiagnostics(parser.Diagnostics.GetErrors(), sourceText, Console.Error);
                Environment.Exit(1);
            }

            Console.WriteLine($"AST for {inputFile.Name}:");
            Console.WriteLine(new string('=', 80));

            var dumper = new AstDumper();
            var ast = dumper.Dump(module);
            Console.Write(ast);

            Console.WriteLine(new string('=', 80));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void EmitParse(FileInfo inputFile, ICompilerLogger logger, int? maxErrors = null)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var sourceText = new SourceText(source, inputFile.FullName);
            var lexer = new Lexer(sourceText, logger);
            if (maxErrors is > 0)
            {
                lexer.MaxErrors = maxErrors.Value;
            }
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
            {
                RenderDiagnostics(lexer.Diagnostics.GetErrors(), sourceText, Console.Error);
                Environment.Exit(1);
            }

            var parserMaxErrors = maxErrors is > 0 ? maxErrors.Value : 25;
            var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger, parserMaxErrors);
            parser.ParseModule();

            if (parser.Diagnostics.HasErrors)
            {
                RenderDiagnostics(parser.Diagnostics.GetErrors(), sourceText, Console.Error);
                Environment.Exit(1);
            }

            Console.WriteLine("PARSE_OK");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void EmitDiagnostics(FileInfo inputFile, ICompilerLogger logger, string format,
        bool warnAsError = false, string? nowarn = null, int? maxErrors = null, bool includeCodegen = false)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var api = new CompilerApi(logger);

            IReadOnlyList<CompilerDiagnostic> diagnostics;

            if (includeCodegen)
            {
                var compilerOptions = new CompilerOptions
                {
                    WarningsAsErrors = warnAsError,
                    SuppressedWarnings = ParseNowarnCodes(nowarn),
                    MaxErrors = maxErrors ?? 0
                };
                var result = api.Compile(source, compilerOptions, inputFile.FullName);
                diagnostics = result.Diagnostics;
            }
            else
            {
                var result = api.Analyze(source);
                diagnostics = result.Diagnostics;
            }

            var hasErrors = diagnostics.Any(d => d.IsError);

            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var jsonItems = diagnostics.Select(d => new
                {
                    severity = d.Severity.ToString().ToLowerInvariant(),
                    code = d.Code ?? "SPY????",
                    line = d.Line ?? 0,
                    column = d.Column ?? 0,
                    message = d.Message,
                    phase = d.Phase.ToString()
                });

                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                Console.WriteLine(JsonSerializer.Serialize(jsonItems, jsonOptions));
            }
            else
            {
                if (diagnostics.Count == 0)
                {
                    Console.WriteLine("No diagnostics.");
                }
                else
                {
                    foreach (var d in diagnostics)
                    {
                        var severity = d.Severity.ToString().ToLowerInvariant();
                        var code = d.Code ?? "SPY????";
                        var line = d.Line ?? 0;
                        var col = d.Column ?? 0;
                        Console.WriteLine($"{severity} {code} ({line}:{col}): {d.Message}");
                    }
                }
            }

            if (hasErrors)
            {
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void EmitCSharp(FileInfo inputFile, FileInfo? output, string[] references, string[] modulePaths,
        ICompilerLogger logger, bool warnAsError = false, string? nowarn = null, int? maxErrors = null,
        bool showLineDirectives = false, string outputType = "exe")
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var sourceText = new SourceText(source, inputFile.FullName);

            // Compile via CompilerApi
            var compilerOptions = new CompilerOptions
            {
                OutputType = outputType,
                References = references,
                ModulePaths = modulePaths,
                WarningsAsErrors = warnAsError,
                SuppressedWarnings = ParseNowarnCodes(nowarn),
                MaxErrors = maxErrors ?? 0
            };
            var api = new CompilerApi(logger);
            var result = api.Compile(source, compilerOptions, inputFile.FullName);

            if (!result.Success)
            {
                Console.Error.WriteLine("Compilation errors:");
                Console.Error.WriteLine();
                RenderDiagnostics(result.Diagnostics.Where(d => d.IsError), sourceText, Console.Error);
                Environment.Exit(1);
            }

            // Display warnings
            var warnings = result.Diagnostics.Where(d => d.IsWarning).ToList();
            if (warnings.Count > 0)
            {
                RenderDiagnostics(warnings, sourceText, Console.Out);
            }

            // The generated C# includes #line directives by default for source mapping.
            // For emit csharp, strip them for clean output unless --show-line-directives is specified.
            var csharpCode = result.GeneratedCSharp ?? "";
            if (!showLineDirectives)
            {
                csharpCode = StripLineDirectives(csharpCode);
            }

            // Determine output file
            FileInfo outputFile;
            if (output != null)
            {
                outputFile = output;
            }
            else
            {
                // Default: replace .spy extension with .cs
                var outputPath = Path.ChangeExtension(inputFile.FullName, ".cs");
                outputFile = new FileInfo(outputPath);
            }

            // Output verbose timing summary to stderr when --log-level is Info or higher
            OutputVerboseTimingSummary(result.Metrics, logger);

            // Write C# code to output file
            File.WriteAllText(outputFile.FullName, csharpCode);
            Console.WriteLine($"Generated C# code written to: {outputFile.FullName}");

            // Write imported modules' generated C# alongside the entry file
            var outputDir = outputFile.DirectoryName ?? ".";
            foreach (var (modulePath, moduleCode) in result.GeneratedCSharpFiles)
            {
                // Skip the entry file (already written above)
                if (string.Equals(Path.GetFullPath(modulePath), Path.GetFullPath(inputFile.FullName),
                    StringComparison.OrdinalIgnoreCase))
                    continue;

                var moduleFileName = Path.GetFileNameWithoutExtension(modulePath) + ".cs";
                var moduleOutputPath = Path.Combine(outputDir, moduleFileName);
                var processedModuleCode = showLineDirectives ? moduleCode : StripLineDirectives(moduleCode);
                File.WriteAllText(moduleOutputPath, processedModuleCode);
                Console.WriteLine($"Generated C# code written to: {moduleOutputPath}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Strip #line directives from generated C# for clean emit output.
    /// </summary>
    static string StripLineDirectives(string csharpCode)
    {
        var lines = csharpCode.Split('\n');
        var filtered = lines.Where(line => !line.TrimStart().StartsWith("#line "));
        return string.Join('\n', filtered);
    }

    static void ClearCache(string? cacheDir)
    {
        try
        {
            var cache = new OverloadIndexCache(cacheDir);
            cache.ClearAll();
            Console.WriteLine("Overload discovery cache cleared successfully.");
            if (cacheDir != null)
            {
                Console.WriteLine($"Cache directory: {cacheDir}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error clearing cache: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void CompileProject(FileInfo projectFile, string configuration, bool clean, bool incremental, DirectoryInfo? emitCsTo, ICompilerLogger logger, CompilerLogLevel logLevel = CompilerLogLevel.None, string? metricsFormat = null, FileInfo? metricsOutput = null, bool warnAsError = false, string? nowarn = null, int? maxErrors = null)
    {
        try
        {
            // Load project configuration
            var projectConfig = ProjectFileParser.Load(projectFile.FullName, configuration);

            // Handle clean if requested
            if (clean)
            {
                CleanProject(projectConfig);
            }

            Console.WriteLine($"Project: {projectConfig.RootNamespace}");
            Console.WriteLine($"Configuration: {projectConfig.Configuration}");
            Console.WriteLine($"Output: {projectConfig.OutputType}");
            Console.WriteLine($"Source files: {projectConfig.SourceFiles.Count}");
            if (incremental)
            {
                Console.WriteLine("Mode: Incremental");
            }
            Console.WriteLine();

            // Create compiler with options (CLI flags override project file settings)
            var mergedSuppressed = new HashSet<string>(projectConfig.SuppressedWarnings);
            mergedSuppressed.UnionWith(ParseNowarnCodes(nowarn));

            var compilerOptions = new CompilerOptions
            {
                References = projectConfig.References.ToArray(),
                ModulePaths = projectConfig.ModulePaths.ToArray(),
                WarningsAsErrors = warnAsError || projectConfig.WarningsAsErrors,
                SuppressedWarnings = mergedSuppressed,
                MaxErrors = maxErrors ?? 0,
                Incremental = incremental
            };

            var compiler = new Sharpy.Compiler.Compiler(compilerOptions, logger);

            // Compile the project
            var result = compiler.CompileProject(projectConfig);

            // Save generated C# code if requested
            if (emitCsTo != null && result.GeneratedCSharpFiles.Any())
            {
                SaveGeneratedCSharp(emitCsTo, result.GeneratedCSharpFiles);
            }

            // Display warnings
            var projectWarnings = result.Diagnostics.GetWarnings();
            if (projectWarnings.Count > 0)
            {
                RenderDiagnosticsFromFiles(projectWarnings, Console.Out);
            }

            // Check for errors
            if (!result.Success)
            {
                Console.Error.WriteLine("Build FAILED.");
                Console.Error.WriteLine();
                RenderDiagnosticsFromFiles(result.Diagnostics.GetErrors(), Console.Error);
                Environment.Exit(1);
            }

            // Success
            Console.WriteLine("Build succeeded.");
            Console.WriteLine($"Output: {result.OutputAssemblyPath}");

            // Output verbose timing summary to stderr when --log-level is Info or higher
            OutputVerboseTimingSummary(result.Metrics, logger);

            // Output metrics if requested
            OutputProjectMetrics(result.Metrics, metricsFormat, metricsOutput);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            if (logLevel == CompilerLogLevel.Debug)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            Environment.Exit(1);
        }
    }

    static void ShowCacheInfo(string? cacheDir)
    {
        try
        {
            var cache = new OverloadIndexCache(cacheDir);
            var info = cache.GetInfo();

            Console.WriteLine("Overload Discovery Cache Information:");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"Cache Directory: {info.CacheDirectory}");
            Console.WriteLine($"Cached Assemblies: {info.CachedAssemblies}");
            Console.WriteLine($"Total Size: {FormatBytes(info.TotalSizeBytes)}");
            Console.WriteLine(new string('=', 50));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error retrieving cache info: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void HandleExplainCommand(string? code, bool list)
    {
        if (list)
        {
            var all = DiagnosticExplanations.GetAll();
            Console.WriteLine(CliBold("Documented Diagnostic Codes:"));
            Console.WriteLine(CliColor(new string('=', 60), "36")); // cyan

            string? lastCategory = null;
            foreach (var entry in all.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (entry.Value.Category != lastCategory)
                {
                    if (lastCategory != null)
                        Console.WriteLine();
                    var catColor = CategoryColor(entry.Value.Category);
                    Console.WriteLine($"  {CliColor($"[{entry.Value.Category}]", catColor, bold: true)}");
                    lastCategory = entry.Value.Category;
                }
                var entryColor = CategoryColor(entry.Value.Category);
                Console.WriteLine($"    {CliColor(entry.Key, entryColor)}  {entry.Value.Title}");
            }

            Console.WriteLine(CliColor(new string('=', 60), "36"));
            Console.WriteLine($"Total: {CliBold(all.Count.ToString())} documented codes");
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            Console.Error.WriteLine("Usage: sharpyc explain <code>");
            Console.Error.WriteLine("       sharpyc explain --list");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Example: sharpyc explain SPY0200");
            Environment.Exit(1);
            return;
        }

        var trimmedCode = code!.Trim();
        var explanation = DiagnosticExplanations.Get(trimmedCode);
        if (explanation == null)
        {
            Console.Error.WriteLine($"No explanation found for diagnostic code '{trimmedCode}'.");
            Console.Error.WriteLine("Use 'sharpyc explain --list' to see all documented codes.");
            Environment.Exit(1);
            return;
        }

        var color = CategoryColor(explanation.Category);
        Console.WriteLine($"{CliColor(explanation.Code, color, bold: true)}: {CliBold(explanation.Title)}");
        Console.WriteLine(CliColor(new string('=', 60), "36"));
        Console.WriteLine();
        Console.WriteLine(explanation.Description);

        if (explanation.Example != null)
        {
            Console.WriteLine();
            Console.WriteLine(CliColor("Example:", "36", bold: true));
            foreach (var line in explanation.Example.Split('\n'))
                Console.WriteLine($"  {line}");
        }

        if (explanation.Fix != null)
        {
            Console.WriteLine();
            Console.WriteLine(CliColor("Fix:", "36", bold: true));
            foreach (var line in explanation.Fix.Split('\n'))
                Console.WriteLine($"  {line}");
        }

        Console.WriteLine();
        Console.WriteLine($"{CliColor("Category:", "36", bold: true)} {CliColor(explanation.Category, color, bold: true)}");
    }

    /// <summary>
    /// Render a diagnostic with rich source context to stderr.
    /// Falls back to simple format if no source text is available.
    /// </summary>
    static void RenderDiagnostic(CompilerDiagnostic diagnostic, SourceText? sourceText, TextWriter writer)
    {
        writer.WriteLine(_renderer.Render(diagnostic, sourceText));
    }

    /// <summary>
    /// Render multiple diagnostics with rich source context.
    /// Groups by compiler phase when diagnostics come from multiple phases.
    /// </summary>
    static void RenderDiagnostics(IEnumerable<CompilerDiagnostic> diagnostics, SourceText? sourceText, TextWriter writer)
    {
        var diagList = diagnostics.ToList();
        var phases = diagList.Select(d => d.Phase).Distinct().ToList();
        var groupByPhase = phases.Count > 1;
        var isWarnings = diagList.Count > 0 && diagList.All(d => d.IsWarning);

        if (groupByPhase)
        {
            foreach (var phase in PhaseOrder.Where(p => diagList.Any(d => d.Phase == p)))
            {
                writer.WriteLine($"{PhaseLabel(phase, isWarnings)}:");
                foreach (var diagnostic in diagList.Where(d => d.Phase == phase))
                {
                    RenderDiagnostic(diagnostic, sourceText, writer);
                    writer.WriteLine();
                }
            }
        }
        else
        {
            foreach (var diagnostic in diagList)
            {
                RenderDiagnostic(diagnostic, sourceText, writer);
                writer.WriteLine();
            }
        }
    }

    /// <summary>
    /// Render diagnostics loading source text from each diagnostic's file path.
    /// Used for multi-file compilation results where a single SourceText is not available.
    /// Groups by compiler phase when diagnostics come from multiple phases.
    /// </summary>
    static void RenderDiagnosticsFromFiles(IEnumerable<CompilerDiagnostic> diagnostics, TextWriter writer)
    {
        var sourceCache = new Dictionary<string, SourceText?>();
        var diagList = diagnostics.ToList();
        var phases = diagList.Select(d => d.Phase).Distinct().ToList();
        var groupByPhase = phases.Count > 1;
        var isWarnings = diagList.Count > 0 && diagList.All(d => d.IsWarning);

        if (groupByPhase)
        {
            foreach (var phase in PhaseOrder.Where(p => diagList.Any(d => d.Phase == p)))
            {
                writer.WriteLine($"{PhaseLabel(phase, isWarnings)}:");
                foreach (var diagnostic in diagList.Where(d => d.Phase == phase))
                {
                    RenderDiagnosticFromFile(diagnostic, sourceCache, writer);
                }
            }
        }
        else
        {
            foreach (var diagnostic in diagList)
            {
                RenderDiagnosticFromFile(diagnostic, sourceCache, writer);
            }
        }
    }

    static void RenderDiagnosticFromFile(CompilerDiagnostic diagnostic, Dictionary<string, SourceText?> sourceCache, TextWriter writer)
    {
        SourceText? sourceText = null;

        if (!string.IsNullOrEmpty(diagnostic.FilePath))
        {
            if (!sourceCache.TryGetValue(diagnostic.FilePath, out sourceText))
            {
                try
                {
                    if (File.Exists(diagnostic.FilePath))
                    {
                        var content = File.ReadAllText(diagnostic.FilePath);
                        sourceText = new SourceText(content, diagnostic.FilePath);
                    }
                }
                catch
                {
                    // If we can't read the file, render without source context
                }
                sourceCache[diagnostic.FilePath] = sourceText;
            }
        }

        RenderDiagnostic(diagnostic, sourceText, writer);
        writer.WriteLine();
    }

    /// <summary>
    /// Ordered list of compiler phases for grouped diagnostic output.
    /// </summary>
    static readonly CompilerPhase[] PhaseOrder = new[]
    {
        CompilerPhase.Lexer,
        CompilerPhase.Parser,
        CompilerPhase.NameResolution,
        CompilerPhase.ImportResolution,
        CompilerPhase.TypeChecking,
        CompilerPhase.Validation,
        CompilerPhase.CodeGeneration,
        CompilerPhase.Assembly,
        CompilerPhase.Unknown
    };

    static string PhaseLabel(CompilerPhase phase, bool isWarnings = false)
    {
        var suffix = isWarnings ? "warnings" : "errors";
        return phase switch
        {
            CompilerPhase.Lexer => $"Lexer {suffix}",
            CompilerPhase.Parser => $"Parse {suffix}",
            CompilerPhase.NameResolution => $"Name resolution {suffix}",
            CompilerPhase.ImportResolution => $"Import resolution {suffix}",
            CompilerPhase.TypeChecking => $"Type {suffix}",
            CompilerPhase.Validation => $"Validation {suffix}",
            CompilerPhase.CodeGeneration => $"Code generation {suffix}",
            CompilerPhase.Assembly => $"Assembly {suffix}",
            CompilerPhase.Unknown => $"Other {suffix}",
            _ => $"Other {suffix}",
        };
    }

    static HashSet<string> ParseNowarnCodes(string? nowarn)
    {
        if (string.IsNullOrWhiteSpace(nowarn))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return new HashSet<string>(
            nowarn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }

    // ANSI color helpers for CLI formatting (explain command, etc.)
    static string CliBold(string text) => _useColor ? $"\x1b[1m{text}\x1b[0m" : text;
    static string CliColor(string text, string code, bool bold = false)
    {
        if (!_useColor)
            return text;
        var boldCode = bold ? "1;" : "";
        return $"\x1b[{boldCode}{code}m{text}\x1b[0m";
    }

    static string CategoryColor(string category) => category switch
    {
        "Lexer" => "33",    // yellow
        "Parser" => "33",   // yellow
        "Semantic" => "31", // red
        "Validation" => "34", // blue
        "CodeGen" => "32",  // green
        "Infrastructure" => "36", // cyan
        _ => "37"           // white
    };

    static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    static void CleanProject(ProjectConfig projectConfig)
    {
        try
        {
            var projectDir = Path.GetDirectoryName(projectConfig.ProjectFilePath);
            if (projectDir == null)
            {
                Console.Error.WriteLine("Warning: Could not determine project directory");
                return;
            }

            // Delete bin/ directory
            var binDir = Path.Combine(projectDir, "bin");
            if (Directory.Exists(binDir))
            {
                Console.WriteLine($"Deleting: {binDir}");
                Directory.Delete(binDir, recursive: true);
            }

            // Delete obj/ directory (if it exists - not currently used but may be in future)
            var objDir = Path.Combine(projectDir, "obj");
            if (Directory.Exists(objDir))
            {
                Console.WriteLine($"Deleting: {objDir}");
                Directory.Delete(objDir, recursive: true);
            }

            Console.WriteLine("Clean completed.");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Clean failed: {ex.Message}");
        }
    }

    static void SaveGeneratedCSharp(DirectoryInfo outputDir, Dictionary<string, string> generatedFiles)
    {
        try
        {
            // Create output directory if it doesn't exist
            if (!outputDir.Exists)
            {
                outputDir.Create();
            }

            Console.WriteLine($"Saving generated C# code to: {outputDir.FullName}");

            foreach (var (modulePath, csCode) in generatedFiles)
            {
                // Create a filename based on the module path
                var fileName = Path.GetFileNameWithoutExtension(modulePath) + ".cs";
                var outputPath = Path.Combine(outputDir.FullName, fileName);

                File.WriteAllText(outputPath, csCode);
                Console.WriteLine($"  Saved: {fileName}");
            }

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Could not save generated C# code: {ex.Message}");
        }
    }

    static void CompileToBinary(
        FileInfo inputFile,
        string outputType,
        FileInfo? output,
        string[] references,
        string[] projectReferences,
        string[] modulePaths,
        ICompilerLogger logger,
        string? metricsFormat,
        FileInfo? metricsOutput,
        bool warnAsError = false,
        string? nowarn = null,
        int? maxErrors = null)
    {
        try
        {
            // Read source file
            var source = File.ReadAllText(inputFile.FullName);
            var sourceText = new SourceText(source, inputFile.FullName);

            // Compile via CompilerApi
            var compilerOptions = new CompilerOptions
            {
                OutputType = outputType,
                References = references,
                ModulePaths = modulePaths,
                WarningsAsErrors = warnAsError,
                SuppressedWarnings = ParseNowarnCodes(nowarn),
                MaxErrors = maxErrors ?? 0
            };

            var api = new CompilerApi(logger);
            var result = api.Compile(source, compilerOptions, inputFile.FullName);

            if (!result.Success)
            {
                Console.Error.WriteLine("Compilation failed:");
                Console.Error.WriteLine();
                RenderDiagnostics(result.Diagnostics.Where(d => d.IsError), sourceText, Console.Error);
                Environment.Exit(1);
            }

            // Display Sharpy compilation warnings
            var compilationWarnings = result.Diagnostics.Where(d => d.IsWarning).ToList();
            if (compilationWarnings.Count > 0)
            {
                RenderDiagnostics(compilationWarnings, sourceText, Console.Out);
            }

            // Determine output path
            var inputFileName = Path.GetFileNameWithoutExtension(inputFile.Name);
            var outputDir = output != null
                ? Path.GetDirectoryName(output.FullName) ?? Directory.GetCurrentDirectory()
                : Directory.GetCurrentDirectory();

            var assemblyName = output != null
                ? Path.GetFileNameWithoutExtension(output.Name)
                : inputFileName;

            var extension = outputType.ToLowerInvariant() == "exe" ? ".exe" : ".dll";
            var finalOutputPath = output != null
                ? output.FullName
                : Path.Combine(outputDir, assemblyName + extension);

            // Create output directory if needed
            var outputDirectory = Path.GetDirectoryName(finalOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Create a minimal project config for single-file compilation
            var projectConfig = new SingleFileProjectConfig(
                projectFilePath: inputFile.FullName,
                projectDirectory: Path.GetDirectoryName(inputFile.FullName) ?? Directory.GetCurrentDirectory(),
                rootNamespace: inputFileName,
                assemblyName: assemblyName,
                outputType: outputType,
                targetFramework: "net8.0",
                configuration: "Debug",
                sourceFiles: new List<string> { inputFile.FullName },
                references: references.ToList(),
                modulePaths: modulePaths.ToList(),
                outputAssemblyPath: finalOutputPath
            );

            // Prepare C# sources for compilation - use all generated files (entry + imports)
            var csharpSources = new Dictionary<string, string>();
            foreach (var (sourcePath, csCode) in result.GeneratedCSharpFiles)
            {
                var csFileName = Path.ChangeExtension(sourcePath, ".cs");
                csharpSources[csFileName] = csCode;
            }

            // Fallback for backward compatibility if GeneratedCSharpFiles is empty
            if (csharpSources.Count == 0 && result.GeneratedCSharp != null)
            {
                csharpSources[Path.ChangeExtension(inputFile.FullName, ".cs")] = result.GeneratedCSharp;
            }

            // Compile to assembly
            var assemblyCompiler = new AssemblyCompiler(logger);
            var assemblyResult = assemblyCompiler.CompileToAssembly(csharpSources, projectConfig);

            if (!assemblyResult.Success)
            {
                Console.Error.WriteLine("Assembly compilation failed:");
                Console.Error.WriteLine();
                RenderDiagnostics(assemblyResult.Diagnostics.GetErrors(), sourceText, Console.Error);
                Environment.Exit(1);
            }

            // Display warnings
            var assemblyWarnings = assemblyResult.Diagnostics.GetWarnings();
            if (assemblyWarnings.Count > 0)
            {
                RenderDiagnostics(assemblyWarnings, sourceText, Console.Out);
            }

            // Success
            Console.WriteLine($"Successfully compiled to: {assemblyResult.OutputAssemblyPath}");

            // Output verbose timing summary to stderr when --log-level is Info or higher
            OutputVerboseTimingSummary(result.Metrics, logger);

            // Output metrics if requested (showing assembly compilation metrics)
            OutputMetrics(assemblyResult.Metrics, metricsFormat, metricsOutput);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Wrapper for ProjectConfig to override OutputAssemblyPath for single-file compilation
    /// </summary>
    private class SingleFileProjectConfig : ProjectConfig
    {
        private readonly string _outputAssemblyPath;

        public SingleFileProjectConfig(
            string projectFilePath,
            string projectDirectory,
            string rootNamespace,
            string assemblyName,
            string outputType,
            string targetFramework,
            string configuration,
            List<string> sourceFiles,
            List<string> references,
            List<string> modulePaths,
            string outputAssemblyPath)
        {
            _outputAssemblyPath = outputAssemblyPath;

            // Set properties
            ProjectFilePath = projectFilePath;
            ProjectDirectory = projectDirectory;
            RootNamespace = rootNamespace;
            AssemblyName = assemblyName;
            OutputType = outputType;
            TargetFramework = targetFramework;
            Configuration = configuration;
            SourceFiles = sourceFiles;
            References = references;
            ModulePaths = modulePaths;
        }

        public override string OutputAssemblyPath => _outputAssemblyPath;
    }
}
