using System.CommandLine;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

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
    /// <summary>The two input kinds <c>compile</c> accepts; the option table has one column per kind.</summary>
    internal enum InputKind
    {
        /// <summary>A single <c>.spy</c> source file (compiled as a synthetic project-of-one-file).</summary>
        SpyFile,

        /// <summary>A <c>.spyproj</c> project file.</summary>
        SpyProject,
    }

    /// <summary>What <c>compile</c> does with an option for one input kind (#2173).</summary>
    internal enum OptionEffect
    {
        /// <summary>The option reaches the compile and changes its result.</summary>
        Honoured,

        /// <summary>
        /// Giving the option is an error for this input kind: the command prints why and exits 1
        /// before compiling anything (<see cref="RefuseInapplicableOptions"/>).
        /// </summary>
        Refused,

        /// <summary>
        /// The option has no effect for this input kind, and its <c>--help</c> description says so
        /// (the suffix is appended from this table by <see cref="Configure"/>).
        /// </summary>
        Ignored,
    }

    /// <summary>One row of the option-to-effect table: the effect per input kind and why.</summary>
    internal sealed record OptionEffectRow(OptionEffect SpyFile, OptionEffect SpyProject, string Reason)
    {
        internal OptionEffect For(InputKind kind) => kind == InputKind.SpyFile ? SpyFile : SpyProject;
    }

    /// <summary>
    /// The option-to-effect table for <c>compile</c>, keyed by option name: every option the command
    /// accepts — its own and the recursive global ones — with what it does for a <c>.spy</c> and a
    /// <c>.spyproj</c> input. A parsed option is honoured, refused for that input kind with a message,
    /// or documented as ignored; it is never silently dropped (#2173, after #2159 found <c>-o</c>
    /// parsed and dropped for a project). <c>CompileOptionEffectTableTests</c> fails when an option is
    /// declared without a row or a row names no declared option.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, OptionEffectRow> OptionEffects =
        new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
        {
            ["--output"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "the assembly path; for a project it overrides bin/{Configuration}/{TFM} (#2159)"),
            ["--configuration"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "the build configuration (default Release)"),
            ["--type"] = new(OptionEffect.Honoured, OptionEffect.Ignored,
                "a project's output type is its <OutputType>"),
            ["--reference"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "for a project, added to the .spyproj's <Reference> items; a reference that cannot be found is SPY0305"),
            ["--project-reference"] = new(OptionEffect.Refused, OptionEffect.Refused,
                CliHelpers.ProjectReferenceUnsupportedReason),
            ["--module-path"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "for a project, added to the .spyproj's <ModulePath> items; a directory that does not exist is an error"),
            ["--self-contained"] = new(OptionEffect.Honoured, OptionEffect.Refused,
                "self-contained publishing of a .spyproj is not supported yet"),
            ["--no-deps"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "runtime dependencies are not copied beside the output"),
            ["--incremental"] = new(OptionEffect.Ignored, OptionEffect.Honoured,
                "the incremental cache lives in a project's obj/"),
            ["--clean"] = new(OptionEffect.Ignored, OptionEffect.Honoured,
                "only a project has bin/ and obj/ directories to delete"),
            ["--emit-csharp"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "generated C# is mirrored beside the output (#2159)"),

            // Recursive global options (GlobalOptions): every one reaches both paths.
            ["--log-level"] = new(OptionEffect.Honoured, OptionEffect.Honoured, "the compiler logger's level"),
            ["--log-file"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "the compiler log goes to this file (at the --log-level/--verbose level)"),
            ["--metrics-format"] = new(OptionEffect.Honoured, OptionEffect.Honoured, "compilation metrics are printed"),
            ["--metrics-output"] = new(OptionEffect.Honoured, OptionEffect.Honoured, "compilation metrics are written to this file"),
            ["--warn-as-error"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "warnings fail the compile (OR-ed with a project's <WarningsAsErrors>)"),
            ["--nowarn"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "the codes are suppressed (unioned with a project's <NoWarn>)"),
            ["--max-errors"] = new(OptionEffect.Honoured, OptionEffect.Honoured, "error reporting stops after this many"),
            ["--verbose"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "raises the log level to Info and shows diagnostic provenance"),
            ["--enable-feature"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "the experimental feature is enabled (unioned with a project's <Features>)"),
            ["--help"] = new(OptionEffect.Honoured, OptionEffect.Honoured, "prints help instead of compiling"),
        };

    /// <summary>
    /// The <c>--help</c> suffix that documents a row's non-honoured cells, e.g.
    /// <c>(ignored for .spyproj)</c>. Empty when the option is honoured for both input kinds.
    /// </summary>
    internal static string DescriptionSuffix(OptionEffectRow row)
    {
        var notes = new List<string>();
        if (row.SpyFile == row.SpyProject && row.SpyFile != OptionEffect.Honoured)
        {
            notes.Add($"{EffectWord(row.SpyFile)} for .spy and .spyproj: {row.Reason}");
        }
        else
        {
            if (row.SpyFile != OptionEffect.Honoured)
                notes.Add($"{EffectWord(row.SpyFile)} for .spy: {row.Reason}");
            if (row.SpyProject != OptionEffect.Honoured)
                notes.Add($"{EffectWord(row.SpyProject)} for .spyproj: {row.Reason}");
        }

        return notes.Count == 0 ? string.Empty : $" ({string.Join("; ", notes)})";

        static string EffectWord(OptionEffect effect) => effect == OptionEffect.Refused ? "refused" : "ignored";
    }

    /// <summary>
    /// Refuses, before anything is compiled or written, every option the user actually gave whose
    /// table cell for <paramref name="kind"/> is <see cref="OptionEffect.Refused"/>. Returns false
    /// (after printing one error per refused option) when the command must exit.
    /// </summary>
    internal static bool RefuseInapplicableOptions(System.CommandLine.ParseResult parseResult, IEnumerable<Option> options, InputKind kind)
    {
        var refused = false;
        foreach (var option in options)
        {
            if (!OptionEffects.TryGetValue(option.Name, out var row) || row.For(kind) != OptionEffect.Refused)
                continue;
            if (parseResult.GetResult(option) is not { Implicit: false })
                continue;

            var inputKind = kind == InputKind.SpyFile ? "a .spy file" : "a .spyproj project";
            Console.Error.WriteLine($"Error: {option.Name} is not supported when compiling {inputKind}: {row.Reason}.");
            refused = true;
        }

        return !refused;
    }

    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("compile", "Compile Sharpy source to a standalone .dll or .exe");

        var inputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file (.spy) or project file (.spyproj)" };
        var outputOpt = new Option<FileInfo?>("--output") { Description = "Output file path" };
        outputOpt.Aliases.Add("-o");
        var configOpt = new Option<string?>("--configuration") { Description = "Build configuration (Debug or Release)" };
        configOpt.Aliases.Add("-c");
        var typeOpt = new Option<string?>("--type") { Description = "Output type: 'exe' or 'library'" };
        typeOpt.Aliases.Add("-t");
        // One value per occurrence — repeat the flag to collect more (#1179, #1215).
        var refOpt = new Option<string[]>("--reference") { Description = "Add a .NET assembly reference (repeatable)" };
        refOpt.Aliases.Add("-r");
        var projRefOpt = new Option<string[]>("--project-reference") { Description = "Add a .NET project reference (repeatable)" };
        projRefOpt.Aliases.Add("-p");
        var modPathOpt = new Option<string[]>("--module-path") { Description = "Additional path to search for modules (repeatable)" };
        modPathOpt.Aliases.Add("-m");
        var selfContainedOpt = new Option<bool>("--self-contained") { Description = "Produce a self-contained executable (no .NET runtime required)" };
        var noDepsOpt = new Option<bool>("--no-deps") { Description = "Skip copying runtime dependencies alongside the output" };
        var incrementalOpt = new Option<bool>("--incremental") { Description = "Enable incremental compilation" };
        var cleanOpt = new Option<bool>("--clean") { Description = "Delete bin/ and obj/ before building" };
        var emitCSharpOpt = new Option<bool>("--emit-csharp") { Description = "Write generated C# source files alongside the output" };

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
        command.Options.Add(emitCSharpOpt);

        // The table documents its own non-honoured cells in --help, so "ignored"/"refused" cannot
        // drift from what the command does. (Global options are honoured for both kinds; the table
        // test keeps it that way, since their descriptions are shared with every other command.)
        foreach (var option in command.Options)
        {
            if (OptionEffects.TryGetValue(option.Name, out var row))
                option.Description += DescriptionSuffix(row);
        }

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
            var emitCSharp = parseResult.GetValue(emitCSharpOpt);
            var logLevel = globals.ResolveLogLevel(parseResult);
            CliHelpers.ShowDiagnosticProvenance = parseResult.GetValue(globals.Verbose);
            var logFile = parseResult.GetValue(globals.LogFile);
            var metricsFormat = parseResult.GetValue(globals.MetricsFormat);
            var metricsOutput = parseResult.GetValue(globals.MetricsOutput);
            var warnAsError = parseResult.GetValue(globals.WarnAsError);
            var nowarn = parseResult.GetValue(globals.Nowarn);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);
            var features = parseResult.GetValue(globals.EnableFeature);

            var kind = input.Extension.Equals(".spyproj", StringComparison.OrdinalIgnoreCase)
                ? InputKind.SpyProject
                : InputKind.SpyFile;
            if (!RefuseInapplicableOptions(parseResult, command.Options, kind)
                || !CliHelpers.ValidateModulePaths(modulePath))
            {
                return CliHelpers.ExitCompileError;
            }

            var logger = CliHelpers.CreateLogger(logLevel, logFile);

            if (kind == InputKind.SpyProject)
            {
                return CompileProject(input, output, configuration, reference, modulePath, clean, incremental, noDeps, emitCSharp, logger, logLevel, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors, features);
            }

            return CompileSingleFile(input, output, configuration, type, reference, projectReference, modulePath, noDeps, selfContained, emitCSharp, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors, features);
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
        bool emitCSharp,
        ICompilerLogger logger,
        string? metricsFormat,
        FileInfo? metricsOutput,
        bool warnAsError,
        string? nowarn,
        int? maxErrors,
        string[]? features)
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
            logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors, configuration, features);

        if (compileResult == null)
        {
            return CliHelpers.LastFailureExitCode;
        }

        if (emitCSharp)
        {
            // Single-file units are keyed by absolute source path; they mirror their path relative
            // to the entry file's directory, the root that names the imported modules (#2159).
            EmitGeneratedCSharp(compileResult.GeneratedCSharpFiles, outputDir, inputFile.DirectoryName);
        }

        if (selfContained)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(inputFile.Name);
            // The entry type is the entry module's members class in its namespace, as semantic
            // analysis recorded it (ScProbe.ScProbeModule for sc_probe.spy); the raw stem emitted
            // sc_probe.Main() — CS0103, every publish failed (#1483, #2013, #2039). assemblyName stays
            // raw (file names).
            var entryTypeName = compileResult.EntryTypeName
                ?? throw new InvalidOperationException("The compiler recorded no entry type for a successful compile.");
            var publishedExe = SelfContainedPublisher.Publish(outputPath, assemblyName, entryTypeName, outputDir, compileResult.UsedAssemblyPaths);
            if (publishedExe == null)
            {
                return 1;
            }
            ReportOutput(publishedExe);
            return 0;
        }

        if (!noDeps)
        {
            RuntimeDependencyHelper.CopyRuntimeDependencies(outputDir, compileResult.UsedAssemblyPaths);
        }

        ReportOutput(outputPath);
        return 0;
    }

    static int CompileProject(
        FileInfo projectFile,
        FileInfo? output,
        string configuration,
        string[] references,
        string[] modulePaths,
        bool clean,
        bool incremental,
        bool noDeps,
        bool emitCSharp,
        ICompilerLogger logger,
        CompilerLogLevel logLevel,
        string? metricsFormat,
        FileInfo? metricsOutput,
        bool warnAsError,
        string? nowarn,
        int? maxErrors,
        string[]? features)
    {
        try
        {
            var projectConfig = ProjectFileParser.Load(projectFile.FullName, configuration);
            // `-o` names the assembly path for a project exactly as it does for a single file;
            // without this the option was parsed and silently dropped, and the assembly (and any
            // --emit-csharp output beside it) went to bin/{Config}/{TFM} instead (#2159).
            if (output != null)
            {
                projectConfig.OutputAssemblyPathOverride = output.FullName;
            }

            // `-r`/`-m` were parsed and silently dropped for a project (#2173). They join the
            // project's own <ModulePath>/<Reference> items, so they mean what those items mean. Paths
            // are resolved against the working directory, as for a single file; each reference is
            // spelled as the file the module registry resolves it to (CliHelpers.ResolveReferences),
            // so semantic analysis and the Roslyn step see the same assembly.
            foreach (var modulePath in modulePaths)
            {
                var full = Path.GetFullPath(modulePath);
                if (!projectConfig.ModulePaths.Contains(full))
                    projectConfig.ModulePaths.Add(full);
            }

            // A `-r` that resolves nowhere is an error, as it is for a single file (where the module
            // registry's SPY0305 fails the compile). The project path does not surface registry load
            // failures — a .spyproj's own unresolvable <Reference> is still accepted, recorded
            // separately — so the command-line references are checked here, by the registry's own
            // resolution over the merged module paths, before anything is compiled.
            var unresolved = new List<CompilerDiagnostic>();
            foreach (var reference in references)
            {
                var resolved = ModuleRegistry.ResolveAssemblyPath(reference, projectConfig.ModulePaths);
                if (resolved == null)
                {
                    unresolved.Add(new CompilerDiagnostic(
                        $"Assembly not found: {reference}",
                        CompilerDiagnosticSeverity.Error,
                        Code: DiagnosticCodes.Semantic.AssemblyNotFound,
                        Phase: CompilerPhase.ImportResolution));
                }
                else if (!projectConfig.References.Contains(resolved))
                {
                    projectConfig.References.Add(resolved);
                }
            }

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

            Console.WriteLine($"Project: {projectConfig.RootNamespace}");
            Console.WriteLine($"Configuration: {projectConfig.Configuration}");
            Console.WriteLine($"Output: {projectConfig.OutputType}");
            Console.WriteLine($"Source files: {projectConfig.SourceFiles.Count}");
            if (incremental)
            {
                Console.WriteLine("Mode: Incremental");
            }
            Console.WriteLine();

            var defaultReferences = CliHelpers.GetDefaultReferences();

            var compilerOptions = CompilerOptionsFactory.ForProject(
                projectConfig,
                defaultReferences: defaultReferences,
                warningsAsErrors: warnAsError,
                additionalSuppressedWarnings: CliHelpers.ParseNowarnCodes(nowarn),
                maxErrors: maxErrors ?? 0,
                incremental: incremental,
                features: CliHelpers.ParseFeatures(features));

            // Inject CLI default references into ProjectConfig so AssemblyCompiler can resolve
            // types from Sharpy.Core/Stdlib during Roslyn compilation.
            foreach (var defaultRef in defaultReferences)
            {
                if (!projectConfig.References.Contains(defaultRef))
                    projectConfig.References.Add(defaultRef);
            }

            var compiler = new Sharpy.Compiler.Compiler(compilerOptions, logger);
            var result = compiler.CompileProject(projectConfig);

            var projectWarnings = result.Diagnostics.GetWarnings();
            if (projectWarnings.Count > 0)
            {
                CliHelpers.RenderDiagnosticsFromFiles(projectWarnings, Console.Out, projectConfig.EntryPoint);
            }

            if (!result.Success)
            {
                Console.Error.WriteLine("Compilation FAILED.");
                Console.Error.WriteLine();
                var errors = result.Diagnostics.GetErrors();
                CliHelpers.RenderDiagnosticsFromFiles(errors, Console.Error, projectConfig.EntryPoint);
                return CliHelpers.MapFailureExitCode(errors);
            }

            var outputPath = result.OutputAssemblyPath;
            if (outputPath != null && !noDeps)
            {
                var outputDir = Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory();
                // The project result does not expose per-module used assemblies, so
                // conservatively copy every referenced runtime assembly.
                RuntimeDependencyHelper.CopyRuntimeDependencies(outputDir, new HashSet<string>(defaultReferences, StringComparer.OrdinalIgnoreCase));
            }

            // --self-contained is refused for a project up front, by the option table, before
            // anything is compiled; it used to be refused here, after the assembly was written.
            CliHelpers.OutputVerboseTimingSummary(result.Metrics, logger);
            CliHelpers.OutputProjectMetrics(result.Metrics, metricsFormat, metricsOutput);

            if (emitCSharp && result.GeneratedCSharpFiles.Count > 0)
            {
                var csOutputDir = outputPath != null
                    ? Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory()
                    : Directory.GetCurrentDirectory();
                EmitGeneratedCSharp(result.GeneratedCSharpFiles, csOutputDir, projectConfig.ProjectDirectory);
            }

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

    /// <summary>
    /// Writes each generated unit beside the output assembly at the path mirroring its source
    /// path relative to <paramref name="sourceRoot"/> (the project directory, or the entry file's
    /// directory for a single file), so <c>lib.spy</c> and <c>pkg/lib.spy</c> land at
    /// <c>lib.cs</c> and <c>pkg/lib.cs</c>. Writing by file stem sent both to <c>lib.cs</c> and the
    /// second silently overwrote the first (#2159, the #2060 contract).
    /// </summary>
    static void EmitGeneratedCSharp(IReadOnlyDictionary<string, string> generatedFiles, string outputDir, string? sourceRoot)
    {
        if (generatedFiles.Count == 0)
        {
            return;
        }

        var written = CliHelpers.WriteMirroredCSharp(
            outputDir, generatedFiles, sourceRoot, CliHelpers.StripLineDirectives);
        Console.WriteLine($"Generated {written.Count} C# file(s) in: {outputDir}");
        foreach (var (_, path) in written)
        {
            Console.WriteLine($"  {Path.GetRelativePath(outputDir, path)}");
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
