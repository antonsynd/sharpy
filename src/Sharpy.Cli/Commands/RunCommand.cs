using System.CommandLine;
using Sharpy.Cli.Services;
using Sharpy.Compiler;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

namespace Sharpy.Cli.Commands;

internal static class RunCommand
{
    /// <summary>
    /// Configures the <c>run</c> command and returns it, so the caller can tell a <c>run</c>
    /// invocation from any other when deciding what to do with a program argument vector (#1215).
    /// </summary>
    /// <param name="programArguments">
    /// The tokens that followed a bare <c>--</c> on the command line, already split off by
    /// <see cref="CliHelpers.SplitAtDoubleDash"/> so the parser never sees them.
    /// </param>
    internal static Command Configure(RootCommand root, GlobalOptions globals, IReadOnlyList<string>? programArguments = null)
    {
        var afterDoubleDash = programArguments ?? Array.Empty<string>();
        var command = new Command(
            "run",
            "Compile and execute a Sharpy source file (arguments after a bare '--' are passed to it)");

        var inputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file to run" };
        var outputOpt = new Option<FileInfo?>("--output") { Description = "Output file path (temporary if not specified)" };
        outputOpt.Aliases.Add("-o");
        // One value per occurrence — repeat the flag to collect more (#1179, #1215).
        var refOpt = new Option<string[]>("--reference") { Description = "Add a .NET assembly reference (repeatable)" };
        refOpt.Aliases.Add("-r");
        var projRefOpt = new Option<string[]>("--project-reference") { Description = "Add a .NET project reference (repeatable)" };
        projRefOpt.Aliases.Add("-p");
        var modPathOpt = new Option<string[]>("--module-path") { Description = "Additional path to search for modules (repeatable)" };
        modPathOpt.Aliases.Add("-m");
        // Deprecated in favour of `--` (#1215). Non-greedy like every other option, which means the
        // multi-token spelling `--args a b c` stops working — it was only ever held together by
        // AllowMultipleArgumentsPerToken, the same greediness that swallowed positional input paths.
        var argsOpt = new Option<string[]>("--args")
        {
            Description = "Deprecated: pass program arguments after a bare '--' instead. "
                + "Takes one value per occurrence (repeatable), so '--args a b c' no longer works — "
                + "spell it '-- a b c', or '--args a --args b --args c'."
        };
        var namespaceOpt = new Option<string?>("--namespace") { Description = "Wrap generated code in a namespace declaration" };
        namespaceOpt.Aliases.Add("-n");
        var selfContainedOpt = new Option<bool>("--self-contained") { Description = "Publish as a self-contained executable (no .NET runtime required)" };
        var serverOpt = new Option<string?>("--server") { Description = "Compile via a keep-alive 'sharpyc server' on the given pipe (default pipe if no name); falls back to in-process if none is running", Arity = ArgumentArity.ZeroOrOne };

        command.Arguments.Add(inputArg);
        command.Options.Add(outputOpt);
        command.Options.Add(refOpt);
        command.Options.Add(projRefOpt);
        command.Options.Add(modPathOpt);
        command.Options.Add(argsOpt);
        command.Options.Add(namespaceOpt);
        command.Options.Add(selfContainedOpt);
        command.Options.Add(serverOpt);

        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var output = parseResult.GetValue(outputOpt);
            var reference = parseResult.GetValue(refOpt) ?? Array.Empty<string>();
            var projectReference = parseResult.GetValue(projRefOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(modPathOpt) ?? Array.Empty<string>();
            var progArgs = CombineProgramArguments(parseResult.GetValue(argsOpt), afterDoubleDash);
            var namespaceName = parseResult.GetValue(namespaceOpt);
            var selfContained = parseResult.GetValue(selfContainedOpt);
            var logLevel = globals.ResolveLogLevel(parseResult);
            CliHelpers.ShowDiagnosticProvenance = parseResult.GetValue(globals.Verbose);
            var logFile = parseResult.GetValue(globals.LogFile);
            var metricsFormat = parseResult.GetValue(globals.MetricsFormat);
            var metricsOutput = parseResult.GetValue(globals.MetricsOutput);
            var warnAsError = parseResult.GetValue(globals.WarnAsError);
            var nowarn = parseResult.GetValue(globals.Nowarn);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);
            var features = parseResult.GetValue(globals.EnableFeature);
            var serverPipe = parseResult.GetResult(serverOpt) is not null
                ? parseResult.GetValue(serverOpt) ?? CompileServerProtocol.DefaultPipeName
                : null;

            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            return HandleRunCommand(input, output, reference, projectReference, modulePath, progArgs, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors, selfContained, features, serverPipe, namespaceName);
        });

        root.Subcommands.Add(command);
        // Display only: the generated help describes the option grammar, which the `--` separator
        // deliberately sits outside of, so `run --help` has to be told about it (#1231).
        RunHelpAction.Install(root, command);
        return command;
    }

    /// <summary>
    /// The program's argument vector: values given to the deprecated <c>--args</c> option first, in
    /// the order they were written, then everything after the <c>--</c> separator (#1215). Both
    /// channels are honoured so a script that has not migrated yet keeps working alongside <c>--</c>.
    /// </summary>
    internal static string[] CombineProgramArguments(string[]? argsOption, IReadOnlyList<string> afterDoubleDash)
    {
        var fromOption = argsOption ?? Array.Empty<string>();
        if (afterDoubleDash.Count == 0)
        {
            return fromOption;
        }

        var combined = new string[fromOption.Length + afterDoubleDash.Count];
        fromOption.CopyTo(combined, 0);
        for (var i = 0; i < afterDoubleDash.Count; i++)
        {
            combined[fromOption.Length + i] = afterDoubleDash[i];
        }

        return combined;
    }

    static int HandleRunCommand(
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
        bool selfContained = false,
        string[]? features = null,
        string? serverPipe = null,
        string? namespaceName = null)
    {
        if (!CliHelpers.ValidateInputFile(inputFile))
        {
            return 1;
        }

        if (!CliHelpers.ValidateNamespaceOption(namespaceName))
        {
            return 1;
        }

        var outputPath = output?.FullName;
        string? tempRunDir = null;

        if (outputPath == null)
        {
            tempRunDir = CreateTemporaryRunDirectory();
            var inputFileName = Path.GetFileNameWithoutExtension(inputFile.Name);
            // The assembly name still carries a GUID even inside a private directory: a
            // --self-contained publish writes a project whose own AssemblyName is the input file's
            // stem and which references this assembly by file name, so the two must differ.
            outputPath = Path.Combine(tempRunDir, $"{inputFileName}_{Guid.NewGuid():N}.exe");
        }

        try
        {
            // Obtain the compiled binary + its runtime-dependency set either from a keep-alive
            // server (--server) or by compiling in-process. A null result from the server means no
            // server answered → fall back so run always works (#1049).
            IReadOnlySet<string> usedAssemblyPaths;
            if (serverPipe != null
                && TryServerCompileForRun(serverPipe, inputFile, outputPath, references, projectReferences, modulePaths, warnAsError, nowarn, maxErrors, features, namespaceName, out var serverExit, out var serverUsedPaths))
            {
                if (serverExit != 0)
                {
                    return serverExit;
                }

                usedAssemblyPaths = serverUsedPaths;
            }
            else
            {
                var compileResult = BuildCommand.CompileToBinary(inputFile, "exe", new FileInfo(outputPath), references, projectReferences, modulePaths, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors, features: features, namespaceName: namespaceName);
                if (compileResult == null)
                {
                    return CliHelpers.LastFailureExitCode;
                }

                usedAssemblyPaths = compileResult.UsedAssemblyPaths;
            }

            var outputDir = Path.GetDirectoryName(outputPath)!;
            RuntimeDependencyHelper.CopyRuntimeDependencies(outputDir, usedAssemblyPaths);

            if (selfContained)
            {
                return HandleSelfContainedRun(inputFile, outputPath, args, usedAssemblyPaths);
            }

            Console.WriteLine();
            Console.WriteLine("=== Running Program ===");
            Console.WriteLine();

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { outputPath },
                UseShellExecute = false
            };

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit();
                return process.ExitCode;
            }

            return 0;
        }
        finally
        {
            // In a finally, not after the wait: a compile that fails returns from the middle of the
            // try, and every such path used to leave its staged output behind.
            CleanupTemporaryRunDirectory(tempRunDir);
        }
    }

    /// <summary>
    /// Creates a fresh private directory under the temp root for a <c>run</c> that was given no
    /// <c>--output</c>. The executable is never staged in the temp root itself: <c>run</c> copies
    /// the whole runtime closure (Sharpy.Core.dll, the used stdlib assemblies, their transitive
    /// managed and native dependencies) beside it under fixed file names and removes them
    /// afterwards, so two concurrent runs sharing that directory overwrite each other's mapped
    /// assemblies and delete each other's dependencies (#1419).
    /// </summary>
    internal static string CreateTemporaryRunDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy_run_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Removes the private directory a temporary <c>run</c> staged its output in, contents and all.
    /// Removing the directory rather than the copied file names is what keeps cleanup from reaching
    /// a concurrent run's files (#1419). Best-effort: a run that cannot clean up still returns its
    /// program's exit code.
    /// </summary>
    static void CleanupTemporaryRunDirectory(string? tempRunDir)
    {
        if (tempRunDir == null)
        {
            return;
        }

        try
        {
            Directory.Delete(tempRunDir, recursive: true);
        }
        catch
        {
        }
    }

    static int HandleSelfContainedRun(
        FileInfo inputFile,
        string compiledExePath,
        string[] args,
        IReadOnlySet<string> usedAssemblyPaths)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(inputFile.Name);
        // The Main() call must name the class the emitter actually generated, not the raw stem: a
        // self-contained publish is always an entry-point file, so ComputeModuleClassName (the same
        // helper CodeGenInfoComputer uses) gives ScProbe for sc_probe.spy and Program for main.spy —
        // never the sc_probe/main the raw stem would emit, which failed EVERY publish with CS0103
        // (#1483).
        var entryTypeName = NameMangler.ComputeModuleClassName(inputFile.FullName) ?? assemblyName;
        var publishDir = Path.Combine(Path.GetTempPath(), $"sharpy_publish_{Guid.NewGuid():N}");

        // No cleanup of the compiled executable here: the caller staged it in a directory of its own
        // and removes that directory in a finally, whichever way this returns (#1419).
        var publishedExe = SelfContainedPublisher.Publish(compiledExePath, assemblyName, entryTypeName, publishDir, usedAssemblyPaths);
        if (publishedExe == null)
        {
            return 1;
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
            return runProcess.ExitCode;
        }

        return 0;
    }

    /// <summary>
    /// Compile the entry file through a keep-alive server (#1049), producing the binary at
    /// <paramref name="outputPath"/> and returning the program's runtime-dependency set so the
    /// caller can copy deps and execute. Returns <c>true</c> when the server answered (its captured
    /// output is echoed and <paramref name="exitCode"/>/<paramref name="usedAssemblyPaths"/> are
    /// populated); returns <c>false</c> when no usable server responded, so the caller falls back to
    /// an in-process compile.
    /// </summary>
    static bool TryServerCompileForRun(
        string pipeName,
        FileInfo inputFile,
        string outputPath,
        string[] references,
        string[] projectReferences,
        string[] modulePaths,
        bool warnAsError,
        string? nowarn,
        int? maxErrors,
        string[]? features,
        string? namespaceName,
        out int exitCode,
        out IReadOnlySet<string> usedAssemblyPaths)
    {
        exitCode = 0;
        usedAssemblyPaths = new HashSet<string>();

        var request = new CompileServerRequest
        {
            Command = CompileServerProtocol.CommandCompile,
            Input = inputFile.FullName,
            Output = outputPath,
            OutputType = "exe",
            References = references,
            ProjectReferences = projectReferences,
            ModulePaths = modulePaths,
            Features = features ?? Array.Empty<string>(),
            Namespace = namespaceName,
            Configuration = "Debug",
            WarnAsError = warnAsError,
            Nowarn = nowarn,
            MaxErrors = maxErrors,
            WorkingDirectory = Directory.GetCurrentDirectory(),
        };

        if (!CompileServerClient.TrySend(pipeName, request, out var response, out var reason)
            || response == null
            || response.Error != null)
        {
            Console.Error.WriteLine(
                $"sharpyc: no compile server on pipe '{pipeName}' ({response?.Error ?? reason}); compiling in-process.");
            return false;
        }

        Console.Out.Write(response.Stdout);
        Console.Error.Write(response.Stderr);
        exitCode = response.ExitCode;
        usedAssemblyPaths = new HashSet<string>(response.UsedAssemblyPaths);
        return true;
    }
}
