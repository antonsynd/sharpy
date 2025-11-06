using System.CommandLine;
using Sharpy.Compiler.Lexer;

namespace Sharpy.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("sharpyc - Sharpy Compiler");

        // Input file argument
        var inputFileArgument = new Argument<FileInfo>(
            name: "input",
            description: "The .spy file to compile"
        );

        // Emit mode options
        var emitTokensOption = new Option<bool>(
            name: "--emit-tokens",
            description: "Emit lexer tokens for the input file"
        );

        var emitAstOption = new Option<bool>(
            name: "--emit-ast",
            description: "Emit the abstract syntax tree (AST) [NOT IMPLEMENTED]"
        );

        // Output type option
        var outputTypeOption = new Option<string>(
            name: "--output-type",
            description: "Output type: 'library' or 'exe' [NOT IMPLEMENTED]",
            getDefaultValue: () => "library"
        );
        outputTypeOption.AddAlias("-t");

        // Output file option
        var outputOption = new Option<FileInfo?>(
            name: "--output",
            description: "Output file path [NOT IMPLEMENTED]"
        );
        outputOption.AddAlias("-o");

        // .NET reference options
        var referenceOption = new Option<string[]>(
            name: "--reference",
            description: ".NET DLL references [NOT IMPLEMENTED]"
        )
        { AllowMultipleArgumentsPerToken = true };
        referenceOption.AddAlias("-r");

        var projectReferenceOption = new Option<string[]>(
            name: "--project-reference",
            description: ".NET project references [NOT IMPLEMENTED]"
        )
        { AllowMultipleArgumentsPerToken = true };
        projectReferenceOption.AddAlias("-p");

        // Add options to command
        rootCommand.AddArgument(inputFileArgument);
        rootCommand.AddOption(emitTokensOption);
        rootCommand.AddOption(emitAstOption);
        rootCommand.AddOption(outputTypeOption);
        rootCommand.AddOption(outputOption);
        rootCommand.AddOption(referenceOption);
        rootCommand.AddOption(projectReferenceOption);

        rootCommand.SetHandler(
            HandleCommand,
            inputFileArgument,
            emitTokensOption,
            emitAstOption,
            outputTypeOption,
            outputOption,
            referenceOption,
            projectReferenceOption
        );

        return await rootCommand.InvokeAsync(args);
    }

    static void HandleCommand(
        FileInfo inputFile,
        bool emitTokens,
        bool emitAst,
        string outputType,
        FileInfo? output,
        string[] references,
        string[] projectReferences)
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
            EmitTokens(inputFile);
            return;
        }

        // Handle emit-ast mode (NOT IMPLEMENTED)
        if (emitAst)
        {
            Console.Error.WriteLine("Error: --emit-ast is not implemented yet");
            Environment.Exit(1);
        }

        // Handle compilation mode (NOT IMPLEMENTED)
        Console.Error.WriteLine("Error: Compilation to binary/library is not implemented yet");
        Console.Error.WriteLine("Available options:");
        Console.Error.WriteLine("  --emit-tokens    Emit lexer tokens (implemented)");
        Console.Error.WriteLine("  --emit-ast       Emit AST (not implemented)");
        Console.Error.WriteLine("  --output-type    Specify output type (not implemented)");
        Console.Error.WriteLine("  --output         Specify output file (not implemented)");
        Console.Error.WriteLine("  --reference      Add .NET DLL reference (not implemented)");
        Console.Error.WriteLine("  --project-reference  Add .NET project reference (not implemented)");
        Environment.Exit(1);
    }

    static void EmitTokens(FileInfo inputFile)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var lexer = new Lexer(source);
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

}
