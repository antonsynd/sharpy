extern alias SharpyRT;
using System.CommandLine;
using System.Runtime.InteropServices;
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
            HandleRunCommand(input, output, reference, projectReference, modulePath, progArgs, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors, selfContained);
        });

        root.Subcommands.Add(command);
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
        CliHelpers.ValidateInputFile(inputFile);

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
            var compileResult = BuildCommand.CompileToBinary(inputFile, "exe", new FileInfo(outputPath), references, projectReferences, modulePaths, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors);

            var sharpyCoreAssembly = typeof(SharpyRT::Sharpy.Builtins).Assembly;
            var sharpyCorePath = sharpyCoreAssembly.Location;
            var outputDir = Path.GetDirectoryName(outputPath)!;
            var sharpyCoreDestPath = Path.Combine(outputDir, "Sharpy.Core.dll");
            File.Copy(sharpyCorePath, sharpyCoreDestPath, overwrite: true);

            var cliDir = Path.GetDirectoryName(sharpyCorePath)!;
            CopyUsedStdlibDependencies(cliDir, outputDir, compileResult.UsedAssemblyPaths);

            if (selfContained)
            {
                HandleSelfContainedRun(inputFile, outputPath, sharpyCorePath, args, isTempOutput, compileResult.UsedAssemblyPaths);
                return;
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
                        CleanupRuntimeDependencies(basePath);
                    }
                    catch
                    {
                    }
                }

                Environment.Exit(process.ExitCode);
            }
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
                    CleanupRuntimeDependencies(basePath);
                }
                catch
                {
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
        bool isTempOutput,
        IReadOnlySet<string> usedAssemblyPaths)
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var assemblyName = Path.GetFileNameWithoutExtension(inputFile.Name);
        var publishDir = Path.Combine(Path.GetTempPath(), $"sharpy_publish_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(publishDir);

            var tempProjDir = Path.Combine(Path.GetTempPath(), $"sharpy_proj_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempProjDir);
            var csprojPath = Path.Combine(tempProjDir, $"{assemblyName}.csproj");

            var cliDir = Path.GetDirectoryName(sharpyCorePath)!;
            var stdlibRefs = new System.Text.StringBuilder();
            foreach (var assemblyPath in usedAssemblyPaths)
            {
                var fileName = Path.GetFileName(assemblyPath);
                if (fileName.Equals("Sharpy.Core.dll", StringComparison.OrdinalIgnoreCase))
                    continue;
                var fullPath = Path.Combine(cliDir, fileName);
                if (File.Exists(fullPath))
                {
                    var includeName = Path.GetFileNameWithoutExtension(fileName);
                    stdlibRefs.AppendLine($@"    <Reference Include=""{includeName}"">
      <HintPath>{fullPath}</HintPath>
    </Reference>");
                }
            }
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
{stdlibRefs}  </ItemGroup>
</Project>";

            File.WriteAllText(csprojPath, csprojContent);
            File.WriteAllText(
                Path.Combine(tempProjDir, "Program.cs"),
                $"// Auto-generated entry point\n{assemblyName}.Main();\n");

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

            try
            { Directory.Delete(tempProjDir, recursive: true); }
            catch { }
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
                    CleanupRuntimeDependencies(basePath);
                }
                catch { }
            }
        }
    }

    private static readonly string[] NumpyNuGetDeps = new[] { "MathNet.Numerics.dll" };
    private static readonly string[] Sqlite3NuGetDeps = new[]
    {
        "Microsoft.Data.Sqlite.dll",
        "SQLitePCLRaw.batteries_v2.dll",
        "SQLitePCLRaw.core.dll",
        "SQLitePCLRaw.provider.e_sqlite3.dll",
    };

    private static readonly Dictionary<string, string[]> PerModuleNuGetDeps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sharpy.Stdlib.Numpy.dll"] = NumpyNuGetDeps,
        ["Sharpy.Stdlib.Sqlite3.dll"] = Sqlite3NuGetDeps,
        ["Sharpy.Stdlib.dll"] = NumpyNuGetDeps.Concat(Sqlite3NuGetDeps).ToArray(),
    };

    static void CopyUsedStdlibDependencies(string cliDir, string outputDir, IReadOnlySet<string> usedAssemblyPaths)
    {
        var copiedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sharpy.Core.dll" };
        foreach (var assemblyPath in usedAssemblyPaths)
        {
            var fileName = Path.GetFileName(assemblyPath);
            if (fileName.Equals("Sharpy.Core.dll", StringComparison.OrdinalIgnoreCase))
                continue;

            CopyRuntimeDependency(cliDir, outputDir, fileName);
            copiedFiles.Add(fileName);

            if (PerModuleNuGetDeps.TryGetValue(fileName, out var nugetDeps))
            {
                foreach (var dep in nugetDeps)
                {
                    CopyRuntimeDependency(cliDir, outputDir, dep);
                    copiedFiles.Add(dep);
                }
            }
        }

        _lastCopiedDependencies = copiedFiles;
    }

    [ThreadStatic]
    private static HashSet<string>? _lastCopiedDependencies;

    static void CopyRuntimeDependency(string sourceDir, string destDir, string fileName)
    {
        var src = Path.Combine(sourceDir, fileName);
        if (File.Exists(src))
            File.Copy(src, Path.Combine(destDir, fileName), overwrite: true);
    }

    static void CleanupRuntimeDependencies(string dir)
    {
        var deps = _lastCopiedDependencies;
        if (deps == null)
            return;
        foreach (var dep in deps)
        {
            var path = Path.Combine(dir, dep);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
