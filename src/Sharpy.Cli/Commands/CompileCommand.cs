using System.CommandLine;
using Sharpy.Compiler;
using Sharpy.Compiler.Logging;

namespace Sharpy.Cli.Commands;

/// <summary>
/// The <c>compile</c> command produces a standalone <c>.dll</c>/<c>.exe</c> artifact
/// on disk, copying the runtime dependencies (Sharpy.Core.dll, used stdlib assemblies,
/// and their NuGet dependencies) alongside the output so it can be executed via
/// <c>dotnet output.dll</c>. Handles both single <c>.spy</c> files and <c>.spyproj</c>
/// projects.
/// </summary>
internal static class CompileCommand
{
    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("compile", "Compile Sharpy source to a standalone .dll or .exe");

        var inputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file (.spy) or project file (.spyproj)" };
        var outputOpt = new Option<FileInfo?>("--output") { Description = "Output file path" };
        outputOpt.Aliases.Add("-o");
        var configOpt = new Option<string?>("--configuration") { Description = "Build configuration (Debug or Release)" };
        configOpt.Aliases.Add("-c");
        var typeOpt = new Option<string?>("--type") { Description = "Output type: 'exe' or 'library' (ignored for .spyproj)" };
        typeOpt.Aliases.Add("-t");
        var refOpt = new Option<string[]>("--reference") { Description = "Add .NET assembly references", AllowMultipleArgumentsPerToken = true };
        refOpt.Aliases.Add("-r");
        var projRefOpt = new Option<string[]>("--project-reference") { Description = "Add .NET project references", AllowMultipleArgumentsPerToken = true };
        projRefOpt.Aliases.Add("-p");
        var modPathOpt = new Option<string[]>("--module-path") { Description = "Additional paths to search for modules", AllowMultipleArgumentsPerToken = true };
        modPathOpt.Aliases.Add("-m");
        var selfContainedOpt = new Option<bool>("--self-contained") { Description = "Produce a self-contained executable (no .NET runtime required)" };
        var noDepsOpt = new Option<bool>("--no-deps") { Description = "Skip copying runtime dependencies alongside the output" };
        var incrementalOpt = new Option<bool>("--incremental") { Description = "Enable incremental compilation for .spyproj projects" };
        var cleanOpt = new Option<bool>("--clean") { Description = "Delete bin/ and obj/ before building a .spyproj project" };

        command.Arguments.Add(inputArg);
        command.Options.Add(outputOpt);
        command.Options.Add(configOpt);
        command.Options.Add(typeOpt);
        command.Options.Add(refOpt);
        command.Options.Add(projRefOpt);
        command.Options.Add(modPathOpt);
        command.Options.Add(selfContainedOpt);
        command.Options.Add(noDepsOpt);
        command.Options.Add(incrementalOpt);
        command.Options.Add(cleanOpt);

        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var output = parseResult.GetValue(outputOpt);
            var configuration = parseResult.GetValue(configOpt) ?? "Release";
            var type = parseResult.GetValue(typeOpt);
            var reference = parseResult.GetValue(refOpt) ?? Array.Empty<string>();
            var projectReference = parseResult.GetValue(projRefOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(modPathOpt) ?? Array.Empty<string>();
            var selfContained = parseResult.GetValue(selfContainedOpt);
            var noDeps = parseResult.GetValue(noDepsOpt);
            var incremental = parseResult.GetValue(incrementalOpt);
            var clean = parseResult.GetValue(cleanOpt);
            var logLevel = parseResult.GetValue(globals.LogLevel) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(globals.LogFile);
            var metricsFormat = parseResult.GetValue(globals.MetricsFormat);
            var metricsOutput = parseResult.GetValue(globals.MetricsOutput);
            var warnAsError = parseResult.GetValue(globals.WarnAsError);
            var nowarn = parseResult.GetValue(globals.Nowarn);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);

            var logger = CliHelpers.CreateLogger(logLevel, logFile);

            if (input.Extension.Equals(".spyproj", StringComparison.OrdinalIgnoreCase))
            {
                return CompileProject(input, configuration, clean, incremental, noDeps, selfContained, logger, logLevel, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors);
            }

            return CompileSingleFile(input, output, configuration, type, reference, projectReference, modulePath, noDeps, selfContained, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors);
        });

        root.Subcommands.Add(command);
    }

    static int CompileSingleFile(
        FileInfo inputFile,
        FileInfo? output,
        string configuration,
        string? type,
        string[] references,
        string[] projectReferences,
        string[] modulePaths,
        bool noDeps,
        bool selfContained,
        ICompilerLogger logger,
        string? metricsFormat,
        FileInfo? metricsOutput,
        bool warnAsError,
        string? nowarn,
        int? maxErrors)
    {
        if (!CliHelpers.ValidateInputFile(inputFile))
        {
            return 1;
        }

        var outputType = type ?? "exe";
        var extension = outputType.ToLowerInvariant() == "exe" ? ".exe" : ".dll";

        string outputPath;
        if (output != null)
        {
            outputPath = output.FullName;
        }
        else
        {
            var assemblyName = Path.GetFileNameWithoutExtension(inputFile.Name);
            var defaultDir = Path.Combine(Directory.GetCurrentDirectory(), "bin", configuration);
            outputPath = Path.Combine(defaultDir, assemblyName + extension);
        }

        var outputDir = Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var compileResult = BuildCommand.CompileToBinary(
            inputFile, outputType, new FileInfo(outputPath), references, projectReferences, modulePaths,
            logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors, configuration);

        if (compileResult == null)
        {
            return 1;
        }

        if (!noDeps)
        {
            RuntimeDependencyHelper.CopyRuntimeDependencies(outputDir, compileResult.UsedAssemblyPaths);
        }

        if (selfContained)
        {
            Console.Error.WriteLine("Self-contained publishing not yet supported.");
            return 1;
        }

        ReportOutput(outputPath);
        return 0;
    }

    static int CompileProject(
        FileInfo projectFile,
        string configuration,
        bool clean,
        bool incremental,
        bool noDeps,
        bool selfContained,
        ICompilerLogger logger,
        CompilerLogLevel logLevel,
        string? metricsFormat,
        FileInfo? metricsOutput,
        bool warnAsError,
        string? nowarn,
        int? maxErrors)
    {
        try
        {
            var projectConfig = ProjectFileParser.Load(projectFile.FullName, configuration);

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

            var mergedSuppressed = new HashSet<string>(projectConfig.SuppressedWarnings);
            mergedSuppressed.UnionWith(CliHelpers.ParseNowarnCodes(nowarn));

            var defaultReferences = CliHelpers.GetDefaultReferences();
            var allReferences = defaultReferences
                .Concat(projectConfig.References)
                .Distinct()
                .ToArray();

            foreach (var defaultRef in defaultReferences)
            {
                if (!projectConfig.References.Contains(defaultRef))
                    projectConfig.References.Add(defaultRef);
            }

            var compilerOptions = new CompilerOptions
            {
                References = allReferences,
                ModulePaths = projectConfig.ModulePaths.ToArray(),
                WarningsAsErrors = warnAsError || projectConfig.WarningsAsErrors,
                SuppressedWarnings = mergedSuppressed,
                MaxErrors = maxErrors ?? 0,
                Incremental = incremental
            };

            var compiler = new Sharpy.Compiler.Compiler(compilerOptions, logger);
            var result = compiler.CompileProject(projectConfig);

            var projectWarnings = result.Diagnostics.GetWarnings();
            if (projectWarnings.Count > 0)
            {
                CliHelpers.RenderDiagnosticsFromFiles(projectWarnings, Console.Out);
            }

            if (!result.Success)
            {
                Console.Error.WriteLine("Compilation FAILED.");
                Console.Error.WriteLine();
                CliHelpers.RenderDiagnosticsFromFiles(result.Diagnostics.GetErrors(), Console.Error);
                return 1;
            }

            var outputPath = result.OutputAssemblyPath;
            if (outputPath != null && !noDeps)
            {
                var outputDir = Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory();
                // The project result does not expose per-module used assemblies, so
                // conservatively copy every referenced runtime assembly.
                RuntimeDependencyHelper.CopyRuntimeDependencies(outputDir, new HashSet<string>(defaultReferences, StringComparer.OrdinalIgnoreCase));
            }

            if (selfContained)
            {
                Console.Error.WriteLine("Self-contained publishing not yet supported.");
                return 1;
            }

            CliHelpers.OutputVerboseTimingSummary(result.Metrics, logger);
            CliHelpers.OutputProjectMetrics(result.Metrics, metricsFormat, metricsOutput);

            if (outputPath != null)
            {
                ReportOutput(outputPath);
            }
            return 0;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            if (logLevel == CompilerLogLevel.Debug)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    static void ReportOutput(string outputPath)
    {
        Console.WriteLine($"Output: {outputPath}");
        if (File.Exists(outputPath))
        {
            var size = new FileInfo(outputPath).Length;
            Console.WriteLine($"Size: {CliHelpers.FormatBytes(size)}");
        }
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

            var binDir = Path.Combine(projectDir, "bin");
            if (Directory.Exists(binDir))
            {
                Console.WriteLine($"Deleting: {binDir}");
                Directory.Delete(binDir, recursive: true);
            }

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
}
