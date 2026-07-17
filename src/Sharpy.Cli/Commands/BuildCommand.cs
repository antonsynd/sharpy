extern alias SharpyRT;
using System.CommandLine;
using Sharpy.Cli.Services;
using Sharpy.Compiler;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;

namespace Sharpy.Cli.Commands;

internal static class BuildCommand
{
    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("build", "Compile a Sharpy source file to a binary or library");

        var inputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file to compile" };
        var typeOpt = new Option<string?>("--type") { Description = "Output type: 'exe' or 'library' (default: exe)" };
        typeOpt.Aliases.Add("-t");
        var outputOpt = new Option<FileInfo?>("--output") { Description = "Output file path" };
        outputOpt.Aliases.Add("-o");
        var refOpt = new Option<string[]>("--reference") { Description = "Add .NET assembly references", AllowMultipleArgumentsPerToken = true };
        refOpt.Aliases.Add("-r");
        var projRefOpt = new Option<string[]>("--project-reference") { Description = "Add .NET project references", AllowMultipleArgumentsPerToken = true };
        projRefOpt.Aliases.Add("-p");
        var modPathOpt = new Option<string[]>("--module-path") { Description = "Additional paths to search for modules", AllowMultipleArgumentsPerToken = true };
        modPathOpt.Aliases.Add("-m");
        var serverOpt = new Option<string?>("--server") { Description = "Compile via a keep-alive 'sharpyc server' on the given pipe (default pipe if no name); falls back to in-process if none is running", Arity = ArgumentArity.ZeroOrOne };

        command.Arguments.Add(inputArg);
        command.Options.Add(typeOpt);
        command.Options.Add(outputOpt);
        command.Options.Add(refOpt);
        command.Options.Add(projRefOpt);
        command.Options.Add(modPathOpt);
        command.Options.Add(serverOpt);

        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var type = parseResult.GetValue(typeOpt) ?? "exe";
            var output = parseResult.GetValue(outputOpt);
            var reference = parseResult.GetValue(refOpt) ?? Array.Empty<string>();
            var projectReference = parseResult.GetValue(projRefOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(modPathOpt) ?? Array.Empty<string>();
            var logLevel = globals.ResolveLogLevel(parseResult);
            CliHelpers.ShowDiagnosticProvenance = parseResult.GetValue(globals.Verbose);
            var logFile = parseResult.GetValue(globals.LogFile);
            var metricsFormat = parseResult.GetValue(globals.MetricsFormat);
            var metricsOutput = parseResult.GetValue(globals.MetricsOutput);
            var warnAsError = parseResult.GetValue(globals.WarnAsError);
            var nowarn = parseResult.GetValue(globals.Nowarn);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);
            var features = parseResult.GetValue(globals.EnableFeature);

            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            if (!CliHelpers.ValidateInputFile(input))
            {
                return 1;
            }

            // --server: try the keep-alive server first; a null result means no server answered and
            // we should fall through to the in-process compile below (explicit over magic, #1049).
            if (parseResult.GetResult(serverOpt) is not null)
            {
                var pipeName = parseResult.GetValue(serverOpt) ?? CompileServerProtocol.DefaultPipeName;
                var request = new CompileServerRequest
                {
                    Command = CompileServerProtocol.CommandCompile,
                    Input = input.FullName,
                    Output = output?.FullName,
                    OutputType = type,
                    References = reference,
                    ProjectReferences = projectReference,
                    ModulePaths = modulePath,
                    Features = features ?? Array.Empty<string>(),
                    Configuration = "Debug",
                    WarnAsError = warnAsError,
                    Nowarn = nowarn,
                    MaxErrors = maxErrors,
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                };

                var serverExit = CompileServerClient.TryRun(
                    pipeName, request, Console.Out, Console.Error, verbose: parseResult.GetValue(globals.Verbose));
                if (serverExit.HasValue)
                {
                    return serverExit.Value;
                }
            }

            var compileResult = CompileToBinary(input, type, output, reference, projectReference, modulePath, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors, configuration: "Debug", features: features);
            return compileResult == null ? CliHelpers.LastFailureExitCode : 0;
        });

        root.Subcommands.Add(command);
    }

    /// <summary>
    /// Compiles a Sharpy source file to a binary. Returns the <see cref="CompileResult"/>
    /// on success, or <c>null</c> if compilation or assembly generation failed (the
    /// caller should treat a <c>null</c> result as exit code 1).
    /// </summary>
    internal static CompileResult? CompileToBinary(
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
        int? maxErrors = null,
        string configuration = "Debug",
        string[]? features = null)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var sourceText = new SourceText(source, inputFile.FullName);

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

            var outputDirectory = Path.GetDirectoryName(finalOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // #1038: single-file compilation is a synthetic project-of-one-file driven through
            // ProjectCompiler. Handing the output location, assembly name, and configuration to
            // CompilerApi lets the project pipeline emit the assembly itself using the very same
            // ProjectConfig it used for code generation — there is no longer a separate
            // AssemblyCompiler hand-off or SingleFileProjectConfig wrapper.
            var compilerOptions = new CompilerOptions
            {
                OutputType = outputType,
                References = references,
                ModulePaths = modulePaths,
                WarningsAsErrors = warnAsError,
                SuppressedWarnings = CliHelpers.ParseNowarnCodes(nowarn),
                MaxErrors = maxErrors ?? 0,
                Features = CliHelpers.ParseFeatures(features),
                Configuration = configuration,
                AssemblyName = assemblyName,
                OutputAssemblyPath = finalOutputPath
            };

            var api = CliHelpers.CreateCompilerApi(logger);
            var result = api.Compile(source, compilerOptions, inputFile.FullName);

            if (!result.Success)
            {
                Console.Error.WriteLine("Compilation failed:");
                Console.Error.WriteLine();
                CliHelpers.RenderDiagnostics(result.Diagnostics.Where(d => d.IsError), sourceText, Console.Error);
                CliHelpers.LastFailureExitCode = CliHelpers.MapFailureExitCode(result.Diagnostics);
                return null;
            }

            var compilationWarnings = result.Diagnostics.Where(d => d.IsWarning).ToList();
            if (compilationWarnings.Count > 0)
            {
                CliHelpers.RenderDiagnostics(compilationWarnings, sourceText, Console.Out);
            }

            Console.WriteLine($"Successfully compiled to: {result.OutputAssemblyPath ?? finalOutputPath}");

            CliHelpers.OutputVerboseTimingSummary(result.ProjectMetrics, logger);
            CliHelpers.OutputProjectMetrics(result.ProjectMetrics, metricsFormat, metricsOutput);

            return result;
        }
        catch (Exception ex)
        {
            // An exception here (not surfaced as a diagnostic) is itself an internal error.
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            CliHelpers.LastFailureExitCode = CliHelpers.ExitInternalError;
            return null;
        }
    }
}
