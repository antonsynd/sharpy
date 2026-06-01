using System.CommandLine;
using Sharpy.Compiler;
using Sharpy.Compiler.Logging;

namespace Sharpy.Cli.Commands;

internal static class RunCommand
{
    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("run", "Compile and execute a Sharpy source file");

        var inputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file to run" };
        var outputOpt = new Option<FileInfo?>("--output") { Description = "Output file path (temporary if not specified)" };
        outputOpt.Aliases.Add("-o");
        var refOpt = new Option<string[]>("--reference") { Description = "Add .NET assembly references", AllowMultipleArgumentsPerToken = true };
        refOpt.Aliases.Add("-r");
        var projRefOpt = new Option<string[]>("--project-reference") { Description = "Add .NET project references", AllowMultipleArgumentsPerToken = true };
        projRefOpt.Aliases.Add("-p");
        var modPathOpt = new Option<string[]>("--module-path") { Description = "Additional paths to search for modules", AllowMultipleArgumentsPerToken = true };
        modPathOpt.Aliases.Add("-m");
        var argsOpt = new Option<string[]>("--args") { Description = "Arguments to pass to the program", AllowMultipleArgumentsPerToken = true };
        var selfContainedOpt = new Option<bool>("--self-contained") { Description = "Publish as a self-contained executable (no .NET runtime required)" };

        command.Arguments.Add(inputArg);
        command.Options.Add(outputOpt);
        command.Options.Add(refOpt);
        command.Options.Add(projRefOpt);
        command.Options.Add(modPathOpt);
        command.Options.Add(argsOpt);
        command.Options.Add(selfContainedOpt);

        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var output = parseResult.GetValue(outputOpt);
            var reference = parseResult.GetValue(refOpt) ?? Array.Empty<string>();
            var projectReference = parseResult.GetValue(projRefOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(modPathOpt) ?? Array.Empty<string>();
            var progArgs = parseResult.GetValue(argsOpt) ?? Array.Empty<string>();
            var selfContained = parseResult.GetValue(selfContainedOpt);
            var logLevel = parseResult.GetValue(globals.LogLevel) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(globals.LogFile);
            var metricsFormat = parseResult.GetValue(globals.MetricsFormat);
            var metricsOutput = parseResult.GetValue(globals.MetricsOutput);
            var warnAsError = parseResult.GetValue(globals.WarnAsError);
            var nowarn = parseResult.GetValue(globals.Nowarn);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);

            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            return HandleRunCommand(input, output, reference, projectReference, modulePath, progArgs, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors, selfContained);
        });

        root.Subcommands.Add(command);
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
        bool selfContained = false)
    {
        if (!CliHelpers.ValidateInputFile(inputFile))
        {
            return 1;
        }

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

        IReadOnlySet<string>? copiedDeps = null;

        try
        {
            var compileResult = BuildCommand.CompileToBinary(inputFile, "exe", new FileInfo(outputPath), references, projectReferences, modulePaths, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors);
            if (compileResult == null)
            {
                return 1;
            }

            var outputDir = Path.GetDirectoryName(outputPath)!;
            copiedDeps = RuntimeDependencyHelper.CopyRuntimeDependencies(outputDir, compileResult.UsedAssemblyPaths);

            if (selfContained)
            {
                return HandleSelfContainedRun(inputFile, outputPath, args, isTempOutput, compileResult.UsedAssemblyPaths, copiedDeps);
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

                if (isTempOutput)
                {
                    try
                    {
                        var basePath = Path.GetDirectoryName(outputPath)!;
                        File.Delete(outputPath);
                        File.Delete(Path.Combine(basePath, tempBaseName + ".runtimeconfig.json"));
                        File.Delete(Path.Combine(basePath, tempBaseName + ".deps.json"));
                        File.Delete(Path.Combine(basePath, tempBaseName + ".pdb"));
                        CleanupRuntimeDependencies(basePath, copiedDeps);
                    }
                    catch
                    {
                    }
                }

                return process.ExitCode;
            }

            return 0;
        }
        catch (Exception)
        {
            if (isTempOutput && File.Exists(outputPath))
            {
                try
                {
                    var basePath = Path.GetDirectoryName(outputPath)!;
                    File.Delete(outputPath);
                    File.Delete(Path.Combine(basePath, tempBaseName + ".runtimeconfig.json"));
                    File.Delete(Path.Combine(basePath, tempBaseName + ".deps.json"));
                    File.Delete(Path.Combine(basePath, tempBaseName + ".pdb"));
                    CleanupRuntimeDependencies(basePath, copiedDeps);
                }
                catch
                {
                }
            }
            throw;
        }
    }

    static int HandleSelfContainedRun(
        FileInfo inputFile,
        string compiledExePath,
        string[] args,
        bool isTempOutput,
        IReadOnlySet<string> usedAssemblyPaths,
        IReadOnlySet<string>? copiedDeps)
    {
        var entryTypeName = Path.GetFileNameWithoutExtension(inputFile.Name);
        var publishDir = Path.Combine(Path.GetTempPath(), $"sharpy_publish_{Guid.NewGuid():N}");

        try
        {
            var publishedExe = SelfContainedPublisher.Publish(compiledExePath, entryTypeName, publishDir, usedAssemblyPaths);
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
        finally
        {
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
                    CleanupRuntimeDependencies(basePath, copiedDeps);
                }
                catch { }
            }
        }
    }

    static void CleanupRuntimeDependencies(string dir, IReadOnlySet<string>? copiedDeps)
        => RuntimeDependencyHelper.CleanupRuntimeDependencies(dir, copiedDeps);
}
