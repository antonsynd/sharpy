using System.CommandLine;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Text;
using Sharpy.Lsp;

namespace Sharpy.Cli.Commands;

internal static class EmitCommand
{
    private static readonly Regex ValidNamespaceRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$",
        RegexOptions.Compiled);

    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("emit", "Emit compiler intermediate representations");

        ConfigureTokens(command, globals);
        ConfigureAst(command, globals);
        ConfigureCSharp(command, globals);
        ConfigureParse(command, globals);
        ConfigureDiagnostics(command, globals);
        ConfigureHover(command, globals);

        root.Subcommands.Add(command);
    }

    static void ConfigureTokens(Command parent, GlobalOptions globals)
    {
        var command = new Command("tokens", "Emit tokenized output");
        var inputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        command.Arguments.Add(inputArg);
        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var logLevel = parseResult.GetValue(globals.LogLevel) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(globals.LogFile);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);
            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            EmitTokens(input, logger, maxErrors);
        });
        parent.Subcommands.Add(command);
    }

    static void ConfigureAst(Command parent, GlobalOptions globals)
    {
        var command = new Command("ast", "Emit abstract syntax tree");
        var inputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        command.Arguments.Add(inputArg);
        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var logLevel = parseResult.GetValue(globals.LogLevel) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(globals.LogFile);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);
            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            EmitAst(input, logger, maxErrors);
        });
        parent.Subcommands.Add(command);
    }

    static void ConfigureCSharp(Command parent, GlobalOptions globals)
    {
        var command = new Command("csharp", "Emit generated C# code");
        var inputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        var outputOpt = new Option<FileInfo?>("--output") { Description = "Output file path" };
        outputOpt.Aliases.Add("-o");
        var refOpt = new Option<string[]>("--reference") { Description = "Add .NET assembly references", AllowMultipleArgumentsPerToken = true };
        refOpt.Aliases.Add("-r");
        var modPathOpt = new Option<string[]>("--module-path") { Description = "Additional paths to search for modules", AllowMultipleArgumentsPerToken = true };
        modPathOpt.Aliases.Add("-m");
        var lineDirectivesOpt = new Option<bool>("--show-line-directives") { Description = "Include #line directives for source mapping (default: stripped for clean output)" };
        var typeOpt = new Option<string?>("--type") { Description = "Output type: 'exe' or 'library' (default: exe)" };
        typeOpt.Aliases.Add("-t");
        var namespaceOpt = new Option<string?>("--namespace") { Description = "Wrap generated code in a namespace declaration" };
        namespaceOpt.Aliases.Add("-n");

        command.Arguments.Add(inputArg);
        command.Options.Add(outputOpt);
        command.Options.Add(refOpt);
        command.Options.Add(modPathOpt);
        command.Options.Add(lineDirectivesOpt);
        command.Options.Add(typeOpt);
        command.Options.Add(namespaceOpt);

        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var output = parseResult.GetValue(outputOpt);
            var reference = parseResult.GetValue(refOpt) ?? Array.Empty<string>();
            var modulePath = parseResult.GetValue(modPathOpt) ?? Array.Empty<string>();
            var showLineDirectives = parseResult.GetValue(lineDirectivesOpt);
            var emitType = parseResult.GetValue(typeOpt) ?? "exe";
            var namespaceName = parseResult.GetValue(namespaceOpt);
            var logLevel = parseResult.GetValue(globals.LogLevel) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(globals.LogFile);
            var warnAsError = parseResult.GetValue(globals.WarnAsError);
            var nowarn = parseResult.GetValue(globals.Nowarn);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);
            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            EmitCSharp(input, output, reference, modulePath, logger, warnAsError, nowarn, maxErrors, showLineDirectives, emitType, namespaceName);
        });
        parent.Subcommands.Add(command);
    }

    static void ConfigureParse(Command parent, GlobalOptions globals)
    {
        var command = new Command("parse", "Validate lexing and parsing only");
        var inputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        command.Arguments.Add(inputArg);
        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var logLevel = parseResult.GetValue(globals.LogLevel) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(globals.LogFile);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);
            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            EmitParse(input, logger, maxErrors);
        });
        parent.Subcommands.Add(command);
    }

    static void ConfigureDiagnostics(Command parent, GlobalOptions globals)
    {
        var command = new Command("diagnostics", "Emit compiler diagnostics");
        var inputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        var formatOpt = new Option<string?>("--format") { Description = "Output format: text or json" };
        formatOpt.Aliases.Add("-f");
        var includeCodegenOpt = new Option<bool>("--include-codegen") { Description = "Include code generation phase (uses Compile instead of Analyze)" };

        command.Arguments.Add(inputArg);
        command.Options.Add(formatOpt);
        command.Options.Add(includeCodegenOpt);

        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var format = parseResult.GetValue(formatOpt) ?? "text";
            if (format != "text" && format != "json")
            {
                Console.Error.WriteLine($"Error: --format must be 'text' or 'json', got '{format}'");
                Environment.Exit(1);
                return;
            }
            var includeCodegen = parseResult.GetValue(includeCodegenOpt);
            var logLevel = parseResult.GetValue(globals.LogLevel) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(globals.LogFile);
            var warnAsError = parseResult.GetValue(globals.WarnAsError);
            var nowarn = parseResult.GetValue(globals.Nowarn);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);
            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            EmitDiagnostics(input, logger, format, warnAsError, nowarn, maxErrors, includeCodegen);
        });
        parent.Subcommands.Add(command);
    }

    static void ConfigureHover(Command parent, GlobalOptions globals)
    {
        var command = new Command("hover", "Emit LSP hover information at a position");
        var inputArg = new Argument<FileInfo>("input") { Description = "Sharpy source file" };
        var lineOpt = new Option<int>("--line") { Description = "1-based line number", Required = true };
        var colOpt = new Option<int>("--col") { Description = "1-based column number", Required = true };

        command.Arguments.Add(inputArg);
        command.Options.Add(lineOpt);
        command.Options.Add(colOpt);

        command.SetAction((parseResult) =>
        {
            var input = parseResult.GetValue(inputArg)!;
            var line = parseResult.GetValue(lineOpt);
            var col = parseResult.GetValue(colOpt);
            var logLevel = parseResult.GetValue(globals.LogLevel) ?? CompilerLogLevel.None;
            var logFile = parseResult.GetValue(globals.LogFile);
            var maxErrors = parseResult.GetValue(globals.MaxErrors);
            var logger = CliHelpers.CreateLogger(logLevel, logFile);
            EmitHover(input, line, col, logger, maxErrors);
        });
        parent.Subcommands.Add(command);
    }

    static void EmitTokens(FileInfo inputFile, ICompilerLogger logger, int? maxErrors = null)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var sourceText = new SourceText(source, inputFile.FullName);
            var lexer = new Lexer(sourceText, logger);
            if (maxErrors is > 0)
            {
                lexer.MaxErrors = maxErrors.Value;
            }
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
            {
                CliHelpers.RenderDiagnostics(lexer.Diagnostics.GetErrors(), sourceText, Console.Error);
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
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void EmitAst(FileInfo inputFile, ICompilerLogger logger, int? maxErrors = null)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var sourceText = new SourceText(source, inputFile.FullName);
            var lexer = new Lexer(sourceText, logger);
            if (maxErrors is > 0)
            {
                lexer.MaxErrors = maxErrors.Value;
            }
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
            {
                CliHelpers.RenderDiagnostics(lexer.Diagnostics.GetErrors(), sourceText, Console.Error);
                Environment.Exit(1);
            }

            var parserMaxErrors = maxErrors is > 0 ? maxErrors.Value : 25;
            var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger, parserMaxErrors);
            var module = parser.ParseModule();

            if (parser.Diagnostics.HasErrors)
            {
                CliHelpers.RenderDiagnostics(parser.Diagnostics.GetErrors(), sourceText, Console.Error);
                Environment.Exit(1);
            }

            Console.WriteLine($"AST for {inputFile.Name}:");
            Console.WriteLine(new string('=', 80));

            var dumper = new AstDumper();
            var ast = dumper.Dump(module);
            Console.Write(ast);

            Console.WriteLine(new string('=', 80));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void EmitParse(FileInfo inputFile, ICompilerLogger logger, int? maxErrors = null)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var sourceText = new SourceText(source, inputFile.FullName);
            var lexer = new Lexer(sourceText, logger);
            if (maxErrors is > 0)
            {
                lexer.MaxErrors = maxErrors.Value;
            }
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
            {
                CliHelpers.RenderDiagnostics(lexer.Diagnostics.GetErrors(), sourceText, Console.Error);
                Environment.Exit(1);
            }

            var parserMaxErrors = maxErrors is > 0 ? maxErrors.Value : 25;
            var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger, parserMaxErrors);
            parser.ParseModule();

            if (parser.Diagnostics.HasErrors)
            {
                CliHelpers.RenderDiagnostics(parser.Diagnostics.GetErrors(), sourceText, Console.Error);
                Environment.Exit(1);
            }

            Console.WriteLine("PARSE_OK");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void EmitDiagnostics(FileInfo inputFile, ICompilerLogger logger, string format,
        bool warnAsError = false, string? nowarn = null, int? maxErrors = null, bool includeCodegen = false)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var api = new CompilerApi(logger);

            IReadOnlyList<CompilerDiagnostic> diagnostics;

            if (includeCodegen)
            {
                var compilerOptions = new CompilerOptions
                {
                    WarningsAsErrors = warnAsError,
                    SuppressedWarnings = CliHelpers.ParseNowarnCodes(nowarn),
                    MaxErrors = maxErrors ?? 0
                };
                var result = api.Compile(source, compilerOptions, inputFile.FullName);
                diagnostics = result.Diagnostics;
            }
            else
            {
                var result = api.Analyze(source);
                diagnostics = result.Diagnostics;
            }

            var hasErrors = diagnostics.Any(d => d.IsError);

            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var jsonItems = diagnostics.Select(d => new
                {
                    severity = d.Severity.ToString().ToLowerInvariant(),
                    code = d.Code ?? "SPY????",
                    line = d.Line ?? 0,
                    column = d.Column ?? 0,
                    message = d.Message,
                    phase = d.Phase.ToString()
                });

                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                Console.WriteLine(JsonSerializer.Serialize(jsonItems, jsonOptions));
            }
            else
            {
                if (diagnostics.Count == 0)
                {
                    Console.WriteLine("No diagnostics.");
                }
                else
                {
                    foreach (var d in diagnostics)
                    {
                        var severity = d.Severity.ToString().ToLowerInvariant();
                        var code = d.Code ?? "SPY????";
                        var line = d.Line ?? 0;
                        var col = d.Column ?? 0;
                        Console.WriteLine($"{severity} {code} ({line}:{col}): {d.Message}");
                    }
                }
            }

            if (hasErrors)
            {
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void EmitHover(FileInfo inputFile, int line, int col, ICompilerLogger logger, int? maxErrors = null)
    {
        try
        {
            var source = File.ReadAllText(inputFile.FullName);
            var api = new CompilerApi(logger);
            var result = api.Analyze(source);

            foreach (var d in result.Diagnostics.Where(d => d.IsError))
            {
                var code = d.Code ?? "SPY????";
                Console.Error.WriteLine($"error {code} ({d.Line ?? 0}:{d.Column ?? 0}): {d.Message}");
            }

            if (result.Ast == null || result.SemanticQuery == null)
            {
                Console.Error.WriteLine("Analysis failed: no AST or semantic info available.");
                Environment.Exit(1);
                return;
            }

            var hoverService = new HoverService(api);
            var hover = hoverService.GetHoverMarkdown(result, line, col);

            if (hover != null)
            {
                Console.WriteLine(hover);
            }
            else
            {
                Console.WriteLine("(no hover)");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void EmitCSharp(FileInfo inputFile, FileInfo? output, string[] references, string[] modulePaths,
        ICompilerLogger logger, bool warnAsError = false, string? nowarn = null, int? maxErrors = null,
        bool showLineDirectives = false, string outputType = "exe", string? namespaceName = null)
    {
        try
        {
            if (namespaceName != null && !ValidNamespaceRegex.IsMatch(namespaceName))
            {
                Console.Error.WriteLine($"Invalid namespace '{namespaceName}': must be a valid dotted identifier (e.g., 'Game.Scripts')");
                Environment.Exit(1);
            }

            var source = File.ReadAllText(inputFile.FullName);
            var sourceText = new SourceText(source, inputFile.FullName);

            var compilerOptions = new CompilerOptions
            {
                OutputType = outputType,
                References = references,
                ModulePaths = modulePaths,
                WarningsAsErrors = warnAsError,
                SuppressedWarnings = CliHelpers.ParseNowarnCodes(nowarn),
                MaxErrors = maxErrors ?? 0,
                Namespace = namespaceName
            };
            var api = new CompilerApi(logger);
            var result = api.Compile(source, compilerOptions, inputFile.FullName);

            if (!result.Success)
            {
                Console.Error.WriteLine("Compilation errors:");
                Console.Error.WriteLine();
                CliHelpers.RenderDiagnostics(result.Diagnostics.Where(d => d.IsError), sourceText, Console.Error);
                Environment.Exit(1);
            }

            var warnings = result.Diagnostics.Where(d => d.IsWarning).ToList();
            if (warnings.Count > 0)
            {
                CliHelpers.RenderDiagnostics(warnings, sourceText, Console.Out);
            }

            var csharpCode = result.GeneratedCSharp ?? "";
            if (!showLineDirectives)
            {
                csharpCode = CliHelpers.StripLineDirectives(csharpCode);
            }

            FileInfo outputFile;
            if (output != null)
            {
                outputFile = output;
            }
            else
            {
                var outputPath = Path.ChangeExtension(inputFile.FullName, ".cs");
                outputFile = new FileInfo(outputPath);
            }

            CliHelpers.OutputVerboseTimingSummary(result.Metrics, logger);

            File.WriteAllText(outputFile.FullName, csharpCode);
            Console.WriteLine($"Generated C# code written to: {outputFile.FullName}");

            var outputDir = outputFile.DirectoryName ?? ".";
            foreach (var (modulePath, moduleCode) in result.GeneratedCSharpFiles)
            {
                if (string.Equals(Path.GetFullPath(modulePath), Path.GetFullPath(inputFile.FullName),
                    StringComparison.OrdinalIgnoreCase))
                    continue;

                var moduleFileName = Path.GetFileNameWithoutExtension(modulePath) + ".cs";
                var moduleOutputPath = Path.Combine(outputDir, moduleFileName);
                var processedModuleCode = showLineDirectives ? moduleCode : CliHelpers.StripLineDirectives(moduleCode);
                File.WriteAllText(moduleOutputPath, processedModuleCode);
                Console.WriteLine($"Generated C# code written to: {moduleOutputPath}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
