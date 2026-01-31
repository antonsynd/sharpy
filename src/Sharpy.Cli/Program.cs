#pragma warning disable CS0618 // LexerError and ParserError are obsolete
using System.CommandLine;
using Sharpy.Compiler;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Cli;

class Program
{
    static int Main(string[] args)
    {
        var rootCommand = new RootCommand("sharpyc - Sharpy Compiler");

        // === Global Options ===
        var logLevelOption = new Option<CompilerLogLevel?>("--log-level") { Description = "Set compiler log level (None, Error, Warning, Info, Debug)" };
        var logFileOption = new Option<FileInfo?>("--log-file") { Description = "Write compiler logs to the specified file" };
        var metricsFormatOption = new Option<string?>("--metrics-format") { Description = "Output compilation metrics (text or json)" };
        var metricsOutputOption = new Option<FileInfo?>("--metrics-output") { Description = "Write metrics to the specified file" };

        rootCommand.Options.Add(logLevelOption);
        rootCommand.Options.Add(logFileOption);
        rootCommand.Options.Add(metricsFormatOption);
        rootCommand.Options.Add(metricsOutputOption);

        // === Build Command ===
        var buildCommand = new Command("build", "Compile a Sharpy source file to a binary or library");

        var buildInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file to compile" };
        var buildTypeOpt = new Option<string?>("--type") { Description = "Output type: 'exe' or 'library' (default: library)" };
        buildTypeOpt.Aliases.Add("-t");
        var buildOutputOpt = new Option<FileInfo?>("--output") { Description = "Output file path" };
        buildOutputOpt.Aliases.Add("-o");
        var buildRefOpt = new Option<string[]>("--reference") { Description = "Add .NET assembly references", AllowMultipleArgumentsPerToken = true };
        buildRefOpt.Aliases.Add("-r");
        var buildProjRefOpt = new Option<string[]>("--project-reference") { Description = "Add .NET project references", AllowMultipleArgumentsPerToken = true };
        buildProjRefOpt.Aliases.Add("-p");
        var buildModPathOpt = new Option<string[]>("--module-path") { Description = "Additional paths to search for modules", AllowMultipleArgumentsPerToken = true };
        buildModPathOpt.Aliases.Add("-m");

        buildCommand.Arguments.Add(buildInputArg);
        buildCommand.Options.Add(buildTypeOpt);
        buildCommand.Options.Add(buildOutputOpt);
        buildCommand.Options.Add(buildRefOpt);
        buildCommand.Options.Add(buildProjRefOpt);
        buildCommand.Options.Add(buildModPathOpt);

        buildCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(buildInputArg)!;
            var type = parseResult.GetValue(buildTypeOpt) ?? "library";
            var output = parseResult.GetValue(buildOutputOpt);
            var reference = parseResult.GetValue(buildRefOpt) ?? Array.Empty<string>();
            var projectReference = parseResult.GetValue(buildProjRefOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(buildModPathOpt) ?? Array.Empty<string>();
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var metricsFormat = parseResult.GetValue(metricsFormatOption);
            var metricsOutput = parseResult.GetValue(metricsOutputOption);

            var logger = CreateLogger(logLevel, logFile);
            HandleBuildCommand(input, type, output, reference, projectReference, modulePath, logger, metricsFormat, metricsOutput);
        });

        // === Run Command ===
        var runCommand = new Command("run", "Compile and execute a Sharpy source file");

        var runInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file to run" };
        var runOutputOpt = new Option<FileInfo?>("--output") { Description = "Output file path (temporary if not specified)" };
        runOutputOpt.Aliases.Add("-o");
        var runRefOpt = new Option<string[]>("--reference") { Description = "Add .NET assembly references", AllowMultipleArgumentsPerToken = true };
        runRefOpt.Aliases.Add("-r");
        var runProjRefOpt = new Option<string[]>("--project-reference") { Description = "Add .NET project references", AllowMultipleArgumentsPerToken = true };
        runProjRefOpt.Aliases.Add("-p");
        var runModPathOpt = new Option<string[]>("--module-path") { Description = "Additional paths to search for modules", AllowMultipleArgumentsPerToken = true };
        runModPathOpt.Aliases.Add("-m");
        var runArgsOpt = new Option<string[]>("--args") { Description = "Arguments to pass to the program", AllowMultipleArgumentsPerToken = true };

        runCommand.Arguments.Add(runInputArg);
        runCommand.Options.Add(runOutputOpt);
        runCommand.Options.Add(runRefOpt);
        runCommand.Options.Add(runProjRefOpt);
        runCommand.Options.Add(runModPathOpt);
        runCommand.Options.Add(runArgsOpt);

        runCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(runInputArg)!;
            var output = parseResult.GetValue(runOutputOpt);
            var reference = parseResult.GetValue(runRefOpt) ?? Array.Empty<string>();
            var projectReference = parseResult.GetValue(runProjRefOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(runModPathOpt) ?? Array.Empty<string>();
            var progArgs = parseResult.GetValue(runArgsOpt) ?? Array.Empty<string>();
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var metricsFormat = parseResult.GetValue(metricsFormatOption);
            var metricsOutput = parseResult.GetValue(metricsOutputOption);

            var logger = CreateLogger(logLevel, logFile);
            HandleRunCommand(input, output, reference, projectReference, modulePath, progArgs, logger, metricsFormat, metricsOutput);
        });

        // === Project Command ===
        var projectCommand = new Command("project", "Build a Sharpy project from a .spyproj file");

        var projFileArg = new Argument<FileInfo?>("project") { Description = "Path to .spyproj file (auto-discovers if not specified)", Arity = ArgumentArity.ZeroOrOne };
        var projConfigOpt = new Option<string?>("--configuration") { Description = "Build configuration (Debug or Release)" };
        projConfigOpt.Aliases.Add("-c");
        var projCleanOpt = new Option<bool>("--clean") { Description = "Delete bin/ and obj/ directories before building" };
        var projEmitCsOpt = new Option<DirectoryInfo?>("--emit-cs-to") { Description = "Save generated C# code to the specified directory" };

        projectCommand.Arguments.Add(projFileArg);
        projectCommand.Options.Add(projConfigOpt);
        projectCommand.Options.Add(projCleanOpt);
        projectCommand.Options.Add(projEmitCsOpt);

        projectCommand.SetAction((parseResult) =>
        {
            var project = parseResult.GetValue(projFileArg);
            var configuration = parseResult.GetValue(projConfigOpt) ?? "Debug";
            var clean = parseResult.GetValue(projCleanOpt);
            var emitCsTo = parseResult.GetValue(projEmitCsOpt);
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var metricsFormat = parseResult.GetValue(metricsFormatOption);
            var metricsOutput = parseResult.GetValue(metricsOutputOption);

            var logger = CreateLogger(logLevel, logFile);
            HandleProjectCommand(project, configuration, clean, emitCsTo, logger, logLevel, metricsFormat, metricsOutput);
        });

        // === Emit Command (with subcommands) ===
        var emitCommand = new Command("emit", "Emit compiler intermediate representations");

        var emitTokensCommand = new Command("tokens", "Emit tokenized output");
        var emitTokensInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        emitTokensCommand.Arguments.Add(emitTokensInputArg);
        emitTokensCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(emitTokensInputArg)!;
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var logger = CreateLogger(logLevel, logFile);
            EmitTokens(input, logger);
        });

        var emitAstCommand = new Command("ast", "Emit abstract syntax tree");
        var emitAstInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        emitAstCommand.Arguments.Add(emitAstInputArg);
        emitAstCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(emitAstInputArg)!;
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var logger = CreateLogger(logLevel, logFile);
            EmitAst(input, logger);
        });

        var emitCsharpCommand = new Command("csharp", "Emit generated C# code");
        var emitCsharpInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        var emitCsharpOutputOpt = new Option<FileInfo?>("--output") { Description = "Output file path" };
        emitCsharpOutputOpt.Aliases.Add("-o");
        emitCsharpCommand.Arguments.Add(emitCsharpInputArg);
        emitCsharpCommand.Options.Add(emitCsharpOutputOpt);
        emitCsharpCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(emitCsharpInputArg)!;
            var output = parseResult.GetValue(emitCsharpOutputOpt);
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var logger = CreateLogger(logLevel, logFile);
            EmitCSharp(input, output, logger);
        });

        var emitParseCommand = new Command("parse", "Validate lexing and parsing only");
        var emitParseInputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        emitParseCommand.Arguments.Add(emitParseInputArg);
        emitParseCommand.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(emitParseInputArg)!;
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var logger = CreateLogger(logLevel, logFile);
            EmitParse(input, logger);
        });

        emitCommand.Subcommands.Add(emitTokensCommand);
        emitCommand.Subcommands.Add(emitAstCommand);
        emitCommand.Subcommands.Add(emitCsharpCommand);
        emitCommand.Subcommands.Add(emitParseCommand);

        // === Cache Command (with subcommands) ===
        var cacheCommand = new Command("cache", "Manage the overload discovery cache");

        var cacheClearCommand = new Command("clear", "Clear the cache");
        var cacheClearDirOpt = new Option<string?>("--cache-dir") { Description = "Custom cache directory" };
        cacheClearCommand.Options.Add(cacheClearDirOpt);
        cacheClearCommand.SetAction((parseResult) =>
        {
            var cacheDir = parseResult.GetValue(cacheClearDirOpt);
            ClearCache(cacheDir);
        });

        var cacheInfoCommand = new Command("info", "Display cache information");
        var cacheInfoDirOpt = new Option<string?>("--cache-dir") { Description = "Custom cache directory" };
        cacheInfoCommand.Options.Add(cacheInfoDirOpt);
        cacheInfoCommand.SetAction((parseResult) =>
        {
            var cacheDir = parseResult.GetValue(cacheInfoDirOpt);
            ShowCacheInfo(cacheDir);
        });

        cacheCommand.Subcommands.Add(cacheClearCommand);
        cacheCommand.Subcommands.Add(cacheInfoCommand);

        // === Add all commands to root ===
        rootCommand.Subcommands.Add(buildCommand);
        rootCommand.Subcommands.Add(runCommand);
        rootCommand.Subcommands.Add(projectCommand);
        rootCommand.Subcommands.Add(emitCommand);
        rootCommand.Subcommands.Add(cacheCommand);

        return rootCommand.Parse(args).Invoke();
    }

    static ICompilerLogger CreateLogger(CompilerLogLevel logLevel, FileInfo? logFile)
    {
        if (logLevel == CompilerLogLevel.None)
        {
            return NullLogger.Instance;
        }
        else if (logFile != null)
        {
            var stream = new StreamWriter(logFile.FullName, append: false);
            return new ConsoleCompilerLogger(logLevel, stream, stream);
        }
        else
        {
            return new ConsoleCompilerLogger(logLevel);
        }
    }

    static void OutputMetrics(CompilationMetrics? metrics, string? metricsFormat, FileInfo? metricsOutput)
    {
        if (metrics == null || metricsFormat == null)
            return;

        var format = metricsFormat.ToLowerInvariant();
        if (format != "text" && format != "json")
        {
            Console.Error.WriteLine($"Invalid metrics format: {metricsFormat}. Use 'text' or 'json'.");
            return;
        }

        var output = format == "json" ? metrics.FormatAsJson() : metrics.FormatAsText();

        if (metricsOutput != null)
        {
            try
            {
                File.WriteAllText(metricsOutput.FullName, output);
                Console.WriteLine($"Metrics written to: {metricsOutput.FullName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write metrics to file: {ex.Message}");
                Console.WriteLine(output);
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(output);
        }
    }

    static void OutputProjectMetrics(ProjectCompilationMetrics? metrics, string? metricsFormat, FileInfo? metricsOutput)
    {
        if (metrics == null || metricsFormat == null)
            return;

        var format = metricsFormat.ToLowerInvariant();
        if (format != "text" && format != "json")
        {
            Console.Error.WriteLine($"Invalid metrics format: {metricsFormat}. Use 'text' or 'json'.");
            return;
        }

        var output = format == "json" ? metrics.FormatAsJson() : metrics.FormatAsText();

        if (metricsOutput != null)
        {
            try
            {
                File.WriteAllText(metricsOutput.FullName, output);
                Console.WriteLine($"Metrics written to: {metricsOutput.FullName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write metrics to file: {ex.Message}");
                Console.WriteLine(output);
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(output);
        }
    }

    static void HandleBuildCommand(
        FileInfo inputFile,
        string outputType,
        FileInfo? output,
        string[] references,
        string[] projectReferences,
        string[] modulePaths,
        ICompilerLogger logger,
        string? metricsFormat,
        FileInfo? metricsOutput)
    {
        ValidateInputFile(inputFile);
        CompileToBinary(inputFile, outputType, output, references, projectReferences, modulePaths, logger, metricsFormat, metricsOutput);
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
        FileInfo? metricsOutput)
    {
        ValidateInputFile(inputFile);

        // Determine output path - use temp file if not specified
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
            // Compile to executable
            CompileToBinary(inputFile, "exe", new FileInfo(outputPath), references, projectReferences, modulePaths, logger, metricsFormat, metricsOutput);

            // Copy Sharpy.Core.dll to the output directory so the executable can find it
            var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;
            var sharpyCorePath = sharpyCoreAssembly.Location;
            var outputDir = Path.GetDirectoryName(outputPath)!;
            var sharpyCoreDestPath = Path.Combine(outputDir, "Sharpy.Core.dll");
            File.Copy(sharpyCorePath, sharpyCoreDestPath, overwrite: true);

            // TODO: Implement self-contained publish mode to bundle runtime + all dependencies
            // instead of manually copying Sharpy.Core.dll. This would make the run command
            // truly standalone without requiring dotnet to be installed.

            // Run the compiled executable
            Console.WriteLine();
            Console.WriteLine("=== Running Program ===");
            Console.WriteLine();

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { outputPath },
                UseShellExecute = false
            };

            // Add program arguments
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit();

                // Clean up temp files after execution if needed
                if (isTempOutput)
                {
                    try
                    {
                        var basePath = Path.GetDirectoryName(outputPath)!;
                        File.Delete(outputPath);
                        File.Delete(Path.Combine(basePath, tempBaseName + ".runtimeconfig.json"));
                        File.Delete(Path.Combine(basePath, tempBaseName + ".deps.json"));
                        File.Delete(Path.Combine(basePath, tempBaseName + ".pdb"));
                        File.Delete(sharpyCoreDestPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }

                Environment.Exit(process.ExitCode);
            }
        }
        catch (Exception)
        {
            // Clean up temp files on error if needed
            if (isTempOutput && File.Exists(outputPath))
            {
                try
                {
                    var basePath = Path.GetDirectoryName(outputPath)!;
                    File.Delete(outputPath);
                    File.Delete(Path.Combine(basePath, tempBaseName + ".runtimeconfig.json"));
                    File.Delete(Path.Combine(basePath, tempBaseName + ".deps.json"));
                    File.Delete(Path.Combine(basePath, tempBaseName + ".pdb"));

                    // Also clean up Sharpy.Core.dll
                    var sharpyCoreDestPath = Path.Combine(basePath, "Sharpy.Core.dll");
                    if (File.Exists(sharpyCoreDestPath))
                    {
                        File.Delete(sharpyCoreDestPath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            throw;
        }
    }

    static void HandleProjectCommand(
        FileInfo? projectFile,
        string configuration,
        bool clean,
        DirectoryInfo? emitCsTo,
        ICompilerLogger logger,
        CompilerLogLevel logLevel,
        string? metricsFormat,
        FileInfo? metricsOutput)
    {
        FileInfo? resolvedProjectFile = projectFile;

        if (resolvedProjectFile == null)
        {
            // Auto-discover .spyproj file in current directory
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

        CompileProject(resolvedProjectFile, configuration, clean, emitCsTo, logger, logLevel, metricsFormat, metricsOutput);
    }

    static void ValidateInputFile(FileInfo inputFile)
    {
        if (!inputFile.Exists)
        {
            Console.Error.WriteLine($"Error: Input file '{inputFile.FullName}' does not exist.");
            Environment.Exit(1);
        }

        if (inputFile.Extension != ".spy")
        {
            Console.Error.WriteLine($"Warning: Input file '{inputFile.Name}' does not have .spy extension.");
        }
    }

    static void EmitTokens(FileInfo inputFile, ICompilerLogger logger)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var lexer = new Lexer(source, logger);
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
            {
                foreach (var diag in lexer.Diagnostics.GetErrors())
                    Console.Error.WriteLine($"  {FormatDiagnostic(diag)}");
                Environment.Exit(1);
            }

            Console.WriteLine($"Tokens for {inputFile.Name}:");
            Console.WriteLine(new string('=', 80));

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                var value = string.IsNullOrEmpty(token.Value) ? "" : $" = '{token.Value}'";
                Console.WriteLine($"{i,4}: {token.Type,-20} @ L{token.Line}:C{token.Column}{value}");
            }

            Console.WriteLine(new string('=', 80));
            Console.WriteLine($"Total tokens: {tokens.Count}");
        }
        catch (LexerError ex)
        {
            Console.Error.WriteLine($"Lexer error at line {ex.Line}, column {ex.Column}:");
            Console.Error.WriteLine($"  {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void EmitAst(FileInfo inputFile, ICompilerLogger logger)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var lexer = new Lexer(source, logger);
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
            {
                foreach (var diag in lexer.Diagnostics.GetErrors())
                    Console.Error.WriteLine($"  {FormatDiagnostic(diag)}");
                Environment.Exit(1);
            }

            var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
            var module = parser.ParseModule();

            if (parser.Diagnostics.HasErrors)
            {
                foreach (var diag in parser.Diagnostics.GetErrors())
                    Console.Error.WriteLine($"  {FormatDiagnostic(diag)}");
                Environment.Exit(1);
            }

            Console.WriteLine($"AST for {inputFile.Name}:");
            Console.WriteLine(new string('=', 80));

            var dumper = new AstDumper();
            var ast = dumper.Dump(module);
            Console.Write(ast);

            Console.WriteLine(new string('=', 80));
        }
        catch (LexerError ex)
        {
            Console.Error.WriteLine($"Lexer error at line {ex.Line}, column {ex.Column}:");
            Console.Error.WriteLine($"  {ex.Message}");
            Environment.Exit(1);
        }
        catch (ParserError ex)
        {
            Console.Error.WriteLine($"Parser error at line {ex.Line}, column {ex.Column}:");
            Console.Error.WriteLine($"  {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void EmitParse(FileInfo inputFile, ICompilerLogger logger)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var lexer = new Lexer(source, logger);
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
            {
                foreach (var diag in lexer.Diagnostics.GetErrors())
                    Console.Error.WriteLine($"  {FormatDiagnostic(diag)}");
                Environment.Exit(1);
            }

            var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
            parser.ParseModule();

            if (parser.Diagnostics.HasErrors)
            {
                foreach (var diag in parser.Diagnostics.GetErrors())
                    Console.Error.WriteLine($"  {FormatDiagnostic(diag)}");
                Environment.Exit(1);
            }

            Console.WriteLine("PARSE_OK");
        }
        catch (LexerError ex)
        {
            Console.Error.WriteLine($"Lexer error at line {ex.Line}, column {ex.Column}:");
            Console.Error.WriteLine($"  {ex.Message}");
            Environment.Exit(1);
        }
        catch (ParserError ex)
        {
            Console.Error.WriteLine($"Parser error at line {ex.Line}, column {ex.Column}:");
            Console.Error.WriteLine($"  {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void EmitCSharp(FileInfo inputFile, FileInfo? output, ICompilerLogger logger)
    {
        try
        {
            // Parse the Sharpy source file
            var source = File.ReadAllText(inputFile.FullName);
            var lexer = new Lexer(source, logger);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
            var module = parser.ParseModule();

            // Run semantic analysis to register type aliases and resolve types
            var builtins = new BuiltinRegistry();
            var symbolTable = new SymbolTable(builtins);
            var semanticInfo = new SemanticInfo();

            // Name resolution pass (registers type aliases, classes, functions, etc.)
            var nameResolver = new NameResolver(symbolTable, logger);
            nameResolver.ResolveDeclarations(module);
            nameResolver.ResolveInheritance();

            if (nameResolver.Diagnostics.HasErrors)
            {
                Console.Error.WriteLine("Name resolution errors:");
                foreach (var diag in nameResolver.Diagnostics.GetErrors())
                {
                    Console.Error.WriteLine($"  {FormatDiagnostic(diag)}");
                }
                Environment.Exit(1);
            }

            // Type checking pass
            var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
            var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger);
            typeChecker.CheckModule(module, computeCodeGenInfo: true);

            if (typeChecker.Errors.Any())
            {
                Console.Error.WriteLine("Type checking errors:");
                var bag = DiagnosticBag.FromSemanticErrors(typeChecker.Errors);
                foreach (var diag in bag.GetErrors())
                {
                    Console.Error.WriteLine($"  {FormatDiagnostic(diag)}");
                }
                Environment.Exit(1);
            }

            // Set up code generation context with the analyzed symbol table
            var context = new CodeGenContext(symbolTable, builtins)
            {
                SourceFilePath = inputFile.FullName,
                // Single-file emit is treated as an entry point for consistency with run/build
                IsEntryPoint = true,
                Logger = logger,
                SemanticInfo = semanticInfo
            };

            // Generate C# code using RoslynEmitter
            var emitter = new RoslynEmitter(context);
            var compilationUnit = emitter.GenerateCompilationUnit(module);
            var csharpCode = compilationUnit.ToFullString();

            // Determine output file
            FileInfo outputFile;
            if (output != null)
            {
                outputFile = output;
            }
            else
            {
                // Default: replace .spy extension with .cs
                var outputPath = Path.ChangeExtension(inputFile.FullName, ".cs");
                outputFile = new FileInfo(outputPath);
            }

            // Write C# code to output file
            File.WriteAllText(outputFile.FullName, csharpCode);
            Console.WriteLine($"Generated C# code written to: {outputFile.FullName}");
        }
        catch (LexerError ex)
        {
            Console.Error.WriteLine($"Lexer error at line {ex.Line}, column {ex.Column}:");
            Console.Error.WriteLine($"  {ex.Message}");
            Environment.Exit(1);
        }
        catch (ParserError ex)
        {
            Console.Error.WriteLine($"Parser error at line {ex.Line}, column {ex.Column}:");
            Console.Error.WriteLine($"  {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void ClearCache(string? cacheDir)
    {
        try
        {
            var cache = new OverloadIndexCache(cacheDir);
            cache.ClearAll();
            Console.WriteLine("Overload discovery cache cleared successfully.");
            if (cacheDir != null)
            {
                Console.WriteLine($"Cache directory: {cacheDir}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error clearing cache: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void CompileProject(FileInfo projectFile, string configuration, bool clean, DirectoryInfo? emitCsTo, ICompilerLogger logger, CompilerLogLevel logLevel = CompilerLogLevel.None, string? metricsFormat = null, FileInfo? metricsOutput = null)
    {
        try
        {
            // Load project configuration
            var projectConfig = ProjectFileParser.Load(projectFile.FullName, configuration);

            // Handle clean if requested
            if (clean)
            {
                CleanProject(projectConfig);
            }

            Console.WriteLine($"Project: {projectConfig.RootNamespace}");
            Console.WriteLine($"Configuration: {projectConfig.Configuration}");
            Console.WriteLine($"Output: {projectConfig.OutputType}");
            Console.WriteLine($"Source files: {projectConfig.SourceFiles.Count}");
            Console.WriteLine();

            // Create compiler with options
            var compilerOptions = new CompilerOptions
            {
                References = projectConfig.References.ToArray(),
                ModulePaths = projectConfig.ModulePaths.ToArray()
            };

            var compiler = new Sharpy.Compiler.Compiler(compilerOptions, logger);

            // Compile the project
            var result = compiler.CompileProject(projectConfig);

            // Save generated C# code if requested
            if (emitCsTo != null && result.GeneratedCSharpFiles.Any())
            {
                SaveGeneratedCSharp(emitCsTo, result.GeneratedCSharpFiles);
            }

            // Display warnings
            if (result.Warnings.Any())
            {
                Console.WriteLine("Warnings:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"  {warning}");
                }
                Console.WriteLine();
            }

            // Check for errors
            if (!result.Success)
            {
                Console.Error.WriteLine("Build FAILED.");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Errors:");
                foreach (var error in result.Diagnostics.GetErrors())
                {
                    Console.Error.WriteLine($"  {FormatDiagnostic(error)}");
                }
                Environment.Exit(1);
            }

            // Success
            Console.WriteLine("Build succeeded.");
            Console.WriteLine($"Output: {result.OutputAssemblyPath}");

            // Output metrics if requested
            OutputProjectMetrics(result.Metrics, metricsFormat, metricsOutput);
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

    static void ShowCacheInfo(string? cacheDir)
    {
        try
        {
            var cache = new OverloadIndexCache(cacheDir);
            var info = cache.GetInfo();

            Console.WriteLine("Overload Discovery Cache Information:");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"Cache Directory: {info.CacheDirectory}");
            Console.WriteLine($"Cached Assemblies: {info.CachedAssemblies}");
            Console.WriteLine($"Total Size: {FormatBytes(info.TotalSizeBytes)}");
            Console.WriteLine(new string('=', 50));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error retrieving cache info: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Format a diagnostic for CLI output.
    /// Format: file.spy(3,5): error SHP0201: Undefined variable 'x'
    /// </summary>
    static string FormatDiagnostic(CompilerDiagnostic diagnostic)
    {
        var parts = new List<string>();

        // File and location
        if (!string.IsNullOrEmpty(diagnostic.FilePath))
        {
            var file = Path.GetFileName(diagnostic.FilePath);
            if (diagnostic.Line.HasValue && diagnostic.Column.HasValue)
                parts.Add($"{file}({diagnostic.Line},{diagnostic.Column})");
            else if (diagnostic.Line.HasValue)
                parts.Add($"{file}({diagnostic.Line})");
            else
                parts.Add(file);
        }
        else if (diagnostic.Line.HasValue)
        {
            if (diagnostic.Column.HasValue)
                parts.Add($"({diagnostic.Line},{diagnostic.Column})");
            else
                parts.Add($"({diagnostic.Line})");
        }

        // Severity
        var severity = diagnostic.Severity switch
        {
            CompilerDiagnosticSeverity.Error => "error",
            CompilerDiagnosticSeverity.Warning => "warning",
            CompilerDiagnosticSeverity.Info => "info",
            CompilerDiagnosticSeverity.Hint => "hint",
            _ => "diagnostic"
        };

        // Code and message
        if (!string.IsNullOrEmpty(diagnostic.Code))
            parts.Add($"{severity} {diagnostic.Code}: {diagnostic.Message}");
        else
            parts.Add($"{severity}: {diagnostic.Message}");

        return string.Join(": ", parts);
    }

    static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
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

            // Delete bin/ directory
            var binDir = Path.Combine(projectDir, "bin");
            if (Directory.Exists(binDir))
            {
                Console.WriteLine($"Deleting: {binDir}");
                Directory.Delete(binDir, recursive: true);
            }

            // Delete obj/ directory (if it exists - not currently used but may be in future)
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
            // Create output directory if it doesn't exist
            if (!outputDir.Exists)
            {
                outputDir.Create();
            }

            Console.WriteLine($"Saving generated C# code to: {outputDir.FullName}");

            foreach (var (modulePath, csCode) in generatedFiles)
            {
                // Create a filename based on the module path
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

    static void CompileToBinary(
        FileInfo inputFile,
        string outputType,
        FileInfo? output,
        string[] references,
        string[] projectReferences,
        string[] modulePaths,
        ICompilerLogger logger,
        string? metricsFormat,
        FileInfo? metricsOutput)
    {
        try
        {
            // Read source file
            var source = File.ReadAllText(inputFile.FullName);

            // Create compiler with options
            var compilerOptions = new CompilerOptions
            {
                References = references,
                ModulePaths = modulePaths
            };

            var compiler = new Sharpy.Compiler.Compiler(compilerOptions, logger);

            // Compile to get C# code
            var result = compiler.Compile(source, inputFile.FullName);

            if (!result.Success)
            {
                Console.Error.WriteLine("Compilation failed:");
                foreach (var diagnostic in result.Diagnostics.GetErrors())
                {
                    Console.Error.WriteLine($"  {FormatDiagnostic(diagnostic)}");
                }
                Environment.Exit(1);
            }

            // Determine output path
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

            // Create output directory if needed
            var outputDirectory = Path.GetDirectoryName(finalOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Create a minimal project config for single-file compilation
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

            // Prepare C# sources for compilation - use all generated files (entry + imports)
            var csharpSources = new Dictionary<string, string>();
            foreach (var (sourcePath, csCode) in result.GeneratedCSharpFiles)
            {
                var csFileName = Path.ChangeExtension(sourcePath, ".cs");
                csharpSources[csFileName] = csCode;
            }

            // Fallback for backward compatibility if GeneratedCSharpFiles is empty
            if (csharpSources.Count == 0 && result.GeneratedCSharpCode != null)
            {
                csharpSources[Path.ChangeExtension(inputFile.FullName, ".cs")] = result.GeneratedCSharpCode;
            }

            // Compile to assembly
            var assemblyCompiler = new AssemblyCompiler(logger);
            var assemblyResult = assemblyCompiler.CompileToAssembly(csharpSources, projectConfig);

            if (!assemblyResult.Success)
            {
                Console.Error.WriteLine("Assembly compilation failed:");
                foreach (var error in assemblyResult.Errors)
                {
                    Console.Error.WriteLine($"  {error}");
                }
                Environment.Exit(1);
            }

            // Display warnings
            if (assemblyResult.Warnings.Any())
            {
                Console.WriteLine("Warnings:");
                foreach (var warning in assemblyResult.Warnings)
                {
                    Console.WriteLine($"  {warning}");
                }
                Console.WriteLine();
            }

            // Success
            Console.WriteLine($"Successfully compiled to: {assemblyResult.OutputAssemblyPath}");

            // Output metrics if requested (showing assembly compilation metrics)
            OutputMetrics(assemblyResult.Metrics, metricsFormat, metricsOutput);
        }
        catch (LexerError ex)
        {
            Console.Error.WriteLine($"Lexer error at line {ex.Line}, column {ex.Column}:");
            Console.Error.WriteLine($"  {ex.Message}");
            Environment.Exit(1);
        }
        catch (ParserError ex)
        {
            Console.Error.WriteLine($"Parser error at line {ex.Line}, column {ex.Column}:");
            Console.Error.WriteLine($"  {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Wrapper for ProjectConfig to override OutputAssemblyPath for single-file compilation
    /// </summary>
    private class SingleFileProjectConfig : ProjectConfig
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

            // Set properties
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
