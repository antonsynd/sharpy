using System.CommandLine;
using Sharpy.Compiler;
using Sharpy.Compiler.Logging;

namespace Sharpy.Cli.Commands;

internal static class ProjectCommand
{
    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("project", "Build a Sharpy project from a .spyproj file");

        var projFileArg = new Argument<FileInfo?>("project") { Description = "Path to .spyproj file (auto-discovers if not specified)", Arity = ArgumentArity.ZeroOrOne };
        var configOpt = new Option<string?>("--configuration") { Description = "Build configuration (Debug or Release)" };
        configOpt.Aliases.Add("-c");
        var cleanOpt = new Option<bool>("--clean") { Description = "Delete bin/ and obj/ directories before building" };
        var emitCsOpt = new Option<DirectoryInfo?>("--emit-cs-to") { Description = "Save generated C# code to the specified directory" };
        var incrementalOpt = new Option<bool>("--incremental") { Description = "Enable incremental compilation (only recompile changed files)" };

        command.Arguments.Add(projFileArg);
        command.Options.Add(configOpt);
        command.Options.Add(cleanOpt);
        command.Options.Add(emitCsOpt);
        command.Options.Add(incrementalOpt);

        command.SetAction((parseResult) =>
        {
            var project = parseResult.GetValue(projFileArg);
            var configuration = parseResult.GetValue(configOpt) ?? "Debug";
            var clean = parseResult.GetValue(cleanOpt);
            var emitCsTo = parseResult.GetValue(emitCsOpt);
            var incremental = parseResult.GetValue(incrementalOpt);
            var logLevel = parseResult.GetValue(globals.LogLevel) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(globals.LogFile);
            var metricsFormat = parseResult.GetValue(globals.MetricsFormat);
            var metricsOutput = parseResult.GetValue(globals.MetricsOutput);
            var warnAsError = parseResult.GetValue(globals.WarnAsError);
            var nowarn = parseResult.GetValue(globals.Nowarn);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);

            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            HandleProjectCommand(project, configuration, clean, incremental, emitCsTo, logger, logLevel, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors);
        });

        root.Subcommands.Add(command);
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

    static void CompileProject(FileInfo projectFile, string configuration, bool clean, bool incremental, DirectoryInfo? emitCsTo, ICompilerLogger logger, CompilerLogLevel logLevel = CompilerLogLevel.None, string? metricsFormat = null, FileInfo? metricsOutput = null, bool warnAsError = false, string? nowarn = null, int? maxErrors = null)
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

            var allReferences = CliHelpers.GetDefaultReferences()
                .Concat(projectConfig.References)
                .Distinct()
                .ToArray();

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

            if (emitCsTo != null && result.GeneratedCSharpFiles.Any())
            {
                SaveGeneratedCSharp(emitCsTo, result.GeneratedCSharpFiles);
            }

            var projectWarnings = result.Diagnostics.GetWarnings();
            if (projectWarnings.Count > 0)
            {
                CliHelpers.RenderDiagnosticsFromFiles(projectWarnings, Console.Out);
            }

            if (!result.Success)
            {
                Console.Error.WriteLine("Build FAILED.");
                Console.Error.WriteLine();
                CliHelpers.RenderDiagnosticsFromFiles(result.Diagnostics.GetErrors(), Console.Error);
                Environment.Exit(1);
            }

            Console.WriteLine("Build succeeded.");
            Console.WriteLine($"Output: {result.OutputAssemblyPath}");

            CliHelpers.OutputVerboseTimingSummary(result.Metrics, logger);
            CliHelpers.OutputProjectMetrics(result.Metrics, metricsFormat, metricsOutput);
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

    static void SaveGeneratedCSharp(DirectoryInfo outputDir, Dictionary<string, string> generatedFiles)
    {
        try
        {
            if (!outputDir.Exists)
            {
                outputDir.Create();
            }

            Console.WriteLine($"Saving generated C# code to: {outputDir.FullName}");

            foreach (var (modulePath, csCode) in generatedFiles)
            {
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
}
