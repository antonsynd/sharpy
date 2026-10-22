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
        var command = new Command("build", "Compile a Sharpy source file to a binary or library. Copies its runtime dependencies and references beside the output, so it runs from there (--no-deps writes the assembly only)");

        var inputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file to compile" };
        var typeOpt = new Option<string?>("--type") { Description = "Output type: 'exe' or 'library' (default: exe)" };
        typeOpt.Aliases.Add("-t");
        var outputOpt = new Option<FileInfo?>("--output") { Description = "Output file path" };
        outputOpt.Aliases.Add("-o");
        // One value per occurrence — repeat the flag to collect more (#1179, #1215).
        var refOpt = new Option<string[]>("--reference") { Description = "Add a .NET assembly reference (repeatable)" };
        refOpt.Aliases.Add("-r");
        var projRefOpt = new Option<string[]>("--project-reference") { Description = "Add a .NET project reference (repeatable)" };
        projRefOpt.Aliases.Add("-p");
        var modPathOpt = new Option<string[]>("--module-path") { Description = "Additional path to search for modules (repeatable)" };
        modPathOpt.Aliases.Add("-m");
        // The spelling `compile` has always used (#2313 cell 0, R-HF c).
        var noDepsOpt = new Option<bool>("--no-deps") { Description = "Skip copying runtime dependencies alongside the output" };
        var serverOpt = new Option<string?>("--server") { Description = "Compile via a keep-alive 'sharpyc server' on the given pipe (default pipe if no name); falls back to in-process if none is running", Arity = ArgumentArity.ZeroOrOne };

        command.Arguments.Add(inputArg);
        command.Options.Add(typeOpt);
        command.Options.Add(outputOpt);
        command.Options.Add(refOpt);
        command.Options.Add(projRefOpt);
        command.Options.Add(modPathOpt);
        command.Options.Add(noDepsOpt);
        command.Options.Add(serverOpt);

        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            if (!CommandOptionEffects.Refuse(parseResult, command)
                || !CliHelpers.RefuseInputKind(input, CliHelpers.SourceInputKinds))
            {
                return CliHelpers.ExitCompileError;
            }

            var type = parseResult.GetValue(typeOpt) ?? "exe";
            var output = parseResult.GetValue(outputOpt);
            var reference = parseResult.GetValue(refOpt) ?? Array.Empty<string>();
            var projectReference = parseResult.GetValue(projRefOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(modPathOpt) ?? Array.Empty<string>();
            var noDeps = parseResult.GetValue(noDepsOpt);
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
            if (parseResult.GetResult(serverOpt) is not null && CliHelpers.ServerRouteOrInProcess(parseResult, globals))
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
                    pipeName, request, Console.Out, Console.Error, out var serverUsedPaths, verbose: parseResult.GetValue(globals.Verbose));
                if (serverExit.HasValue)
                {
                    // The server wrote the assembly; the deployment is the client's, as for `run --server`.
                    if (serverExit.Value == 0 && !noDeps)
                    {
                        Deploy(OutputDirectory(output), serverUsedPaths, reference, modulePath);
                    }
                    return serverExit.Value;
                }
            }

            var compileResult = CompileToBinary(input, type, output, reference, modulePath, logger, metricsFormat, metricsOutput, out var failureExitCode, warnAsError, nowarn, maxErrors, configuration: "Debug", features: features);
            if (compileResult == null)
            {
                return failureExitCode;
            }

            if (!noDeps)
            {
                var outputDir = compileResult.OutputAssemblyPath != null
                    ? Path.GetDirectoryName(compileResult.OutputAssemblyPath) ?? OutputDirectory(output)
                    : OutputDirectory(output);
                Deploy(outputDir, compileResult.UsedAssemblyPaths, reference, modulePath);
            }
            return 0;
        });

        root.Subcommands.Add(command);
    }

    /// <summary>
    /// `build` deploys like `compile` and `run` (#2313 cell 0, R-HF c): Sharpy.Core, the used
    /// standard-library assemblies and every `-r` reference, with what sits beside each, are copied
    /// beside the output, so it runs in place as `dotnet build`'s output does. The deployment seed's
    /// user half is the `-r` list resolved exactly as <see cref="CompileToBinary"/> hands it to the
    /// compiler — the same seed `compile` forms (#2223).
    /// </summary>
    static void Deploy(string outputDir, IReadOnlySet<string> usedAssemblyPaths, string[] references, string[] modulePaths)
        => RuntimeDependencyHelper.CopyRuntimeDependencies(
            outputDir, usedAssemblyPaths, CliHelpers.ResolveReferences(references, modulePaths));

    /// <summary>
    /// The directory `build` writes the assembly to: <c>-o</c>'s directory, else the working
    /// directory — the rule <see cref="CompileToBinary"/> applies, and the one a server compile
    /// applies with the client's working directory.
    /// </summary>
    static string OutputDirectory(FileInfo? output)
        => output != null
            ? Path.GetDirectoryName(output.FullName) ?? Directory.GetCurrentDirectory()
            : Directory.GetCurrentDirectory();

    /// <summary>
    /// Compiles a Sharpy source file to a binary. Returns the <see cref="CompileResult"/>
    /// on success, or <c>null</c> if compilation or assembly generation failed; then
    /// <paramref name="failureExitCode"/> carries the ICE (3) / escaped-C# (2) / ordinary-error (1)
    /// distinction mapped from this call's own diagnostics (<see cref="CliHelpers.MapFailureExitCode"/>).
    /// It is a return value, not a process global read after the fact: a long-lived process
    /// (the compile server) must never answer one request with another's exit code (#2250).
    /// </summary>
    internal static CompileResult? CompileToBinary(
        FileInfo inputFile,
        string outputType,
        FileInfo? output,
        string[] references,
        string[] modulePaths,
        ICompilerLogger logger,
        string? metricsFormat,
        FileInfo? metricsOutput,
        out int failureExitCode,
        bool warnAsError = false,
        string? nowarn = null,
        int? maxErrors = null,
        string configuration = "Debug",
        string[]? features = null,
        string? namespaceName = null)
    {
        // The one single-file seam behind build, run, compile <file>.spy and the compile server:
        // a module path that does not exist is an error rather than ignored (#2173). A project
        // reference never reaches here: the commands refuse it from their option table
        // (CommandOptionEffects.Refuse) and the server refuses it from the request.
        failureExitCode = CliHelpers.ExitCompileError;
        if (!CliHelpers.ValidateModulePaths(modulePaths))
        {
            return null;
        }

        references = CliHelpers.ResolveReferences(references, modulePaths);

        try
        {
            var source = File.ReadAllText(inputFile.FullName);

            var inputFileName = Path.GetFileNameWithoutExtension(inputFile.Name);
            var outputDir = OutputDirectory(output);

            var assemblyName = output != null
                ? Path.GetFileNameWithoutExtension(output.Name)
                : inputFileName;

            var extension = outputType.ToLowerInvariant() == "exe" ? ".exe" : ".dll";
            var finalOutputPath = output != null
                ? output.FullName
                : Path.Combine(outputDir, assemblyName + extension);

            // The output directory is created by the assembly writer when the assembly is written,
            // not here: a compile refused before that (a `-r` that resolves nowhere is SPY0305 from
            // the registry) must leave nothing behind, not even an empty directory (#2223).

            // #1038: single-file compilation is a synthetic project-of-one-file driven through
            // ProjectCompiler. Handing the output location, assembly name, and configuration to
            // CompilerApi lets the project pipeline emit the assembly itself using the very same
            // ProjectConfig it used for code generation — there is no longer a separate
            // AssemblyCompiler hand-off or SingleFileProjectConfig wrapper.
            var compilerOptions = CompilerOptionsFactory.ForCli(
                outputType: outputType,
                references: references,
                modulePaths: modulePaths,
                warningsAsErrors: warnAsError,
                suppressedWarnings: CliHelpers.ParseNowarnCodes(nowarn),
                maxErrors: maxErrors ?? 0,
                features: CliHelpers.ParseFeatures(features),
                @namespace: namespaceName,
                configuration: configuration,
                assemblyName: assemblyName,
                outputAssemblyPath: finalOutputPath);

            var api = CliHelpers.CreateCompilerApi(logger);
            var result = api.Compile(source, compilerOptions, inputFile.FullName);

            if (!result.Success)
            {
                Console.Error.WriteLine("Compilation failed:");
                Console.Error.WriteLine();
                // Each diagnostic is rendered against its OWN file, exactly as `project` and
                // `compile` already do. A single-file `build`/`run` still pulls in siblings through
                // import resolution, so handing the entry buffer to every diagnostic made a sibling
                // file's parse error underline an innocent line of the entry file (#1437).
                CliHelpers.RenderDiagnosticsFromFiles(result.Diagnostics.Where(d => d.IsError), Console.Error, inputFile.FullName);
                failureExitCode = CliHelpers.MapFailureExitCode(result.Diagnostics);
                return null;
            }

            var compilationWarnings = result.Diagnostics.Where(d => d.IsWarning).ToList();
            if (compilationWarnings.Count > 0)
            {
                CliHelpers.RenderDiagnosticsFromFiles(compilationWarnings, Console.Out, inputFile.FullName);
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
            failureExitCode = CliHelpers.ExitInternalError;
            return null;
        }
    }
}
