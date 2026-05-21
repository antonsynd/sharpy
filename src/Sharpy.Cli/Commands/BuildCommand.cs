extern alias SharpyRT;
using System.CommandLine;
using Sharpy.Compiler;
using Sharpy.Compiler.Logging;
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

        command.Arguments.Add(inputArg);
        command.Options.Add(typeOpt);
        command.Options.Add(outputOpt);
        command.Options.Add(refOpt);
        command.Options.Add(projRefOpt);
        command.Options.Add(modPathOpt);

        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var type = parseResult.GetValue(typeOpt) ?? "exe";
            var output = parseResult.GetValue(outputOpt);
            var reference = parseResult.GetValue(refOpt) ?? Array.Empty<string>();
            var projectReference = parseResult.GetValue(projRefOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(modPathOpt) ?? Array.Empty<string>();
            var logLevel = parseResult.GetValue(globals.LogLevel) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(globals.LogFile);
            var metricsFormat = parseResult.GetValue(globals.MetricsFormat);
            var metricsOutput = parseResult.GetValue(globals.MetricsOutput);
            var warnAsError = parseResult.GetValue(globals.WarnAsError);
            var nowarn = parseResult.GetValue(globals.Nowarn);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);

            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            CliHelpers.ValidateInputFile(input);
            CompileToBinary(input, type, output, reference, projectReference, modulePath, logger, metricsFormat, metricsOutput, warnAsError, nowarn, maxErrors);
        });

        root.Subcommands.Add(command);
    }

    internal static void CompileToBinary(
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
            var source = File.ReadAllText(inputFile.FullName);
            var sourceText = new SourceText(source, inputFile.FullName);

            var compilerOptions = new CompilerOptions
            {
                OutputType = outputType,
                References = references,
                ModulePaths = modulePaths,
                WarningsAsErrors = warnAsError,
                SuppressedWarnings = CliHelpers.ParseNowarnCodes(nowarn),
                MaxErrors = maxErrors ?? 0
            };

            var api = CliHelpers.CreateCompilerApi(logger);
            var result = api.Compile(source, compilerOptions, inputFile.FullName);

            if (!result.Success)
            {
                Console.Error.WriteLine("Compilation failed:");
                Console.Error.WriteLine();
                CliHelpers.RenderDiagnostics(result.Diagnostics.Where(d => d.IsError), sourceText, Console.Error);
                Environment.Exit(1);
            }

            var compilationWarnings = result.Diagnostics.Where(d => d.IsWarning).ToList();
            if (compilationWarnings.Count > 0)
            {
                CliHelpers.RenderDiagnostics(compilationWarnings, sourceText, Console.Out);
            }

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

            var csharpSources = new Dictionary<string, string>();
            foreach (var (sourcePath, csCode) in result.GeneratedCSharpFiles)
            {
                var csFileName = Path.ChangeExtension(sourcePath, ".cs");
                csharpSources[csFileName] = csCode;
            }

            if (csharpSources.Count == 0 && result.GeneratedCSharp != null)
            {
                csharpSources[Path.ChangeExtension(inputFile.FullName, ".cs")] = result.GeneratedCSharp;
            }

            var assemblyCompiler = new AssemblyCompiler(logger);
            var assemblyResult = assemblyCompiler.CompileToAssembly(csharpSources, projectConfig);

            if (!assemblyResult.Success)
            {
                Console.Error.WriteLine("Assembly compilation failed:");
                Console.Error.WriteLine();
                CliHelpers.RenderDiagnostics(assemblyResult.Diagnostics.GetErrors(), sourceText, Console.Error);
                Environment.Exit(1);
            }

            var assemblyWarnings = assemblyResult.Diagnostics.GetWarnings();
            if (assemblyWarnings.Count > 0)
            {
                CliHelpers.RenderDiagnostics(assemblyWarnings, sourceText, Console.Out);
            }

            Console.WriteLine($"Successfully compiled to: {assemblyResult.OutputAssemblyPath}");

            CliHelpers.OutputVerboseTimingSummary(result.Metrics, logger);
            CliHelpers.OutputMetrics(assemblyResult.Metrics, metricsFormat, metricsOutput);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    internal class SingleFileProjectConfig : ProjectConfig
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
