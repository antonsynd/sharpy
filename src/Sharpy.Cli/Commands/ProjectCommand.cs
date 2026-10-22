using System.CommandLine;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Shared;

namespace Sharpy.Cli.Commands;

internal static class ProjectCommand
{
    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("project", "Build a Sharpy project from a .spyproj file. Copies its runtime dependencies and references beside the output, so it runs from there (--no-deps writes the assembly only)");

        var projFileArg = new Argument<FileInfo?>("project") { Description = "Path to .spyproj file (auto-discovers if not specified)", Arity = ArgumentArity.ZeroOrOne };
        var configOpt = new Option<string?>("--configuration") { Description = "Build configuration (Debug or Release)" };
        configOpt.Aliases.Add("-c");
        var cleanOpt = new Option<bool>("--clean") { Description = "Delete bin/ and obj/ directories before building" };
        var emitCsOpt = new Option<DirectoryInfo?>("--emit-cs-to") { Description = "Save generated C# code to the specified directory" };
        var incrementalOpt = new Option<bool>("--incremental") { Description = "Enable incremental compilation (only recompile changed files)" };
        // `compile app.spyproj` takes these and `project` did not (a parse error), so the two project
        // commands diverged (#2223); both now merge them through CliHelpers.MergeCommandLineIntoProject.
        var refOpt = new Option<string[]>("--reference") { Description = "Add a .NET assembly reference (repeatable)" };
        refOpt.Aliases.Add("-r");
        var modPathOpt = new Option<string[]>("--module-path") { Description = "Additional path to search for modules (repeatable)" };
        modPathOpt.Aliases.Add("-m");
        // The spelling `compile` has always used (#2313 cell 0, R-HF c).
        var noDepsOpt = new Option<bool>("--no-deps") { Description = "Skip copying runtime dependencies alongside the output" };

        command.Arguments.Add(projFileArg);
        command.Options.Add(configOpt);
        command.Options.Add(cleanOpt);
        command.Options.Add(emitCsOpt);
        command.Options.Add(incrementalOpt);
        command.Options.Add(refOpt);
        command.Options.Add(modPathOpt);
        command.Options.Add(noDepsOpt);

        command.SetAction((parseResult) =>
        {
            var project = parseResult.GetValue(projFileArg);
            var configuration = parseResult.GetValue(configOpt) ?? "Debug";
            var clean = parseResult.GetValue(cleanOpt);
            var emitCsTo = parseResult.GetValue(emitCsOpt);
            var incremental = parseResult.GetValue(incrementalOpt);
            var logLevel = globals.ResolveLogLevel(parseResult);
            CliHelpers.ShowDiagnosticProvenance = parseResult.GetValue(globals.Verbose);
            var logFile = parseResult.GetValue(globals.LogFile);
            var metricsFormat = parseResult.GetValue(globals.MetricsFormat);
            var metricsOutput = parseResult.GetValue(globals.MetricsOutput);
            var warnAsError = parseResult.GetValue(globals.WarnAsError);
            var nowarn = parseResult.GetValue(globals.Nowarn);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);
            var features = parseResult.GetValue(globals.EnableFeature);
            var reference = parseResult.GetValue(refOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(modPathOpt) ?? Array.Empty<string>();
            var noDeps = parseResult.GetValue(noDepsOpt);

            // The input-kind refusal comes before anything is written or validated: a .spy (Sharpy
            // source, not XML) or a directory used to reach the XML loader as "Unexpected error:
            // Data at the root level is invalid", and until P46 verify the refusal ran after the
            // logger had created --log-file and -m had been validated (#2223).
            if (project != null && !CliHelpers.RefuseInputKind(project, CliHelpers.ProjectInputKinds))
            {
                return CliHelpers.ExitCompileError;
            }

            if (!CliHelpers.ValidateModulePaths(modulePath))
            {
                return CliHelpers.ExitCompileError;
            }

            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            return HandleProjectCommand(project, configuration, clean, incremental, emitCsTo, logger, logLevel, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors, features, reference, modulePath, noDeps);
        });

        root.Subcommands.Add(command);
    }

    static int HandleProjectCommand(
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
        int? maxErrors = null,
        string[]? features = null,
        string[]? references = null,
        string[]? modulePaths = null,
        bool noDeps = false)
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
                return 1;
            }

            resolvedProjectFile = new FileInfo(discoveredPath);
            Console.WriteLine($"Building project: {Path.GetFileName(discoveredPath)}");
        }

        return CompileProject(resolvedProjectFile, configuration, clean, incremental, emitCsTo, logger, logLevel, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors, features, references, modulePaths, noDeps);
    }

    /// <summary>
    /// Restores the project's <c>&lt;PackageReference&gt;</c> items, recording the versions NuGet chose;
    /// <c>false</c> after reporting the failure. With no packages it does nothing. The one restore path
    /// for the interactive and the <c>--server</c> build, so both refuse before any work when there is
    /// no <c>dotnet</c> to restore with (#2292) — the self-contained release archive carries none.
    /// </summary>
    static bool RestorePackages(ProjectConfig projectConfig, ICompilerLogger logger)
    {
        if (projectConfig.PackageReferences.Count == 0)
        {
            return true;
        }

        if (!CliHelpers.TryResolveDotnet(NuGetRestorer.Purpose, DotnetRequirement.Sdk, out var dotnetPath))
        {
            return false;
        }

        Console.WriteLine($"Restoring {projectConfig.PackageReferences.Count} NuGet package(s)...");
        var restoreResult = NuGetRestorer.RestorePackages(
            projectConfig.PackageReferences,
            projectConfig.TargetFramework,
            logger,
            dotnetPath);
        if (!restoreResult)
        {
            if (restoreResult.Diagnostic != null)
            {
                CliHelpers.RenderDiagnostic(restoreResult.Diagnostic, null, Console.Error);
            }

            Console.Error.WriteLine("Error: NuGet package restore failed.");
            return false;
        }

        projectConfig.ResolvedVersions = restoreResult.ResolvedVersions;
        return true;
    }

    static int CompileProject(FileInfo projectFile, string configuration, bool clean, bool incremental, DirectoryInfo? emitCsTo, ICompilerLogger logger, CompilerLogLevel logLevel = CompilerLogLevel.None, string? metricsFormat = null, FileInfo? metricsOutput = null, bool warnAsError = false, string? nowarn = null, int? maxErrors = null, string[]? features = null, string[]? references = null, string[]? modulePaths = null, bool noDeps = false)
    {
        try
        {
            var projectConfig = ProjectFileParser.Load(projectFile.FullName, configuration);

            // -r/-m join the project's items exactly as for `compile app.spyproj`; a -r that resolves
            // nowhere is refused before anything is cleaned, restored or compiled (#2223).
            var unresolved = CliHelpers.MergeCommandLineIntoProject(
                projectConfig, references ?? Array.Empty<string>(), modulePaths ?? Array.Empty<string>());
            if (unresolved.Count > 0)
            {
                Console.Error.WriteLine("Compilation FAILED.");
                Console.Error.WriteLine();
                CliHelpers.RenderDiagnosticsFromFiles(unresolved, Console.Error, projectConfig.EntryPoint);
                return CliHelpers.ExitCompileError;
            }

            if (clean)
            {
                CleanProject(projectConfig);
            }

            if (!RestorePackages(projectConfig, logger))
            {
                return CliHelpers.ExitCompileError;
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

            // The deployment seed's user half, captured before the default references join the list
            // below: the .spyproj's own <Reference> items plus the merged `-r` — the seed
            // `compile app.spyproj` forms (#2223).
            var userReferences = projectConfig.References.ToList();
            var defaultReferences = CliHelpers.GetDefaultReferences();

            var compilerOptions = CompilerOptionsFactory.ForProject(
                projectConfig,
                defaultReferences: defaultReferences,
                warningsAsErrors: warnAsError,
                additionalSuppressedWarnings: CliHelpers.ParseNowarnCodes(nowarn),
                maxErrors: maxErrors ?? 0,
                incremental: incremental,
                features: CliHelpers.ParseFeatures(features));

            // Inject CLI default references into ProjectConfig so AssemblyCompiler
            // can resolve types from Sharpy.Core/Stdlib during Roslyn compilation
            DefaultReferences.ApplyTo(projectConfig, defaultReferences);

            var compiler = new Sharpy.Compiler.Compiler(compilerOptions, logger);

            var result = compiler.CompileProject(projectConfig);

            if (emitCsTo != null && result.GeneratedCSharpFiles.Any())
            {
                SaveGeneratedCSharp(emitCsTo, result.GeneratedCSharpFiles);
            }

            var projectWarnings = result.Diagnostics.GetWarnings();
            if (projectWarnings.Count > 0)
            {
                CliHelpers.RenderDiagnosticsFromFiles(projectWarnings, Console.Out, projectConfig.EntryPoint);
            }

            if (!result.Success)
            {
                Console.Error.WriteLine("Build FAILED.");
                Console.Error.WriteLine();
                var errors = result.Diagnostics.GetErrors();
                CliHelpers.RenderDiagnosticsFromFiles(errors, Console.Error, projectConfig.EntryPoint);
                return CliHelpers.MapFailureExitCode(errors);
            }

            // `project` deploys like `compile app.spyproj` (#2313 cell 0, R-HF c): the output runs
            // in place, as `dotnet build`'s does. The project result does not expose per-module used
            // assemblies, so — as there — every default reference seeds the closure, plus every user
            // reference.
            if (result.OutputAssemblyPath != null && !noDeps)
            {
                var outputDir = Path.GetDirectoryName(result.OutputAssemblyPath) ?? Directory.GetCurrentDirectory();
                RuntimeDependencyHelper.CopyRuntimeDependencies(
                    outputDir, new HashSet<string>(defaultReferences, StringComparer.OrdinalIgnoreCase), userReferences);
            }

            Console.WriteLine("Build succeeded.");
            Console.WriteLine($"Output: {result.OutputAssemblyPath}");

            CliHelpers.OutputVerboseTimingSummary(result.Metrics, logger);
            CliHelpers.OutputProjectMetrics(result.Metrics, metricsFormat, metricsOutput);
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

    /// <summary>
    /// Compile a <c>.spyproj</c> for the keep-alive server (D2, #1049), writing progress/diagnostics
    /// to the (captured) console and surfacing the per-phase <paramref name="metrics"/> the server
    /// ships back so warm timings are reportable. A lean sibling of <see cref="CompileProject"/> —
    /// no clean/emit-cs side effects — kept here so it stays in step with the option-building the
    /// interactive path uses.
    /// </summary>
    internal static int CompileProjectForServer(
        FileInfo projectFile,
        string configuration,
        bool incremental,
        ICompilerLogger logger,
        out ProjectCompilationMetrics? metrics)
    {
        metrics = null;
        try
        {
            var projectConfig = ProjectFileParser.Load(projectFile.FullName, configuration);

            if (!RestorePackages(projectConfig, logger))
            {
                return CliHelpers.ExitCompileError;
            }

            Console.WriteLine($"Project: {projectConfig.RootNamespace}");
            Console.WriteLine($"Configuration: {projectConfig.Configuration}");
            Console.WriteLine($"Source files: {projectConfig.SourceFiles.Count}");
            if (incremental)
            {
                Console.WriteLine("Mode: Incremental");
            }
            Console.WriteLine();

            // The server path has no --warn-as-error/--nowarn/--max-errors/--enable-feature to
            // layer, so the project's own settings are the whole story.
            var compilerOptions = CompilerOptionsFactory.ForProject(
                projectConfig,
                defaultReferences: CliHelpers.GetDefaultReferences(),
                incremental: incremental);

            DefaultReferences.ApplyTo(projectConfig, CliHelpers.GetDefaultReferences());

            var compiler = new Sharpy.Compiler.Compiler(compilerOptions, logger);
            var result = compiler.CompileProject(projectConfig);
            metrics = result.Metrics;

            var projectWarnings = result.Diagnostics.GetWarnings();
            if (projectWarnings.Count > 0)
            {
                CliHelpers.RenderDiagnosticsFromFiles(projectWarnings, Console.Out, projectConfig.EntryPoint);
            }

            if (!result.Success)
            {
                Console.Error.WriteLine("Build FAILED.");
                Console.Error.WriteLine();
                var errors = result.Diagnostics.GetErrors();
                CliHelpers.RenderDiagnosticsFromFiles(errors, Console.Error, projectConfig.EntryPoint);
                return CliHelpers.MapFailureExitCode(errors);
            }

            Console.WriteLine("Build succeeded.");
            Console.WriteLine($"Output: {result.OutputAssemblyPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return CliHelpers.ExitInternalError;
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

            // Each unit mirrors its path relative to the project directory (or to the pinned
            // <SourceRoot>, when the .spyproj sets one) under the output directory, so two
            // units sharing a file stem in different directories land at distinct paths; a unit
            // mapping to an already-written path is refused loudly, never written over the first
            // (#2060). CliHelpers.WriteMirroredCSharp is the one writer every command shares (#2159).
            foreach (var (_, outputPath) in CliHelpers.WriteMirroredCSharp(outputDir.FullName, generatedFiles))
            {
                Console.WriteLine($"  Saved: {Path.GetRelativePath(outputDir.FullName, outputPath)}");
            }

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Could not save generated C# code: {ex.Message}");
        }
    }
}
