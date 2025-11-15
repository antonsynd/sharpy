using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Base class for end-to-end integration tests that compile Sharpy code to C# and execute it.
/// </summary>
public abstract class IntegrationTestBase
{
    protected readonly ITestOutputHelper Output;

    protected IntegrationTestBase(ITestOutputHelper output)
    {
        Output = output;
    }

    /// <summary>
    /// Result of compiling and executing Sharpy code.
    /// </summary>
    protected class ExecutionResult
    {
        public bool Success { get; init; }
        public string StandardOutput { get; init; } = string.Empty;
        public string StandardError { get; init; } = string.Empty;
        public List<string> CompilationErrors { get; init; } = new();
        public string? GeneratedCSharp { get; init; }
        public Exception? Exception { get; init; }
    }

    /// <summary>
    /// Compiles Sharpy source code to C# and executes it, returning the result.
    /// </summary>
    protected ExecutionResult CompileAndExecute(string sharpySource, string fileName = "test.spy")
    {
        try
        {
            // Phase 1: Lex Sharpy code
            var logger = new TestLogger(Output);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(sharpySource, logger);
            var tokens = lexer.TokenizeAll();

            // Phase 2: Parse Sharpy code
            var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
            var module = parser.ParseModule();

            // Phase 3: Semantic analysis
            var builtinRegistry = new BuiltinRegistry();
            var symbolTable = new SymbolTable(builtinRegistry);
            var semanticInfo = new SemanticInfo();

            var nameResolver = new NameResolver(symbolTable, logger);
            nameResolver.ResolveDeclarations(module);

            if (nameResolver.Errors.Any())
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = nameResolver.Errors.Select(e => e.Message).ToList()
                };
            }

            var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
            var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger);
            typeChecker.CheckModule(module);

            if (typeChecker.Errors.Any())
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = typeChecker.Errors.Select(e => e.Message).ToList()
                };
            }

            // Phase 4: Generate C# code
            var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
            {
                SourceFilePath = fileName
            };
            NameMangler.Reset();
            var emitter = new RoslynEmitter(codeGenContext);
            var compilationUnit = emitter.GenerateCompilationUnit(module);
            var generatedCSharp = compilationUnit.ToFullString();

            Output.WriteLine("=== Generated C# ===");
            Output.WriteLine(generatedCSharp);
            Output.WriteLine("====================");

            // Phase 5: Compile C# to assembly
            var syntaxTree = CSharpSyntaxTree.ParseText(generatedCSharp);
            
            // Get references to required assemblies
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            };

            // Try to add Sharpy.Runtime reference if available
            try
            {
                var runtimeAssembly = Assembly.Load("Sharpy.Runtime");
                references.Add(MetadataReference.CreateFromFile(runtimeAssembly.Location));
            }
            catch
            {
                // Sharpy.Runtime not available, continue without it
                Output.WriteLine("Warning: Sharpy.Runtime assembly not available");
            }

            var compilation = CSharpCompilation.Create(
                "SharpyTestAssembly",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString())
                    .ToList();

                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = errors,
                    GeneratedCSharp = generatedCSharp
                };
            }

            // Phase 6: Execute the compiled assembly
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            var originalOut = Console.Out;
            var originalErr = Console.Error;

            try
            {
                using var outWriter = new StringWriter(stdout);
                using var errWriter = new StringWriter(stderr);
                Console.SetOut(outWriter);
                Console.SetError(errWriter);

                // Find and invoke the entry point
                var entryPoint = assembly.EntryPoint;
                if (entryPoint == null)
                {
                    // Try to find a Main method or main function
                    var moduleType = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("Module"));
                    if (moduleType != null)
                    {
                        var mainMethod = moduleType.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)
                                      ?? moduleType.GetMethod("main", BindingFlags.Public | BindingFlags.Static);
                        if (mainMethod != null)
                        {
                            mainMethod.Invoke(null, mainMethod.GetParameters().Length == 0 ? null : new object[] { Array.Empty<string>() });
                        }
                    }
                }
                else
                {
                    entryPoint.Invoke(null, entryPoint.GetParameters().Length == 0 ? null : new object[] { Array.Empty<string>() });
                }
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
            }

            return new ExecutionResult
            {
                Success = true,
                StandardOutput = stdout.ToString(),
                StandardError = stderr.ToString(),
                GeneratedCSharp = generatedCSharp
            };
        }
        catch (LexerError ex)
        {
            return new ExecutionResult
            {
                Success = false,
                CompilationErrors = new List<string> { $"Lexer error at line {ex.Line}, column {ex.Column}: {ex.Message}" }
            };
        }
        catch (ParserError ex)
        {
            return new ExecutionResult
            {
                Success = false,
                CompilationErrors = new List<string> { $"Parser error at line {ex.Line}, column {ex.Column}: {ex.Message}" }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { $"Unexpected error: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Simple logger that writes to test output.
    /// </summary>
    private class TestLogger : ICompilerLogger
    {
        private readonly ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public void LogTokenRead(string tokenType, int line, int column, string value)
        {
            // Don't log tokens during tests to avoid clutter
        }

        public void LogIndentChange(int oldLevel, int newLevel)
        {
            // Don't log indent changes during tests
        }

        public void LogParseEnter(string rule, int tokenPosition)
        {
            // Don't log parse enter during tests
        }

        public void LogParseExit(string rule, bool success)
        {
            // Don't log parse exit during tests
        }

        public void LogError(string message, int line, int column)
        {
            _output.WriteLine($"ERROR [{line},{column}]: {message}");
        }

        public void LogWarning(string message, int line, int column)
        {
            _output.WriteLine($"WARNING [{line},{column}]: {message}");
        }

        public void LogInfo(string message)
        {
            _output.WriteLine($"INFO: {message}");
        }

        public void LogDebug(string message)
        {
            _output.WriteLine($"DEBUG: {message}");
        }

        public void LogTrace(string message)
        {
            // Don't log trace during tests
        }

        public bool IsEnabled(CompilerLogLevel level)
        {
            return level <= CompilerLogLevel.Info;
        }
    }
}
