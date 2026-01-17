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
using static Sharpy.Compiler.Tests.TestHelpers;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Base class for end-to-end integration tests that compile Sharpy code to C# and execute it.
/// </summary>
public abstract class IntegrationTestBase
{
    protected readonly ITestOutputHelper Output;
    private static readonly object ConsoleLock = new object();

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
        // Set up assembly resolution for Sharpy.Runtime
        string? runtimePath = null;
        ResolveEventHandler? resolveHandler = null;

        try
        {
            // Phase 1: Lex Sharpy code
            var logger = new OutputTestLogger(Output);
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
            nameResolver.ResolveInheritance(); // Second pass: resolve inheritance relationships

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
                SourceFilePath = fileName,
                IsEntryPoint = true  // Integration tests are executable programs
            };
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

            // Try to add Sharpy.Core reference if available
            try
            {
                // Find Sharpy.Core.dll in the build output directory
                var testAssemblyPath = Assembly.GetExecutingAssembly().Location;
                var testDir = Path.GetDirectoryName(testAssemblyPath);

                // Detect the current .NET version from the test assembly path
                // Path format: .../bin/Debug/net10.0/...
                var targetFramework = testDir!.Split(Path.DirectorySeparatorChar)
                    .FirstOrDefault(s => s.StartsWith("net") && char.IsDigit(s.Length > 3 ? s[3] : ' '))
                    ?? "net10.0"; // Default to net10.0

                runtimePath = Path.Combine(testDir!, "..", "..", "..", "..", "Sharpy.Core", "bin", "Debug", targetFramework, "Sharpy.Core.dll");
                runtimePath = Path.GetFullPath(runtimePath);

                if (File.Exists(runtimePath))
                {
                    references.Add(MetadataReference.CreateFromFile(runtimePath));
                    Output.WriteLine($"Loaded Sharpy.Core from: {runtimePath}");

                    // Set up assembly resolver for runtime execution
                    resolveHandler = (sender, args) =>
                    {
                        if (args.Name.StartsWith("Sharpy.Core,"))
                        {
                            return Assembly.LoadFrom(runtimePath);
                        }
                        return null;
                    };
                    AppDomain.CurrentDomain.AssemblyResolve += resolveHandler;
                }
                else
                {
                    Output.WriteLine($"Warning: Sharpy.Core not found at: {runtimePath}");
                }
            }
            catch (FileNotFoundException ex)
            {
                // Sharpy.Core not available, continue without it
                Output.WriteLine($"Warning: Failed to load Sharpy.Core: {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                Output.WriteLine($"Warning: Failed to load Sharpy.Core: {ex.Message}");
            }
            catch (BadImageFormatException ex)
            {
                Output.WriteLine($"Warning: Failed to load Sharpy.Core: {ex.Message}");
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

            // Lock console I/O to prevent interference from parallel tests
            lock (ConsoleLock)
            {
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
        catch (TargetInvocationException ex)
        {
            var errorMessage = ex.InnerException != null
                ? $"Unexpected error during execution: {ex.InnerException.Message}\nStack Trace: {ex.InnerException.StackTrace}"
                : $"Unexpected error during execution: {ex.Message}";

            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { errorMessage }
            };
        }
        catch (InvalidOperationException ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { $"Invalid operation: {ex.Message}" }
            };
        }
        catch (FileNotFoundException ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { $"File not found: {ex.Message}" }
            };
        }
        catch (TypeLoadException ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { $"Type load error: {ex.Message}" }
            };
        }
        // Generic catch as final fallback for any unexpected exceptions during compilation/execution
        // This is intentional as test infrastructure needs to handle arbitrary code gracefully
        catch (Exception ex)
        {
            var errorMessage = $"Unexpected error: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $"\nInner Exception: {ex.InnerException.Message}";
                errorMessage += $"\nStack Trace: {ex.InnerException.StackTrace}";
            }

            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { errorMessage }
            };
        }
        finally
        {
            // Clean up assembly resolver
            if (resolveHandler != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolveHandler;
            }
        }
    }
}
