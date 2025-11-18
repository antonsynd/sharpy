using System.CommandLine;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Cli;

class Program
{
    static int Main(string[] args)
    {
        var rootCommand = new RootCommand("sharpyc - Sharpy Compiler");

        // Input file argument (optional when using cache commands)
        var inputFileArgument = new Argument<FileInfo?>("input") { Arity = ArgumentArity.ZeroOrOne };

        // Emit mode options
        var emitTokensOption = new Option<bool>("--emit-tokens");
        var emitAstOption = new Option<bool>("--emit-ast");
        var emitCSharpOption = new Option<bool>("--emit-csharp");

        // Output type option
        var outputTypeOption = new Option<string?>("--output-type");
        outputTypeOption.Aliases.Add("-t");

        // Output file option
        var outputOption = new Option<FileInfo?>("--output");
        outputOption.Aliases.Add("-o");

        // .NET reference options
        var referenceOption = new Option<string[]>("--reference") { AllowMultipleArgumentsPerToken = true };
        referenceOption.Aliases.Add("-r");

        var projectReferenceOption = new Option<string[]>("--project-reference") { AllowMultipleArgumentsPerToken = true };
        projectReferenceOption.Aliases.Add("-p");

        // Module path option for searching assemblies
        var modulePathOption = new Option<string[]>("--module-path") { AllowMultipleArgumentsPerToken = true };
        modulePathOption.Aliases.Add("-m");

        // Logging options
        var logLevelOption = new Option<CompilerLogLevel?>("--log-level");
        var logFileOption = new Option<FileInfo?>("--log-file");

        // Cache management options
        var clearCacheOption = new Option<bool>("--clear-cache") { Description = "Clear the overload discovery cache" };
        var cacheInfoOption = new Option<bool>("--cache-info") { Description = "Display information about the overload discovery cache" };

        // Add options to command
        rootCommand.Arguments.Add(inputFileArgument);
        rootCommand.Options.Add(emitTokensOption);
        rootCommand.Options.Add(emitAstOption);
        rootCommand.Options.Add(emitCSharpOption);
        rootCommand.Options.Add(outputTypeOption);
        rootCommand.Options.Add(outputOption);
        rootCommand.Options.Add(referenceOption);
        rootCommand.Options.Add(projectReferenceOption);
        rootCommand.Options.Add(modulePathOption);
        rootCommand.Options.Add(logLevelOption);
        rootCommand.Options.Add(logFileOption);
        rootCommand.Options.Add(clearCacheOption);
        rootCommand.Options.Add(cacheInfoOption);

        rootCommand.SetAction((parseResult) =>
        {
            var inputFile = parseResult.GetValue(inputFileArgument);
            var emitTokens = parseResult.GetValue(emitTokensOption);
            var emitAst = parseResult.GetValue(emitAstOption);
            var emitCSharp = parseResult.GetValue(emitCSharpOption);
            var outputType = parseResult.GetValue(outputTypeOption) ?? "library";
            var output = parseResult.GetValue(outputOption);
            var references = parseResult.GetValue(referenceOption);
            var projectReferences = parseResult.GetValue(projectReferenceOption);
            var modulePaths = parseResult.GetValue(modulePathOption);
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);
            var clearCache = parseResult.GetValue(clearCacheOption);
            var cacheInfo = parseResult.GetValue(cacheInfoOption);

            // Handle cache management commands (no input file required)
            if (clearCache)
            {
                ClearCache();
                return;
            }

            if (cacheInfo)
            {
                ShowCacheInfo();
                return;
            }

            // For compilation commands, input file is required
            if (inputFile == null)
            {
                Console.Error.WriteLine("Error: Input file is required for compilation.");
                Console.Error.WriteLine("Use --clear-cache or --cache-info for cache management.");
                Environment.Exit(1);
            }

            // Create logger and optional file stream that needs disposal
            StreamWriter? fileStream = null;
            try
            {
                ICompilerLogger logger;
                if (logLevel == CompilerLogLevel.None)
                {
                    logger = NullLogger.Instance;
                }
                else if (logFile != null)
                {
                    fileStream = new StreamWriter(logFile.FullName, append: false);
                    logger = new ConsoleCompilerLogger(logLevel, fileStream, fileStream);
                }
                else
                {
                    logger = new ConsoleCompilerLogger(logLevel);
                }

                HandleCommand(inputFile, emitTokens, emitAst, emitCSharp, outputType, output,
                             references ?? Array.Empty<string>(),
                             projectReferences ?? Array.Empty<string>(),
                             modulePaths ?? Array.Empty<string>(),
                             logger);
            }
            finally
            {
                fileStream?.Flush();
                fileStream?.Dispose();
            }
        });

        return rootCommand.Parse(args).Invoke();
    }

    static void HandleCommand(
        FileInfo inputFile,
        bool emitTokens,
        bool emitAst,
        bool emitCSharp,
        string outputType,
        FileInfo? output,
        string[] references,
        string[] projectReferences,
        string[] modulePaths, // TODO: Use modulePaths when compiler integration is implemented
        ICompilerLogger logger)
    {
        // Validate input file
        if (!inputFile.Exists)
        {
            Console.Error.WriteLine($"Error: Input file '{inputFile.FullName}' does not exist.");
            Environment.Exit(1);
        }

        if (inputFile.Extension != ".spy")
        {
            Console.Error.WriteLine($"Warning: Input file '{inputFile.Name}' does not have .spy extension.");
        }

        // Check for conflicting options
        var emitOptions = new[] { emitTokens, emitAst, emitCSharp };
        if (emitOptions.Count(x => x) > 1)
        {
            Console.Error.WriteLine("Error: Cannot specify multiple emit options (--emit-tokens, --emit-ast, --emit-csharp)");
            Environment.Exit(1);
        }

        // Handle emit-tokens mode (IMPLEMENTED)
        if (emitTokens)
        {
            EmitTokens(inputFile, logger);
            return;
        }

        // Handle emit-ast mode (IMPLEMENTED)
        if (emitAst)
        {
            EmitAst(inputFile, logger);
            return;
        }

        // Handle emit-csharp mode (IMPLEMENTED)
        if (emitCSharp)
        {
            EmitCSharp(inputFile, output, logger);
            return;
        }

        // Handle compilation mode (NOT IMPLEMENTED)
        Console.Error.WriteLine("Error: Compilation to binary/library is not implemented yet");
        Console.Error.WriteLine("Available options:");
        Console.Error.WriteLine("  --emit-tokens       Emit lexer tokens (implemented)");
        Console.Error.WriteLine("  --emit-ast          Emit AST (implemented)");
        Console.Error.WriteLine("  --emit-csharp       Emit C# code (implemented)");
        Console.Error.WriteLine("  --clear-cache       Clear overload discovery cache (implemented)");
        Console.Error.WriteLine("  --cache-info        Show overload discovery cache info (implemented)");
        Console.Error.WriteLine("  --output-type       Specify output type (not implemented)");
        Console.Error.WriteLine("  --output            Specify output file (not implemented)");
        Console.Error.WriteLine("  --reference         Add .NET DLL reference (not implemented)");
        Console.Error.WriteLine("  --module-path       Add module search path (not implemented)");
        Console.Error.WriteLine("  --project-reference Add .NET project reference (not implemented)");
        Environment.Exit(1);
    }

    static void EmitTokens(FileInfo inputFile, ICompilerLogger logger)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var lexer = new Lexer(source, logger);
            var tokens = lexer.TokenizeAll();

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
            var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
            var module = parser.ParseModule();

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

            // Set up code generation context
            var builtins = new BuiltinRegistry();
            var symbolTable = new SymbolTable(builtins);
            var context = new CodeGenContext(symbolTable, builtins)
            {
                SourceFilePath = inputFile.FullName
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

    static void ClearCache()
    {
        try
        {
            var cache = new OverloadIndexCache();
            cache.ClearAll();
            Console.WriteLine("Overload discovery cache cleared successfully.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error clearing cache: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void ShowCacheInfo()
    {
        try
        {
            var cache = new OverloadIndexCache();
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
}
