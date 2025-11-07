using System.CommandLine;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;

namespace Sharpy.Cli;

class Program
{
    static int Main(string[] args)
    {
        var rootCommand = new RootCommand("sharpyc - Sharpy Compiler");

        // Input file argument
        var inputFileArgument = new Argument<FileInfo>("input");

        // Emit mode options
        var emitTokensOption = new Option<bool>("--emit-tokens");
        var emitAstOption = new Option<bool>("--emit-ast");

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

        // Logging options
        var logLevelOption = new Option<CompilerLogLevel?>("--log-level");
        var logFileOption = new Option<FileInfo?>("--log-file");

        // Add options to command
        rootCommand.Arguments.Add(inputFileArgument);
        rootCommand.Options.Add(emitTokensOption);
        rootCommand.Options.Add(emitAstOption);
        rootCommand.Options.Add(outputTypeOption);
        rootCommand.Options.Add(outputOption);
        rootCommand.Options.Add(referenceOption);
        rootCommand.Options.Add(projectReferenceOption);
        rootCommand.Options.Add(logLevelOption);
        rootCommand.Options.Add(logFileOption);

        rootCommand.SetAction((parseResult) =>
        {
            var inputFile = parseResult.GetValue(inputFileArgument);
            var emitTokens = parseResult.GetValue(emitTokensOption);
            var emitAst = parseResult.GetValue(emitAstOption);
            var outputType = parseResult.GetValue(outputTypeOption) ?? "library";
            var output = parseResult.GetValue(outputOption);
            var references = parseResult.GetValue(referenceOption);
            var projectReferences = parseResult.GetValue(projectReferenceOption);
            var logLevel = parseResult.GetValue(logLevelOption) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(logFileOption);

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

                HandleCommand(inputFile!, emitTokens, emitAst, outputType, output,
                             references ?? Array.Empty<string>(),
                             projectReferences ?? Array.Empty<string>(),
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
        string outputType,
        FileInfo? output,
        string[] references,
        string[] projectReferences,
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
        if (emitTokens && emitAst)
        {
            Console.Error.WriteLine("Error: Cannot specify both --emit-tokens and --emit-ast");
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

        // Handle compilation mode (NOT IMPLEMENTED)
        Console.Error.WriteLine("Error: Compilation to binary/library is not implemented yet");
        Console.Error.WriteLine("Available options:");
        Console.Error.WriteLine("  --emit-tokens    Emit lexer tokens (implemented)");
        Console.Error.WriteLine("  --emit-ast       Emit AST (implemented)");
        Console.Error.WriteLine("  --output-type    Specify output type (not implemented)");
        Console.Error.WriteLine("  --output         Specify output file (not implemented)");
        Console.Error.WriteLine("  --reference      Add .NET DLL reference (not implemented)");
        Console.Error.WriteLine("  --project-reference  Add .NET project reference (not implemented)");
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
}
